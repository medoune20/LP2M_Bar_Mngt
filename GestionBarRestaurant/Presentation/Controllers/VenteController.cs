using Domaine;
using Domaine.Models;
using Infrastructure.Donnees;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Presentation.Filtres;

namespace Presentation.Controllers;

[AutorisationFiltre]
public class VenteController : BaseController
{
    private readonly AppDbContext _db;

    public VenteController(AppDbContext db)
    {
        _db = db;
    }

    private CaisseSession GetOrCreateCaisseRapide()
    {
        var caisse = _db.Caisses
            .Where(c => c.TenantId == TenantId
                        && c.Statut == StatutCaisse.Ouverte
                        && c.CaissierId == UtilisateurId)
            .OrderByDescending(c => c.DateOuverture)
            .FirstOrDefault();

        if (caisse != null) return caisse;

        caisse = new CaisseSession
        {
            TenantId = TenantId,
            DateOuverture = DateTime.Now,
            MontantOuverture = 0,
            Libelle = "Caisse rapide POS",
            Caissier = UtilisateurNom,
            CaissierId = UtilisateurId,
            Statut = StatutCaisse.Ouverte
        };

        _db.Caisses.Add(caisse);
        _db.SaveChanges();
        return caisse;
    }

    public IActionResult Index()
    {
        var ventes = _db.Ventes.Where(v => v.TenantId == TenantId);
        if (EstCaissier) ventes = ventes.Where(v => v.VendeurId == UtilisateurId);
        return View(ventes.OrderByDescending(v => v.DateVente).ToList());
    }

    [HttpGet]
    public IActionResult Rapide()
    {
        // Ouvre automatiquement une session POS personnelle si aucune caisse n'est ouverte.
        // Cela évite le blocage du bouton de paiement sur une base fraîche.
        GetOrCreateCaisseRapide();

        ViewBag.Produits = _db.Produits
            .AsNoTracking()
            .Where(p => p.TenantId == TenantId && p.Actif && p.StockActuel > 0)
            .OrderBy(p => p.Categorie).ThenBy(p => p.Nom)
            .ToList();
        ViewBag.Clients = _db.Clients
            .AsNoTracking()
            .Where(c => c.TenantId == TenantId && c.Actif)
            .OrderBy(c => c.Nom)
            .ToList();
        ViewBag.RegleFidelite = _db.ReglesFidelite.AsNoTracking().FirstOrDefault(r => r.TenantId == TenantId && r.Actif);
        ViewBag.CategoriesPOS = _db.Categories.AsNoTracking().Where(c => c.TenantId == TenantId && c.Actif)
            .OrderBy(c => c.Ordre).ThenBy(c => c.Nom).Select(c => c.Nom).ToList();

        var caissesQ = _db.Caisses.Where(c => c.TenantId == TenantId && c.Statut == StatutCaisse.Ouverte);
        if (EstCaissier) caissesQ = caissesQ.Where(c => c.CaissierId == UtilisateurId);
        var caisses = caissesQ.OrderByDescending(c => c.DateOuverture).ToList();
        ViewBag.CaissesDispo = caisses;
        ViewBag.CaisseOuverte = caisses.Any();
        return View();
    }

    [HttpPost]
    public IActionResult Rapide(int[] produitIds, int[] quantites, int clientId, string modePaiement, string? referencePaiement, decimal remise, int pointsFideliteUtilises, decimal montantRecu, int caisseSessionId, string? clientUuid)
    {
        // Idempotence : si une vente avec ce GUID existe déjà (double-clic ou
        // resynchronisation d'une vente hors-ligne), on ne la recrée pas.
        clientUuid = (clientUuid ?? string.Empty).Trim();
        if (clientUuid.Length > 0)
        {
            var existante = _db.Ventes.FirstOrDefault(v => v.TenantId == TenantId && v.ClientUuid == clientUuid);
            if (existante != null)
            {
                TempData["Succes"] = $"Vente déjà enregistrée. Ticket : {existante.NumeroTicket}.";
                return RedirectToAction(nameof(Details), new { id = existante.Id });
            }
        }

        if (produitIds.Length == 0 || quantites.Length == 0)
        {
            TempData["Erreur"] = "Le panier est vide.";
            return RedirectToAction(nameof(Rapide));
        }

        if (produitIds.Length != quantites.Length)
        {
            TempData["Erreur"] = "Le panier est incohérent. Merci de le revalider.";
            return RedirectToAction(nameof(Rapide));
        }

        // Caisse cible : celle choisie si valide et utilisable, sinon la session ouverte la plus récente de l'utilisateur.
        var caissesUtilisables = _db.Caisses.Where(c => c.TenantId == TenantId && c.Statut == StatutCaisse.Ouverte);
        if (EstCaissier) caissesUtilisables = caissesUtilisables.Where(c => c.CaissierId == UtilisateurId);
        var caisse = caisseSessionId > 0
            ? caissesUtilisables.FirstOrDefault(c => c.Id == caisseSessionId)
            : null;
        caisse ??= caissesUtilisables.OrderByDescending(c => c.DateOuverture).FirstOrDefault();
        if (caisse == null)
        {
            caisse = GetOrCreateCaisseRapide();
        }

        var lignes = new List<LigneVente>();
        for (int i = 0; i < produitIds.Length; i++)
        {
            if (produitIds[i] <= 0 || quantites[i] <= 0) continue;
            var produit = _db.Produits.FirstOrDefault(p => p.Id == produitIds[i] && p.TenantId == TenantId && p.Actif);
            if (produit == null) continue;
            if (produit.StockActuel < quantites[i])
            {
                TempData["Erreur"] = $"Stock insuffisant pour {produit.Nom}. Stock disponible : {produit.StockActuel}.";
                return RedirectToAction(nameof(Rapide));
            }
            lignes.Add(new LigneVente
            {
                ProduitId = produit.Id,
                ProduitNom = produit.Nom,
                Quantite = quantites[i],
                PrixUnitaire = produit.PrixVente,
                PrixAchatUnitaire = produit.PrixAchat // coût figé au moment de la vente (marge réelle)
            });
        }

        if (!lignes.Any())
        {
            TempData["Erreur"] = "Le panier est vide.";
            return RedirectToAction(nameof(Rapide));
        }

        var client = _db.Clients.FirstOrDefault(c => c.Id == clientId && c.TenantId == TenantId && c.Actif);
        var regleFidelite = _db.ReglesFidelite.FirstOrDefault(r => r.TenantId == TenantId && r.Actif);
        var totalBrut = lignes.Sum(l => l.Total);
        remise = Math.Max(0, Math.Min(remise, totalBrut));

        pointsFideliteUtilises = Math.Max(0, pointsFideliteUtilises);
        decimal remiseFidelite = 0;

        if (client != null && regleFidelite != null && pointsFideliteUtilises > 0)
        {
            if (pointsFideliteUtilises > client.PointsFidelite)
            {
                TempData["Erreur"] = "Le client n'a pas assez de points fidélité.";
                return RedirectToAction(nameof(Rapide));
            }
            if (pointsFideliteUtilises < regleFidelite.SeuilUtilisationPoints)
            {
                TempData["Erreur"] = $"Le seuil minimum d'utilisation est de {regleFidelite.SeuilUtilisationPoints} points.";
                return RedirectToAction(nameof(Rapide));
            }
            remiseFidelite = pointsFideliteUtilises * regleFidelite.ValeurPoint;
        }

        remiseFidelite = Math.Min(remiseFidelite, Math.Max(0, totalBrut - remise));
        var totalNet = totalBrut - remise - remiseFidelite;
        modePaiement = string.IsNullOrWhiteSpace(modePaiement) ? "Espèces" : modePaiement.Trim();
        var estCredit = string.Equals(modePaiement, "Crédit", StringComparison.OrdinalIgnoreCase);
        var estEspeces = string.Equals(modePaiement, "Espèces", StringComparison.OrdinalIgnoreCase);
        var exigeReference = string.Equals(modePaiement, "Mobile Money", StringComparison.OrdinalIgnoreCase)
            || string.Equals(modePaiement, "Carte bancaire", StringComparison.OrdinalIgnoreCase);
        referencePaiement = (referencePaiement ?? string.Empty).Trim();

        if (estEspeces && montantRecu < totalNet)
        {
            TempData["Erreur"] = "Le montant reçu est inférieur au total net.";
            return RedirectToAction(nameof(Rapide));
        }

        if (exigeReference && string.IsNullOrWhiteSpace(referencePaiement))
        {
            TempData["Erreur"] = "La référence de transaction est obligatoire pour Mobile Money et Carte bancaire.";
            return RedirectToAction(nameof(Rapide));
        }

        // Règle de gestion : une vente à crédit exige un client identifié et un plafond suffisant.
        if (estCredit)
        {
            if (client == null || client.Nom.Equals("Client comptoir", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Erreur"] = "Une vente à crédit exige un client identifié (pas de client comptoir).";
                return RedirectToAction(nameof(Rapide));
            }
            if (client.PlafondCredit > 0 && client.SoldeCredit + totalNet > client.PlafondCredit)
            {
                TempData["Erreur"] = $"Plafond de crédit dépassé pour {client.Nom}. Encours actuel : {client.SoldeCredit:N0} FCFA, plafond : {client.PlafondCredit:N0} FCFA.";
                return RedirectToAction(nameof(Rapide));
            }
            montantRecu = 0;
        }

        using var transaction = _db.Database.BeginTransaction();
        MouvementFidelite? mouvementGain = null;

        try
        {
            var dateVente = DateTime.Now;
            var vente = new Vente
            {
                TenantId = TenantId,
                DateVente = dateVente,
                NumeroTicket = $"T{TenantId}-{dateVente:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}"[..28],
                ClientId = client?.Id,
                ClientNom = client?.Nom ?? "Client comptoir",
                ModePaiement = modePaiement,
                ReferencePaiement = referencePaiement,
                Remise = remise,
                PointsFideliteUtilises = pointsFideliteUtilises,
                RemiseFidelite = remiseFidelite,
                MontantRecu = montantRecu,
                TotalBrut = totalBrut,
                Vendeur = UtilisateurNom,
                VendeurId = UtilisateurId,
                CaisseSessionId = caisse.Id,
                Statut = StatutVente.Validee,
                ClientUuid = clientUuid,
                Lignes = lignes
            };

            _db.Ventes.Add(vente);
            foreach (var ligne in lignes)
            {
                var produit = _db.Produits.First(p => p.Id == ligne.ProduitId && p.TenantId == TenantId);
                produit.StockActuel -= ligne.Quantite;
                _db.MouvementsStock.Add(new MouvementStock { TenantId = TenantId, DateMouvement = DateTime.Now, ProduitId = produit.Id, ProduitNom = produit.Nom, Type = TypeMouvementStock.Sortie, Quantite = ligne.Quantite, Motif = $"Vente {vente.NumeroTicket}", Utilisateur = vente.Vendeur });
            }

            // Règle du tiroir-caisse : seules les espèces entrent dans le tiroir.
            // Les autres modes (Mobile Money, carte...) sont suivis à part ; le crédit n'encaisse rien.
            if (estEspeces)
            {
                caisse.Encaissements += vente.Total;
            }
            else if (!estCredit)
            {
                caisse.EncaissementsAutres += vente.Total;
            }

            if (estCredit && client != null)
            {
                client.SoldeCredit += vente.Total;
            }

            if (client != null)
            {
                client.TotalAchats += vente.Total;
                client.DerniereVisite = vente.DateVente;

                if (regleFidelite != null)
                {
                    if (pointsFideliteUtilises > 0)
                    {
                        client.PointsFidelite -= pointsFideliteUtilises;
                        _db.MouvementsFidelite.Add(new MouvementFidelite
                        {
                            TenantId = TenantId,
                            ClientId = client.Id,
                            ClientNom = client.Nom,
                            DateMouvement = DateTime.Now,
                            Points = -pointsFideliteUtilises,
                            TypeMouvement = "Utilisation",
                            Commentaire = $"Remise fidélité sur {vente.NumeroTicket}",
                            Utilisateur = UtilisateurNom
                        });
                    }

                    var pointsGagnes = regleFidelite.MontantPourUnPoint <= 0 ? 0 : (int)Math.Floor(vente.Total / regleFidelite.MontantPourUnPoint);
                    vente.PointsFideliteGagnes = pointsGagnes;

                    if (pointsGagnes > 0)
                    {
                        client.PointsFidelite += pointsGagnes;
                        mouvementGain = new MouvementFidelite
                        {
                            TenantId = TenantId,
                            ClientId = client.Id,
                            ClientNom = client.Nom,
                            DateMouvement = DateTime.Now,
                            Points = pointsGagnes,
                            TypeMouvement = "Gain",
                            Commentaire = $"Gain sur {vente.NumeroTicket}",
                            Utilisateur = UtilisateurNom
                        };
                        _db.MouvementsFidelite.Add(mouvementGain);
                    }
                }
            }

            _db.SaveChanges();

            if (mouvementGain != null)
            {
                mouvementGain.VenteId = vente.Id;
                _db.SaveChanges();
            }

            transaction.Commit();
            TempData["Succes"] = $"Vente enregistrée. Ticket : {vente.NumeroTicket}.";
            return RedirectToAction(nameof(Details), new { id = vente.Id });
        }
        catch
        {
            transaction.Rollback();
            TempData["Erreur"] = "La vente n'a pas pu être enregistrée. Aucune modification n'a été validée.";
            return RedirectToAction(nameof(Rapide));
        }
    }

    // Création rapide d'un client depuis le POS (accessible aussi au caissier).
    // Renvoie du JSON pour ne pas perdre le panier en cours.
    [HttpPost]
    public IActionResult CreerClientRapide(string nom, string? telephone)
    {
        nom = (nom ?? string.Empty).Trim();
        if (nom.Length < 2)
            return Json(new { ok = false, message = "Nom trop court." });

        if (_db.Clients.Any(c => c.TenantId == TenantId && c.Nom.ToLower() == nom.ToLower()))
            return Json(new { ok = false, message = "Un client porte déjà ce nom." });

        var client = new Client
        {
            TenantId = TenantId,
            Nom = nom,
            Telephone = (telephone ?? string.Empty).Trim(),
            Actif = true
        };
        _db.Clients.Add(client);
        _db.SaveChanges();
        return Json(new { ok = true, id = client.Id, nom = client.Nom, points = client.PointsFidelite });
    }

    public IActionResult Details(int id)
    {
        var vente = _db.Ventes.Include(v => v.Lignes).FirstOrDefault(v => v.Id == id && v.TenantId == TenantId);
        if (vente == null) return NotFound();
        if (EstCaissier && vente.VendeurId != UtilisateurId) return RedirectToAction(nameof(Index));
        ViewBag.Tenant = _db.Tenants.FirstOrDefault(t => t.Id == TenantId);
        return View(vente);
    }

    /// <summary>
    /// Annulation d'une vente (Manager ou Administrateur uniquement) :
    /// pas de suppression physique (piste d'audit), restitution du stock,
    /// reprise des points fidélité, contre-passation en caisse et reprise du crédit client.
    /// </summary>
    [HttpPost]
    public IActionResult Annuler(int id, string motif)
    {
        if (string.IsNullOrWhiteSpace(motif))
        {
            TempData["Erreur"] = "Le motif d'annulation est obligatoire.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var vente = _db.Ventes.Include(v => v.Lignes).FirstOrDefault(v => v.Id == id && v.TenantId == TenantId);
        if (vente == null) return NotFound();

        if (vente.Statut == StatutVente.Annulee)
        {
            TempData["Erreur"] = "Cette vente est déjà annulée.";
            return RedirectToAction(nameof(Details), new { id });
        }

        using var transaction = _db.Database.BeginTransaction();
        try
        {
            // 1. Restitution du stock.
            foreach (var ligne in vente.Lignes)
            {
                var produit = _db.Produits.FirstOrDefault(p => p.Id == ligne.ProduitId && p.TenantId == TenantId);
                if (produit != null)
                {
                    produit.StockActuel += ligne.Quantite;
                    _db.MouvementsStock.Add(new MouvementStock
                    {
                        TenantId = TenantId,
                        DateMouvement = DateTime.Now,
                        ProduitId = produit.Id,
                        ProduitNom = produit.Nom,
                        Type = TypeMouvementStock.Entree,
                        Quantite = ligne.Quantite,
                        Motif = $"Annulation vente {vente.NumeroTicket}",
                        Utilisateur = UtilisateurNom
                    });
                }
            }

            // 2. Contre-passation en caisse : uniquement si la session d'origine est encore ouverte.
            var estEspeces = string.Equals(vente.ModePaiement, "Espèces", StringComparison.OrdinalIgnoreCase);
            var estCredit = string.Equals(vente.ModePaiement, "Crédit", StringComparison.OrdinalIgnoreCase);
            var caisseOrigine = vente.CaisseSessionId.HasValue
                ? _db.Caisses.FirstOrDefault(c => c.Id == vente.CaisseSessionId.Value && c.TenantId == TenantId && c.Statut == StatutCaisse.Ouverte)
                : null;

            if (caisseOrigine != null)
            {
                if (estEspeces) caisseOrigine.Encaissements = Math.Max(0, caisseOrigine.Encaissements - vente.Total);
                else if (!estCredit) caisseOrigine.EncaissementsAutres = Math.Max(0, caisseOrigine.EncaissementsAutres - vente.Total);
            }
            else if (!estCredit && vente.Total > 0)
            {
                // Session clôturée : le remboursement doit sortir de la caisse ouverte (décaissement tracé).
                var caisseOuverte = _db.Caisses.OrderByDescending(c => c.DateOuverture)
                    .FirstOrDefault(c => c.TenantId == TenantId && c.Statut == StatutCaisse.Ouverte);
                if (caisseOuverte == null)
                {
                    TempData["Erreur"] = "La session de caisse d'origine est clôturée. Ouvrez une caisse pour enregistrer le remboursement.";
                    transaction.Rollback();
                    return RedirectToAction(nameof(Details), new { id });
                }
                caisseOuverte.Decaissements += vente.Total;
                _db.MouvementsCaisse.Add(new MouvementCaisse
                {
                    TenantId = TenantId,
                    CaisseSessionId = caisseOuverte.Id,
                    Type = TypeMouvementCaisse.Retrait,
                    Montant = vente.Total,
                    Motif = $"Remboursement annulation {vente.NumeroTicket}",
                    Utilisateur = UtilisateurNom
                });
            }

            // 3. Reprise crédit et fidélité côté client.
            if (vente.ClientId.HasValue)
            {
                var client = _db.Clients.FirstOrDefault(c => c.Id == vente.ClientId.Value && c.TenantId == TenantId);
                if (client != null)
                {
                    if (estCredit) client.SoldeCredit = Math.Max(0, client.SoldeCredit - vente.Total);
                    client.TotalAchats = Math.Max(0, client.TotalAchats - vente.Total);

                    if (vente.PointsFideliteGagnes > 0)
                    {
                        client.PointsFidelite = Math.Max(0, client.PointsFidelite - vente.PointsFideliteGagnes);
                        _db.MouvementsFidelite.Add(new MouvementFidelite
                        {
                            TenantId = TenantId,
                            ClientId = client.Id,
                            ClientNom = client.Nom,
                            VenteId = vente.Id,
                            DateMouvement = DateTime.Now,
                            Points = -vente.PointsFideliteGagnes,
                            TypeMouvement = "Annulation",
                            Commentaire = $"Reprise points - annulation {vente.NumeroTicket}",
                            Utilisateur = UtilisateurNom
                        });
                    }

                    if (vente.PointsFideliteUtilises > 0)
                    {
                        client.PointsFidelite += vente.PointsFideliteUtilises;
                        _db.MouvementsFidelite.Add(new MouvementFidelite
                        {
                            TenantId = TenantId,
                            ClientId = client.Id,
                            ClientNom = client.Nom,
                            VenteId = vente.Id,
                            DateMouvement = DateTime.Now,
                            Points = vente.PointsFideliteUtilises,
                            TypeMouvement = "Annulation",
                            Commentaire = $"Restitution points utilisés - annulation {vente.NumeroTicket}",
                            Utilisateur = UtilisateurNom
                        });
                    }
                }
            }

            // 4. Marquage de la vente (audit).
            vente.Statut = StatutVente.Annulee;
            vente.DateAnnulation = DateTime.Now;
            vente.MotifAnnulation = motif.Trim();
            vente.AnnuleePar = UtilisateurNom;

            _db.SaveChanges();
            transaction.Commit();
            TempData["Succes"] = $"Vente {vente.NumeroTicket} annulée. Stock restitué et écritures contre-passées.";
        }
        catch
        {
            transaction.Rollback();
            TempData["Erreur"] = "L'annulation a échoué. Aucune modification n'a été validée.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }
}

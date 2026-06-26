using Application.ViewModels;
using Domaine;
using Domaine.Models;
using Infrastructure.Donnees;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Presentation.Filtres;

namespace Presentation.Controllers;

/// <summary>
/// Service restaurant : plan de salle, ouverture d'une table, addition par table,
/// écran cuisine KDS et encaissement.
/// </summary>
[AutorisationFiltre(1, 2, 3)]
public class SalleController : BaseController
{
    private readonly AppDbContext _db;

    public SalleController(AppDbContext db) => _db = db;

    public IActionResult Index()
    {
        var tables = _db.Tables.Where(t => t.TenantId == TenantId && t.Actif)
            .OrderBy(t => t.Zone).ThenBy(t => t.Ordre).ThenBy(t => t.Nom).ToList();

        var ouvertes = _db.Commandes
            .Include(c => c.Lignes)
            .Where(c => c.TenantId == TenantId && c.Statut == StatutCommande.Ouverte)
            .ToList();

        var etats = tables.Select(t => new TableEtatVm
        {
            Table = t,
            Commande = ouvertes.FirstOrDefault(c => c.TableId == t.Id)
        }).ToList();

        return View(etats);
    }

    // --- Gestion des tables (admin/manager) ---
    [HttpGet]
    public IActionResult Tables()
    {
        var tables = _db.Tables.Where(t => t.TenantId == TenantId)
            .OrderBy(t => t.Zone).ThenBy(t => t.Ordre).ThenBy(t => t.Nom).ToList();
        return View(tables);
    }

    [HttpPost]
    public IActionResult CreerTable(string nom, string? zone, int capacite, int ordre)
    {
        if (!PeutModifier("restaurant")) { TempData["Erreur"] = "Action non autorisée."; return RedirectToAction(nameof(Tables)); }
        nom = (nom ?? string.Empty).Trim();
        if (nom.Length == 0) { TempData["Erreur"] = "Le nom de la table est obligatoire."; return RedirectToAction(nameof(Tables)); }

        _db.Tables.Add(new TableResto
        {
            TenantId = TenantId,
            Nom = nom,
            Zone = string.IsNullOrWhiteSpace(zone) ? "Salle" : zone.Trim(),
            Capacite = capacite <= 0 ? 4 : capacite,
            Ordre = ordre,
            Actif = true
        });
        _db.SaveChanges();
        TempData["Succes"] = "Table ajoutée.";
        return RedirectToAction(nameof(Tables));
    }

    [HttpPost]
    public IActionResult SupprimerTable(int id)
    {
        if (!PeutModifier("restaurant")) { TempData["Erreur"] = "Action non autorisée."; return RedirectToAction(nameof(Tables)); }
        var table = _db.Tables.FirstOrDefault(t => t.Id == id && t.TenantId == TenantId);
        if (table != null)
        {
            var occupee = _db.Commandes.Any(c => c.TenantId == TenantId && c.TableId == id && c.Statut == StatutCommande.Ouverte);
            if (occupee) { TempData["Erreur"] = "Table occupée : clôturez l'addition avant de la retirer."; return RedirectToAction(nameof(Tables)); }
            table.Actif = false;
            _db.SaveChanges();
            TempData["Succes"] = "Table retirée.";
        }
        return RedirectToAction(nameof(Tables));
    }

    // --- Commandes (additions) ---
    [HttpPost]
    public IActionResult Ouvrir(int tableId, int couverts)
    {
        var table = _db.Tables.FirstOrDefault(t => t.Id == tableId && t.TenantId == TenantId && t.Actif);
        if (table == null) { TempData["Erreur"] = "Table introuvable."; return RedirectToAction(nameof(Index)); }

        var existante = _db.Commandes.FirstOrDefault(c => c.TenantId == TenantId && c.TableId == tableId && c.Statut == StatutCommande.Ouverte);
        if (existante != null) return RedirectToAction(nameof(Commande), new { id = existante.Id });

        var commande = new Commande
        {
            TenantId = TenantId,
            TableId = table.Id,
            TableNom = table.Nom,
            Numero = $"C{TenantId}-{DateTime.Now:yyyyMMddHHmmss}",
            Statut = StatutCommande.Ouverte,
            DateOuverture = DateTime.Now,
            OuvertePar = UtilisateurNom,
            Couverts = couverts <= 0 ? 1 : couverts
        };
        _db.Commandes.Add(commande);
        _db.SaveChanges();
        return RedirectToAction(nameof(Commande), new { id = commande.Id });
    }

    [HttpGet]
    public IActionResult Commande(int id)
    {
        var commande = _db.Commandes.Include(c => c.Lignes)
            .FirstOrDefault(c => c.Id == id && c.TenantId == TenantId);
        if (commande == null) return NotFound();

        ViewBag.Produits = _db.Produits.Where(p => p.TenantId == TenantId && p.Actif)
            .OrderBy(p => p.Categorie).ThenBy(p => p.Nom).ToList();
        ViewBag.Categories = _db.Produits.Where(p => p.TenantId == TenantId && p.Actif)
            .Select(p => p.Categorie).Distinct().OrderBy(c => c).ToList();
        return View(commande);
    }

    [HttpPost]
    public IActionResult AjouterLigne(int commandeId, int produitId, int quantite)
    {
        var commande = _db.Commandes.Include(c => c.Lignes)
            .FirstOrDefault(c => c.Id == commandeId && c.TenantId == TenantId && c.Statut == StatutCommande.Ouverte);
        if (commande == null) { TempData["Erreur"] = "Addition introuvable ou déjà clôturée."; return RedirectToAction(nameof(Index)); }

        var produit = _db.Produits.FirstOrDefault(p => p.Id == produitId && p.TenantId == TenantId && p.Actif);
        if (produit == null) { TempData["Erreur"] = "Produit introuvable."; return RedirectToAction(nameof(Commande), new { id = commandeId }); }
        if (produit.StockActuel <= 0) { TempData["Erreur"] = $"Stock épuisé pour {produit.Nom}."; return RedirectToAction(nameof(Commande), new { id = commandeId }); }

        var qte = quantite <= 0 ? 1 : quantite;
        var vaEnCuisine = EstProduitCuisine(produit);
        var statutInitial = vaEnCuisine ? StatutPreparation.EnCuisine : StatutPreparation.EnAttente;
        var ligne = commande.Lignes.FirstOrDefault(l => l.ProduitId == produitId && l.Preparation == statutInitial);

        if (ligne != null)
        {
            ligne.Quantite += qte;
        }
        else
        {
            commande.Lignes.Add(new LigneCommande
            {
                ProduitId = produit.Id,
                ProduitNom = produit.Nom,
                Quantite = qte,
                PrixUnitaire = produit.PrixVente,
                PrixAchatUnitaire = produit.PrixAchat,
                Preparation = statutInitial
            });
        }

        _db.SaveChanges();

        if (vaEnCuisine)
        {
            TempData["Succes"] = $"{produit.Nom} ajouté et envoyé automatiquement en cuisine KDS.";
        }

        return RedirectToAction(nameof(Commande), new { id = commandeId });
    }

    [HttpPost]
    public IActionResult ModifierQuantite(int ligneId, int quantite)
    {
        var ligne = _db.LignesCommande.FirstOrDefault(l => l.Id == ligneId);
        if (ligne == null) return RedirectToAction(nameof(Index));
        var commande = _db.Commandes.FirstOrDefault(c => c.Id == ligne.CommandeId && c.TenantId == TenantId && c.Statut == StatutCommande.Ouverte);
        if (commande == null) { TempData["Erreur"] = "Addition non modifiable."; return RedirectToAction(nameof(Index)); }
        if (ligne.Preparation != StatutPreparation.EnAttente)
        {
            TempData["Erreur"] = "Impossible de modifier la quantité : l'article a déjà été envoyé en cuisine.";
            return RedirectToAction(nameof(Commande), new { id = commande.Id });
        }

        if (quantite <= 0) _db.LignesCommande.Remove(ligne);
        else ligne.Quantite = quantite;
        _db.SaveChanges();
        return RedirectToAction(nameof(Commande), new { id = commande.Id });
    }

    [HttpPost]
    public IActionResult SupprimerLigne(int ligneId)
    {
        var ligne = _db.LignesCommande.FirstOrDefault(l => l.Id == ligneId);
        if (ligne == null) return RedirectToAction(nameof(Index));
        var commande = _db.Commandes.FirstOrDefault(c => c.Id == ligne.CommandeId && c.TenantId == TenantId && c.Statut == StatutCommande.Ouverte);
        if (commande == null) { TempData["Erreur"] = "Addition non modifiable."; return RedirectToAction(nameof(Index)); }
        if (ligne.Preparation != StatutPreparation.EnAttente)
        {
            TempData["Erreur"] = "Impossible de supprimer l'article : il a déjà été envoyé en cuisine.";
            return RedirectToAction(nameof(Commande), new { id = commande.Id });
        }

        _db.LignesCommande.Remove(ligne);
        _db.SaveChanges();
        return RedirectToAction(nameof(Commande), new { id = commande.Id });
    }

    [HttpPost]
    public IActionResult Annuler(int commandeId)
    {
        var commande = _db.Commandes.FirstOrDefault(c => c.Id == commandeId && c.TenantId == TenantId && c.Statut == StatutCommande.Ouverte);
        if (commande != null)
        {
            commande.Statut = StatutCommande.Annulee;
            commande.DateCloture = DateTime.Now;
            _db.SaveChanges();
            TempData["Succes"] = "Addition annulée, table libérée.";
        }
        return RedirectToAction(nameof(Index));
    }

    // --- Encaissement : transforme l'addition en vente, décrémente le stock,
    //     alimente la caisse, puis libère la table. ---
    [HttpPost]
    public IActionResult Encaisser(int commandeId, string modePaiement, string? referencePaiement, decimal montantRecu)
    {
        var commande = _db.Commandes.Include(c => c.Lignes)
            .FirstOrDefault(c => c.Id == commandeId && c.TenantId == TenantId && c.Statut == StatutCommande.Ouverte);
        if (commande == null) { TempData["Erreur"] = "Addition introuvable ou déjà clôturée."; return RedirectToAction(nameof(Index)); }
        if (!commande.Lignes.Any()) { TempData["Erreur"] = "Addition vide."; return RedirectToAction(nameof(Commande), new { id = commandeId }); }

        modePaiement = string.IsNullOrWhiteSpace(modePaiement) ? "Espèces" : modePaiement.Trim();
        referencePaiement = (referencePaiement ?? string.Empty).Trim();
        var estEspeces = string.Equals(modePaiement, "Espèces", StringComparison.OrdinalIgnoreCase);

        foreach (var l in commande.Lignes)
        {
            var prod = _db.Produits.FirstOrDefault(p => p.Id == l.ProduitId && p.TenantId == TenantId);
            if (prod != null && prod.StockActuel < l.Quantite)
            {
                TempData["Erreur"] = $"Stock insuffisant pour {l.ProduitNom} (disponible : {prod.StockActuel}).";
                return RedirectToAction(nameof(Commande), new { id = commandeId });
            }
        }

        var total = commande.Total;
        if (estEspeces && montantRecu < total)
        {
            TempData["Erreur"] = "Le montant reçu est inférieur au total.";
            return RedirectToAction(nameof(Commande), new { id = commandeId });
        }

        var caisse = GetOrCreateCaisse();
        using var tx = _db.Database.BeginTransaction();
        try
        {
            var date = DateTime.Now;
            var vente = new Vente
            {
                TenantId = TenantId,
                DateVente = date,
                NumeroTicket = $"T{TenantId}-{date:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}"[..28],
                ClientNom = string.IsNullOrWhiteSpace(commande.ClientNom) ? "Client comptoir" : commande.ClientNom,
                ModePaiement = modePaiement,
                ReferencePaiement = referencePaiement,
                MontantRecu = estEspeces ? montantRecu : total,
                TotalBrut = total,
                Vendeur = UtilisateurNom,
                VendeurId = UtilisateurId,
                CaisseSessionId = caisse.Id,
                Statut = StatutVente.Validee,
                Lignes = commande.Lignes.Select(l => new LigneVente
                {
                    ProduitId = l.ProduitId,
                    ProduitNom = l.ProduitNom,
                    Quantite = l.Quantite,
                    PrixUnitaire = l.PrixUnitaire,
                    PrixAchatUnitaire = l.PrixAchatUnitaire
                }).ToList()
            };
            _db.Ventes.Add(vente);

            foreach (var l in commande.Lignes)
            {
                var prod = _db.Produits.First(p => p.Id == l.ProduitId && p.TenantId == TenantId);
                prod.StockActuel -= l.Quantite;
                _db.MouvementsStock.Add(new MouvementStock
                {
                    TenantId = TenantId,
                    DateMouvement = date,
                    ProduitId = prod.Id,
                    ProduitNom = prod.Nom,
                    Type = TypeMouvementStock.Sortie,
                    Quantite = l.Quantite,
                    Motif = $"Vente {vente.NumeroTicket} (table {commande.TableNom})",
                    Utilisateur = UtilisateurNom
                });
            }

            if (estEspeces) caisse.Encaissements += total;
            else caisse.EncaissementsAutres += total;

            commande.Statut = StatutCommande.Encaissee;
            commande.DateCloture = date;
            _db.SaveChanges();

            commande.VenteId = vente.Id;
            _db.SaveChanges();
            tx.Commit();

            TempData["Succes"] = $"Addition encaissée. Ticket : {vente.NumeroTicket}.";
            return RedirectToAction("Details", "Vente", new { id = vente.Id });
        }
        catch
        {
            tx.Rollback();
            TempData["Erreur"] = "L'encaissement a échoué. Aucune modification n'a été validée.";
            return RedirectToAction(nameof(Commande), new { id = commandeId });
        }
    }

    // --- Envoi en cuisine + écran KDS ---
    [HttpPost]
    public IActionResult EnvoyerCuisine(int commandeId)
    {
        var commande = _db.Commandes.Include(c => c.Lignes)
            .FirstOrDefault(c => c.Id == commandeId && c.TenantId == TenantId && c.Statut == StatutCommande.Ouverte);
        if (commande == null) { TempData["Erreur"] = "Addition introuvable."; return RedirectToAction(nameof(Index)); }

        var aEnvoyer = commande.Lignes.Where(l => l.Preparation == StatutPreparation.EnAttente).ToList();
        if (aEnvoyer.Count == 0) { TempData["Erreur"] = "Aucun nouvel article à envoyer."; return RedirectToAction(nameof(Commande), new { id = commandeId }); }

        foreach (var l in aEnvoyer) l.Preparation = StatutPreparation.EnCuisine;
        _db.SaveChanges();
        TempData["Succes"] = $"{aEnvoyer.Count} article(s) envoyé(s) en cuisine.";
        return RedirectToAction(nameof(Commande), new { id = commandeId });
    }

    [HttpGet]
    public IActionResult Cuisine(string statut = "actifs")
    {
        statut = string.IsNullOrWhiteSpace(statut) ? "actifs" : statut.Trim().ToLowerInvariant();

        var commandes = _db.Commandes.Include(c => c.Lignes)
            .Where(c => c.TenantId == TenantId && c.Statut == StatutCommande.Ouverte)
            .OrderBy(c => c.DateOuverture)
            .ToList();

        var visibles = statut switch
        {
            "preparation" => commandes.Where(c => c.Lignes.Any(l => l.Preparation == StatutPreparation.EnCuisine)),
            "pretes" => commandes.Where(c => c.Lignes.Any(l => l.Preparation == StatutPreparation.Prete)),
            "servies" => commandes.Where(c => c.Lignes.Any(l => l.Preparation == StatutPreparation.Servie)),
            "toutes" => commandes.Where(c => c.Lignes.Any(l => l.Preparation != StatutPreparation.EnAttente)),
            _ => commandes.Where(c => c.Lignes.Any(l => l.Preparation == StatutPreparation.EnCuisine || l.Preparation == StatutPreparation.Prete))
        };

        ViewBag.StatutFiltre = statut;
        ViewBag.TotalPreparation = commandes.Sum(c => c.Lignes.Where(l => l.Preparation == StatutPreparation.EnCuisine).Sum(l => l.Quantite));
        ViewBag.TotalPretes = commandes.Sum(c => c.Lignes.Where(l => l.Preparation == StatutPreparation.Prete).Sum(l => l.Quantite));
        ViewBag.TotalServies = commandes.Sum(c => c.Lignes.Where(l => l.Preparation == StatutPreparation.Servie).Sum(l => l.Quantite));
        ViewBag.CommandesActives = commandes.Count(c => c.Lignes.Any(l => l.Preparation == StatutPreparation.EnCuisine || l.Preparation == StatutPreparation.Prete));

        return View(visibles.ToList());
    }

    [HttpPost]
    public IActionResult MarquerLigne(int ligneId, int statut, string? retour)
    {
        var ligne = _db.LignesCommande.FirstOrDefault(l => l.Id == ligneId);
        if (ligne == null) return RedirectToAction(nameof(Cuisine));

        var commande = _db.Commandes.FirstOrDefault(c => c.Id == ligne.CommandeId && c.TenantId == TenantId && c.Statut == StatutCommande.Ouverte);
        if (commande == null) return RedirectToAction(nameof(Cuisine));

        if (statut < (int)StatutPreparation.EnCuisine || statut > (int)StatutPreparation.Servie)
        {
            TempData["Erreur"] = "Statut de préparation invalide.";
            return RedirectRetourKds(retour, commande.Id);
        }

        ligne.Preparation = (StatutPreparation)statut;
        _db.SaveChanges();
        return RedirectRetourKds(retour, commande.Id);
    }

    [HttpPost]
    public IActionResult MarquerCommandePrete(int commandeId)
    {
        var commande = _db.Commandes.Include(c => c.Lignes)
            .FirstOrDefault(c => c.Id == commandeId && c.TenantId == TenantId && c.Statut == StatutCommande.Ouverte);
        if (commande == null) return RedirectToAction(nameof(Cuisine));

        var lignes = commande.Lignes.Where(l => l.Preparation == StatutPreparation.EnCuisine).ToList();
        foreach (var ligne in lignes) ligne.Preparation = StatutPreparation.Prete;
        if (lignes.Count > 0) _db.SaveChanges();

        TempData["Succes"] = lignes.Count == 0
            ? "Aucune ligne en préparation à marquer prête."
            : $"Table {commande.TableNom} : {lignes.Count} ligne(s) marquée(s) prête(s).";
        return RedirectToAction(nameof(Cuisine));
    }

    [HttpPost]
    public IActionResult MarquerCommandeServie(int commandeId)
    {
        var commande = _db.Commandes.Include(c => c.Lignes)
            .FirstOrDefault(c => c.Id == commandeId && c.TenantId == TenantId && c.Statut == StatutCommande.Ouverte);
        if (commande == null) return RedirectToAction(nameof(Cuisine));

        var lignes = commande.Lignes.Where(l => l.Preparation == StatutPreparation.Prete).ToList();
        foreach (var ligne in lignes) ligne.Preparation = StatutPreparation.Servie;
        if (lignes.Count > 0) _db.SaveChanges();

        TempData["Succes"] = lignes.Count == 0
            ? "Aucune ligne prête à marquer servie."
            : $"Table {commande.TableNom} : {lignes.Count} ligne(s) marquée(s) servie(s).";
        return RedirectToAction(nameof(Cuisine));
    }

    private IActionResult RedirectRetourKds(string? retour, int commandeId)
    {
        return string.Equals(retour, "commande", StringComparison.OrdinalIgnoreCase)
            ? RedirectToAction(nameof(Commande), new { id = commandeId })
            : RedirectToAction(nameof(Cuisine));
    }

    private CaisseSession GetOrCreateCaisse()
    {
        var caisse = _db.Caisses
            .Where(c => c.TenantId == TenantId && c.Statut == StatutCaisse.Ouverte && c.CaissierId == UtilisateurId)
            .OrderByDescending(c => c.DateOuverture)
            .FirstOrDefault();
        if (caisse != null) return caisse;

        caisse = new CaisseSession
        {
            TenantId = TenantId,
            DateOuverture = DateTime.Now,
            MontantOuverture = 0,
            Libelle = "Caisse service restaurant",
            Caissier = UtilisateurNom,
            CaissierId = UtilisateurId,
            Statut = StatutCaisse.Ouverte
        };
        _db.Caisses.Add(caisse);
        _db.SaveChanges();
        return caisse;
    }

    private static bool EstProduitCuisine(Produit produit)
    {
        var texte = $"{produit.Categorie} {produit.Nom}".ToLowerInvariant();
        string[] motsClesCuisine =
        {
            "snack", "accompagnement", "plat", "menu", "repas", "cuisine", "nourriture",
            "grillade", "grillé", "grille", "braisé", "braise", "poulet", "poisson",
            "brochette", "frites", "alloco", "attiéké", "attieke", "arachide", "chips", "pop-corn"
        };

        return motsClesCuisine.Any(texte.Contains);
    }
}

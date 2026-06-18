using Domaine;
using Domaine.Models;
using Infrastructure.Donnees;
using Microsoft.AspNetCore.Mvc;
using Presentation.Filtres;

namespace Presentation.Controllers;

[AutorisationFiltre]
public class CaisseController : BaseController
{
    private readonly AppDbContext _db;
    public CaisseController(AppDbContext db) { _db = db; }

    // Sessions ouvertes visibles par l'utilisateur :
    // le caissier voit les siennes ; admin/manager voient toutes celles du tenant.
    private IQueryable<CaisseSession> SessionsOuvertesVisibles()
    {
        var q = _db.Caisses.Where(c => c.TenantId == TenantId && c.Statut == StatutCaisse.Ouverte);
        if (EstCaissier) q = q.Where(c => c.CaissierId == UtilisateurId);
        return q;
    }

    // Une session précise, si l'utilisateur a le droit de l'opérer.
    private CaisseSession? SessionOperable(int caisseId)
    {
        var c = _db.Caisses.FirstOrDefault(x => x.Id == caisseId && x.TenantId == TenantId && x.Statut == StatutCaisse.Ouverte);
        if (c == null) return null;
        if (EstCaissier && c.CaissierId != UtilisateurId) return null;
        return c;
    }

    public IActionResult Index()
    {
        var ouvertes = SessionsOuvertesVisibles().OrderByDescending(c => c.DateOuverture).ToList();
        ViewBag.CaissesOuvertes = ouvertes;

        var ids = ouvertes.Select(c => c.Id).ToList();

        // Mouvements et ventilation par session (pour affichage groupé).
        ViewBag.MouvementsParCaisse = _db.MouvementsCaisse
            .Where(m => m.TenantId == TenantId && ids.Contains(m.CaisseSessionId))
            .OrderByDescending(m => m.DateMouvement)
            .ToList()
            .GroupBy(m => m.CaisseSessionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        ViewBag.VentesParModeParCaisse = _db.Ventes
            .Where(v => v.TenantId == TenantId && v.CaisseSessionId != null && ids.Contains(v.CaisseSessionId.Value) && v.Statut == StatutVente.Validee)
            .AsEnumerable()
            .GroupBy(v => v.CaisseSessionId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(v => v.ModePaiement)
                      .Select(x => new { Mode = x.Key, Nombre = x.Count(), Total = x.Sum(v => v.Total) })
                      .OrderByDescending(x => x.Total).ToList<object>());

        var historique = _db.Caisses.Where(c => c.TenantId == TenantId && c.Statut == StatutCaisse.Fermee);
        if (EstCaissier) historique = historique.Where(c => c.CaissierId == UtilisateurId);
        return View(historique.OrderByDescending(c => c.DateOuverture).Take(50).ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Ouvrir(decimal montantOuverture, string? libelle)
    {
        if (montantOuverture < 0)
        {
            TempData["Erreur"] = "Le montant d'ouverture ne peut pas être négatif.";
            return RedirectToAction(nameof(Index));
        }

        libelle = string.IsNullOrWhiteSpace(libelle) ? "Caisse" : libelle.Trim();

        // Plusieurs caisses peuvent être ouvertes simultanément ; on évite seulement
        // qu'un même utilisateur ouvre deux caisses portant exactement le même libellé.
        if (_db.Caisses.Any(c => c.TenantId == TenantId && c.Statut == StatutCaisse.Ouverte
                                  && c.CaissierId == UtilisateurId && c.Libelle.ToLower() == libelle.ToLower()))
        {
            TempData["Erreur"] = $"Vous avez déjà une caisse « {libelle} » ouverte. Donnez un autre nom.";
            return RedirectToAction(nameof(Index));
        }

        _db.Caisses.Add(new CaisseSession
        {
            TenantId = TenantId,
            DateOuverture = DateTime.Now,
            MontantOuverture = montantOuverture,
            Libelle = libelle,
            Caissier = UtilisateurNom,
            CaissierId = UtilisateurId,
            Statut = StatutCaisse.Ouverte
        });
        _db.SaveChanges();
        TempData["Succes"] = $"Caisse « {libelle} » ouverte.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Mouvement(int caisseId, int type, decimal montant, string motif)
    {
        if (montant <= 0)
        {
            TempData["Erreur"] = "Le montant doit être supérieur à 0.";
            return RedirectToAction(nameof(Index));
        }
        if (string.IsNullOrWhiteSpace(motif))
        {
            TempData["Erreur"] = "Le motif du mouvement est obligatoire.";
            return RedirectToAction(nameof(Index));
        }

        var caisse = SessionOperable(caisseId);
        if (caisse == null)
        {
            TempData["Erreur"] = "Caisse introuvable ou non autorisée.";
            return RedirectToAction(nameof(Index));
        }

        var typeMouvement = type == (int)TypeMouvementCaisse.Retrait ? TypeMouvementCaisse.Retrait : TypeMouvementCaisse.Apport;

        if (typeMouvement == TypeMouvementCaisse.Retrait && montant > caisse.SoldeTheorique)
        {
            TempData["Erreur"] = $"Retrait impossible : le solde théorique du tiroir est de {caisse.SoldeTheorique:N0} FCFA.";
            return RedirectToAction(nameof(Index));
        }

        if (typeMouvement == TypeMouvementCaisse.Apport) caisse.Encaissements += montant;
        else caisse.Decaissements += montant;

        _db.MouvementsCaisse.Add(new MouvementCaisse
        {
            TenantId = TenantId,
            CaisseSessionId = caisse.Id,
            Type = typeMouvement,
            Montant = montant,
            Motif = motif.Trim(),
            Utilisateur = UtilisateurNom
        });
        _db.SaveChanges();
        TempData["Succes"] = typeMouvement == TypeMouvementCaisse.Apport ? "Apport enregistré." : "Retrait enregistré.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Fermer(int caisseId, decimal montantCloture, string? commentaireCloture)
    {
        if (montantCloture < 0)
        {
            TempData["Erreur"] = "Le montant de clôture ne peut pas être négatif.";
            return RedirectToAction(nameof(Index));
        }

        var caisse = SessionOperable(caisseId);
        if (caisse == null)
        {
            TempData["Erreur"] = "Caisse introuvable ou non autorisée.";
            return RedirectToAction(nameof(Index));
        }

        var ecart = montantCloture - caisse.SoldeTheorique;
        if (ecart != 0 && string.IsNullOrWhiteSpace(commentaireCloture))
        {
            TempData["Erreur"] = $"Écart de {ecart:N0} FCFA détecté : une justification écrite est obligatoire pour clôturer.";
            return RedirectToAction(nameof(Index));
        }

        caisse.MontantCloture = montantCloture;
        caisse.DateFermeture = DateTime.Now;
        caisse.Statut = StatutCaisse.Fermee;
        caisse.ClotureePar = UtilisateurNom;
        caisse.CommentaireCloture = (commentaireCloture ?? string.Empty).Trim();
        _db.SaveChanges();
        TempData["Succes"] = ecart == 0 ? "Caisse clôturée. Aucun écart." : $"Caisse clôturée. Écart de {ecart:N0} FCFA justifié.";
        return RedirectToAction(nameof(Index));
    }
}

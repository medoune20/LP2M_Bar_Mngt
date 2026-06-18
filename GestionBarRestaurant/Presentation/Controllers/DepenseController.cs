using Domaine.Models;
using Infrastructure.Donnees;
using Microsoft.AspNetCore.Mvc;
using Presentation.Filtres;

namespace Presentation.Controllers;

[AutorisationFiltre]
public class DepenseController : BaseController
{
    private readonly AppDbContext _db;
    public DepenseController(AppDbContext db) { _db = db; }

    public IActionResult Index() => View(_db.Depenses.Where(d => d.TenantId == TenantId).OrderByDescending(d => d.DateDepense).ToList());

    [HttpGet]
    public IActionResult Nouvelle() => View(new Depense { TenantId = TenantId, DateDepense = DateTime.Now });

    [HttpPost]
    public IActionResult Nouvelle(Depense depense)
    {
        if (!ModelState.IsValid) return View(depense);
        depense.TenantId = TenantId;
        depense.SaisiPar = UtilisateurNom;
        var caisse = CaisseOuverteCourante();
        if (caisse != null && depense.DateDepense >= caisse.DateOuverture)
        {
            caisse.Decaissements += depense.Montant;
            depense.CaisseSessionId = caisse.Id;
        }
        _db.Depenses.Add(depense);
        _db.SaveChanges();
        TempData["Succes"] = "Dépense enregistrée.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Modifier(int id)
    {
        var depense = _db.Depenses.FirstOrDefault(d => d.Id == id && d.TenantId == TenantId);
        return depense == null ? NotFound() : View(depense);
    }

    [HttpPost]
    public IActionResult Modifier(Depense depense)
    {
        if (!ModelState.IsValid) return View(depense);
        var existante = _db.Depenses.FirstOrDefault(d => d.Id == depense.Id && d.TenantId == TenantId);
        if (existante == null) return NotFound();

        // Règle de gestion : une dépense rattachée à une session de caisse clôturée est verrouillée.
        if (SessionCloturee(existante))
        {
            TempData["Erreur"] = "Cette dépense est rattachée à une session de caisse clôturée : elle ne peut plus être modifiée.";
            return RedirectToAction(nameof(Index));
        }

        var ancienneDate = existante.DateDepense;
        var ancienMontant = existante.Montant;

        existante.Libelle = depense.Libelle;
        existante.Categorie = depense.Categorie;
        existante.Montant = depense.Montant;
        existante.DateDepense = depense.DateDepense;
        existante.Beneficiaire = depense.Beneficiaire;
        existante.Commentaire = depense.Commentaire;

        var caisse = CaisseOuverteCourante();
        if (caisse != null)
        {
            var ancienneDansCaisse = ancienneDate >= caisse.DateOuverture;
            var nouvelleDansCaisse = existante.DateDepense >= caisse.DateOuverture;
            if (ancienneDansCaisse) caisse.Decaissements -= ancienMontant;
            if (nouvelleDansCaisse) caisse.Decaissements += existante.Montant;
            if (caisse.Decaissements < 0) caisse.Decaissements = 0;
        }

        _db.SaveChanges();
        TempData["Succes"] = "Dépense modifiée.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult Supprimer(int id)
    {
        var depense = _db.Depenses.FirstOrDefault(d => d.Id == id && d.TenantId == TenantId);
        if (depense != null)
        {
            if (SessionCloturee(depense))
            {
                TempData["Erreur"] = "Cette dépense est rattachée à une session de caisse clôturée : elle ne peut plus être supprimée.";
                return RedirectToAction(nameof(Index));
            }

            var caisse = CaisseOuverteCourante();
            if (caisse != null && depense.DateDepense >= caisse.DateOuverture)
            {
                caisse.Decaissements -= depense.Montant;
                if (caisse.Decaissements < 0) caisse.Decaissements = 0;
            }

            _db.Depenses.Remove(depense);
            _db.SaveChanges();
            TempData["Succes"] = "Dépense supprimée.";
        }
        return RedirectToAction(nameof(Index));
    }

    private bool SessionCloturee(Depense depense)
    {
        if (!depense.CaisseSessionId.HasValue) return false;
        return _db.Caisses.Any(c => c.Id == depense.CaisseSessionId.Value && c.Statut == Domaine.StatutCaisse.Fermee);
    }

    private Domaine.Models.CaisseSession? CaisseOuverteCourante()
    {
        return _db.Caisses
            .OrderByDescending(c => c.DateOuverture)
            .FirstOrDefault(c => c.TenantId == TenantId && c.Statut == Domaine.StatutCaisse.Ouverte);
    }
}

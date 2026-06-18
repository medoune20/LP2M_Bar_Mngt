using Domaine.Models;
using Infrastructure.Donnees;
using Microsoft.AspNetCore.Mvc;
using Presentation.Filtres;

namespace Presentation.Controllers;

[AutorisationFiltre]
public class CategorieController : BaseController
{
    private readonly AppDbContext _db;
    public CategorieController(AppDbContext db) { _db = db; }

    public IActionResult Index()
    {
        // À la première ouverture, amorcer les catégories à partir des produits existants.
        if (!_db.Categories.Any(c => c.TenantId == TenantId))
        {
            var existantes = _db.Produits
                .Where(p => p.TenantId == TenantId && p.Categorie != null && p.Categorie != "")
                .Select(p => p.Categorie).Distinct().ToList();
            int ordre = 0;
            foreach (var nom in existantes)
                _db.Categories.Add(new Categorie { TenantId = TenantId, Nom = nom, Ordre = ordre++ });
            if (existantes.Any()) _db.SaveChanges();
        }

        var categories = _db.Categories
            .Where(c => c.TenantId == TenantId)
            .OrderBy(c => c.Ordre).ThenBy(c => c.Nom)
            .ToList();

        // Nombre de produits par catégorie (par nom), utile à l'affichage.
        ViewBag.Comptes = _db.Produits
            .Where(p => p.TenantId == TenantId && p.Actif)
            .GroupBy(p => p.Categorie)
            .ToDictionary(g => g.Key ?? string.Empty, g => g.Count());

        return View(categories);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Creer(Categorie categorie)
    {
        categorie.Nom = (categorie.Nom ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(categorie.Nom))
        {
            TempData["Erreur"] = "Le nom de la catégorie est obligatoire.";
            return RedirectToAction(nameof(Index));
        }
        if (_db.Categories.Any(c => c.TenantId == TenantId && c.Nom.ToLower() == categorie.Nom.ToLower()))
        {
            TempData["Erreur"] = "Cette catégorie existe déjà.";
            return RedirectToAction(nameof(Index));
        }
        categorie.TenantId = TenantId;
        categorie.Couleur = string.IsNullOrWhiteSpace(categorie.Couleur) ? "#165DFF" : categorie.Couleur;
        _db.Categories.Add(categorie);
        _db.SaveChanges();
        TempData["Succes"] = "Catégorie ajoutée.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Modifier(Categorie categorie)
    {
        var existante = _db.Categories.FirstOrDefault(c => c.Id == categorie.Id && c.TenantId == TenantId);
        if (existante == null) return NotFound();

        var ancienNom = existante.Nom;
        var nouveauNom = (categorie.Nom ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(nouveauNom))
        {
            TempData["Erreur"] = "Le nom de la catégorie est obligatoire.";
            return RedirectToAction(nameof(Index));
        }

        existante.Nom = nouveauNom;
        existante.Ordre = categorie.Ordre;
        existante.Couleur = string.IsNullOrWhiteSpace(categorie.Couleur) ? existante.Couleur : categorie.Couleur;
        existante.Actif = categorie.Actif;

        // Répercuter le renommage sur les produits liés (catégorie stockée par nom).
        if (!string.Equals(ancienNom, nouveauNom, StringComparison.Ordinal))
        {
            foreach (var p in _db.Produits.Where(p => p.TenantId == TenantId && p.Categorie == ancienNom))
                p.Categorie = nouveauNom;
        }

        _db.SaveChanges();
        TempData["Succes"] = "Catégorie mise à jour.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Supprimer(int id)
    {
        var cat = _db.Categories.FirstOrDefault(c => c.Id == id && c.TenantId == TenantId);
        if (cat == null) return NotFound();

        var nbProduits = _db.Produits.Count(p => p.TenantId == TenantId && p.Categorie == cat.Nom && p.Actif);
        if (nbProduits > 0)
        {
            TempData["Erreur"] = $"Impossible de supprimer : {nbProduits} produit(s) utilisent cette catégorie. Réaffectez-les d'abord.";
            return RedirectToAction(nameof(Index));
        }
        _db.Categories.Remove(cat);
        _db.SaveChanges();
        TempData["Succes"] = "Catégorie supprimée.";
        return RedirectToAction(nameof(Index));
    }
}

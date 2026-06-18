using Domaine;
using Domaine.Models;
using Infrastructure.Donnees;
using Microsoft.AspNetCore.Mvc;
using Presentation.Filtres;

namespace Presentation.Controllers;

[AutorisationFiltre]
public class StockController : BaseController
{
    private readonly AppDbContext _db;
    public StockController(AppDbContext db) { _db = db; }

    public IActionResult Index()
    {
        ViewBag.Produits = _db.Produits.Where(p => p.TenantId == TenantId && p.Actif).OrderBy(p => p.Nom).ToList();
        ViewBag.Alertes = _db.Produits.Where(p => p.TenantId == TenantId && p.Actif && p.StockActuel <= p.StockMinimum).OrderBy(p => p.StockActuel).ToList();
        return View(_db.MouvementsStock.Where(m => m.TenantId == TenantId).OrderByDescending(m => m.DateMouvement).Take(100).ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Ajuster(int produitId, TypeMouvementStock type, int quantite, string motif, decimal? coutUnitaire)
    {
        var produit = _db.Produits.FirstOrDefault(p => p.Id == produitId && p.TenantId == TenantId && p.Actif);
        if (produit == null)
        {
            TempData["Erreur"] = "Produit introuvable.";
            return RedirectToAction(nameof(Index));
        }
        if (type == TypeMouvementStock.Ajustement)
        {
            if (quantite < 0)
            {
                TempData["Erreur"] = "Le stock final ne peut pas être négatif.";
                return RedirectToAction(nameof(Index));
            }
        }
        else if (quantite <= 0)
        {
            TempData["Erreur"] = "La quantité doit être supérieure à 0.";
            return RedirectToAction(nameof(Index));
        }

        if (type == TypeMouvementStock.Sortie && produit.StockActuel < quantite)
        {
            TempData["Erreur"] = "Stock insuffisant pour cette sortie.";
            return RedirectToAction(nameof(Index));
        }
        if (type == TypeMouvementStock.Entree)
        {
            // Valorisation au Coût Moyen Unitaire Pondéré (CMUP) :
            // si un coût d'achat est saisi à l'entrée, le prix d'achat du produit est recalculé.
            if (coutUnitaire.HasValue && coutUnitaire.Value > 0)
            {
                var stockAvant = Math.Max(0, produit.StockActuel);
                var valeurAvant = stockAvant * produit.PrixAchat;
                var valeurEntree = quantite * coutUnitaire.Value;
                produit.PrixAchat = Math.Round((valeurAvant + valeurEntree) / (stockAvant + quantite), 2);
            }
            produit.StockActuel += quantite;
        }
        else if (type == TypeMouvementStock.Sortie) produit.StockActuel -= quantite;
        else produit.StockActuel = quantite;
        _db.MouvementsStock.Add(new MouvementStock { TenantId = TenantId, DateMouvement = DateTime.Now, ProduitId = produit.Id, ProduitNom = produit.Nom, Type = type, Quantite = quantite, Motif = string.IsNullOrWhiteSpace(motif) ? "Ajustement manuel" : motif, CoutUnitaire = type == TypeMouvementStock.Entree ? coutUnitaire : null, Utilisateur = UtilisateurNom });
        _db.SaveChanges();
        TempData["Succes"] = "Mouvement de stock enregistré.";
        return RedirectToAction(nameof(Index));
    }
}

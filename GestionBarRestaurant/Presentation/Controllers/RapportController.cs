using Application.ViewModels;
using Domaine;
using Infrastructure.Donnees;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Presentation.Filtres;

namespace Presentation.Controllers;

[AutorisationFiltre]
public class RapportController : BaseController
{
    private readonly AppDbContext _db;
    public RapportController(AppDbContext db) { _db = db; }

    public IActionResult Index(DateTime? debut, DateTime? fin)
    {
        var dateDebut = debut?.Date ?? DateTime.Today.AddDays(-30);
        var dateFin = fin?.Date.AddDays(1).AddTicks(-1) ?? DateTime.Today.AddDays(1).AddTicks(-1);
        var ventes = _db.Ventes.Include(v => v.Lignes).Where(v => v.TenantId == TenantId && v.Statut == StatutVente.Validee && v.DateVente >= dateDebut && v.DateVente <= dateFin).ToList();
        var depenses = _db.Depenses.Where(d => d.TenantId == TenantId && d.DateDepense >= dateDebut && d.DateDepense <= dateFin).ToList();
        var produits = _db.Produits.Where(p => p.TenantId == TenantId).ToList();

        ViewBag.Debut = dateDebut.ToString("yyyy-MM-dd");
        ViewBag.Fin = dateFin.ToString("yyyy-MM-dd");
        ViewBag.TotalVentes = ventes.Sum(v => v.Total);
        ViewBag.TotalDepenses = depenses.Sum(d => d.Montant);
        ViewBag.NombreTickets = ventes.Count;
        ViewBag.PanierMoyen = ventes.Any() ? ventes.Average(v => v.Total) : 0;
        // Marge réelle : utilise le coût figé sur la ligne ; repli sur le prix d'achat
        // produit pour les ventes antérieures à la mise à jour (coût ligne = 0).
        ViewBag.MargeEstimee = ventes.Sum(v => v.Lignes.Sum(l =>
        {
            var cout = l.PrixAchatUnitaire;
            if (cout <= 0)
            {
                var produit = produits.FirstOrDefault(p => p.Id == l.ProduitId);
                cout = produit?.PrixAchat ?? 0;
            }
            return (l.PrixUnitaire - cout) * l.Quantite;
        }));
        ViewBag.TopProduits = ventes.SelectMany(v => v.Lignes).GroupBy(l => l.ProduitNom).Select(g => new TopProduitVm { Produit = g.Key, Quantite = g.Sum(x => x.Quantite), Total = g.Sum(x => x.Total) }).OrderByDescending(x => x.Total).Take(10).ToList();
        ViewBag.DepensesParCategorie = depenses.GroupBy(d => d.Categorie).Select(g => new DepenseCategorieVm { Categorie = g.Key, Total = g.Sum(x => x.Montant) }).OrderByDescending(x => x.Total).ToList();
        ViewBag.Clients = ventes.GroupBy(v => v.ClientNom).Select(g => new ClientPerformanceVm { Client = g.Key, Tickets = g.Count(), Total = g.Sum(x => x.Total) }).OrderByDescending(x => x.Total).Take(10).ToList();
        return View();
    }
}

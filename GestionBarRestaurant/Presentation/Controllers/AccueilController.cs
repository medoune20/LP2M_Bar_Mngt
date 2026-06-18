using Domaine;
using Infrastructure.Donnees;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Presentation.Filtres;

namespace Presentation.Controllers;

[AutorisationFiltre(1, 2, 3)]
public class AccueilController : BaseController
{
    private readonly AppDbContext _db;
    public AccueilController(AppDbContext db) { _db = db; }

    public IActionResult Index()
    {
        var aujourdHui = DateTime.Today;
        var ventesQuery = _db.Ventes.Include(v => v.Lignes).Where(v => v.TenantId == TenantId && v.Statut == StatutVente.Validee);
        if (EstCaissier) ventesQuery = ventesQuery.Where(v => v.VendeurId == UtilisateurId);
        var ventesTenant = ventesQuery.ToList();
        var depensesTenant = _db.Depenses.Where(d => d.TenantId == TenantId).ToList();
        var produitsTenant = _db.Produits.Where(p => p.TenantId == TenantId).ToList();

        ViewBag.TotalJour = ventesTenant.Where(v => v.DateVente.Date == aujourdHui).Sum(v => v.Total);
        ViewBag.NombreTickets = ventesTenant.Count(v => v.DateVente.Date == aujourdHui);
        ViewBag.TotalDepenses = depensesTenant.Where(d => d.DateDepense.Date == aujourdHui).Sum(d => d.Montant);
        ViewBag.StockFaible = produitsTenant.Count(p => p.Actif && p.StockActuel <= p.StockMinimum);
        ViewBag.NombreClients = _db.Clients.Count(c => c.TenantId == TenantId && c.Actif);
        ViewBag.Caisse = _db.Caisses.OrderByDescending(c => c.DateOuverture)
            .FirstOrDefault(c => c.TenantId == TenantId && c.Statut == StatutCaisse.Ouverte && (!EstCaissier || c.CaissierId == UtilisateurId));
        ViewBag.DernieresVentes = ventesTenant.OrderByDescending(v => v.DateVente).Take(5).ToList();
        ViewBag.ProduitsFaibleStock = produitsTenant.Where(p => p.Actif && p.StockActuel <= p.StockMinimum).Take(5).ToList();
        ViewBag.TenantNom = TenantNom;

        var labels = new List<string>();
        var values = new List<decimal>();
        for (var d = aujourdHui.AddDays(-6); d <= aujourdHui; d = d.AddDays(1))
        {
            labels.Add(d.ToString("dd/MM"));
            values.Add(ventesTenant.Where(v => v.DateVente.Date == d.Date).Sum(v => v.Total));
        }
        ViewBag.GraphLabels = labels;
        ViewBag.GraphValues = values;
        return View();
    }

    public IActionResult Erreur() => View();
}

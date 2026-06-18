using Application.ViewModels;
using Domaine;
using Infrastructure.Donnees;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Presentation.Filtres;

namespace Presentation.Controllers;

[AutorisationFiltre]
public class EvaluationCaissierController : BaseController
{
    private readonly AppDbContext _db;

    public EvaluationCaissierController(AppDbContext db)
    {
        _db = db;
    }

    public IActionResult Index(DateTime? debut, DateTime? fin)
    {
        var dateDebut = debut?.Date ?? DateTime.Today.AddDays(-30);
        var dateFin = fin?.Date.AddDays(1).AddTicks(-1) ?? DateTime.Today.AddDays(1).AddTicks(-1);

        var ventes = _db.Ventes
            .Include(v => v.Lignes)
            .Where(v => v.TenantId == TenantId && v.Statut == StatutVente.Validee && v.DateVente >= dateDebut && v.DateVente <= dateFin)
            .ToList();

        var evaluations = ventes
            .GroupBy(v => v.Vendeur)
            .Select(g =>
            {
                var ca = g.Sum(v => v.Total);
                var tickets = g.Count();
                var panier = tickets == 0 ? 0 : ca / tickets;
                var remises = g.Sum(v => v.Remise + v.RemiseFidelite);
                var produits = g.Sum(v => v.Lignes.Sum(l => l.Quantite));
                var score = CalculerScore(ca, tickets, panier, remises, produits);
                return new EvaluationCaissierVm
                {
                    Caissier = g.Key,
                    NombreTickets = tickets,
                    ChiffreAffaires = ca,
                    PanierMoyen = panier,
                    RemisesAccordees = remises,
                    ProduitsVendus = produits,
                    ScorePerformance = score,
                    Appreciation = Appreciation(score)
                };
            })
            .OrderByDescending(e => e.ScorePerformance)
            .ToList();

        ViewBag.Debut = dateDebut.ToString("yyyy-MM-dd");
        ViewBag.Fin = dateFin.ToString("yyyy-MM-dd");
        ViewBag.TotalCa = evaluations.Sum(e => e.ChiffreAffaires);
        ViewBag.TotalTickets = evaluations.Sum(e => e.NombreTickets);
        ViewBag.Labels = evaluations.Select(e => e.Caissier).ToList();
        ViewBag.Values = evaluations.Select(e => Math.Round(e.ScorePerformance, 0)).ToList();

        return View(evaluations);
    }

    private static decimal CalculerScore(decimal ca, int tickets, decimal panier, decimal remises, int produits)
    {
        var scoreCa = Math.Min(40, ca / 10000m);
        var scoreTickets = Math.Min(25, tickets * 2m);
        var scorePanier = Math.Min(20, panier / 1000m);
        var scoreProduits = Math.Min(10, produits / 5m);
        var penaliteRemise = ca == 0 ? 0 : Math.Min(15, (remises / ca) * 100m);
        var score = scoreCa + scoreTickets + scorePanier + scoreProduits - penaliteRemise;
        return Math.Max(0, Math.Min(100, score));
    }

    private static string Appreciation(decimal score)
    {
        if (score >= 85) return "Excellent";
        if (score >= 70) return "Très bon";
        if (score >= 55) return "Bon";
        if (score >= 40) return "À suivre";
        return "À accompagner";
    }
}

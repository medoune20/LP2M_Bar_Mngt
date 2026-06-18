using Application.ViewModels;
using Domaine;
using Infrastructure.Donnees;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Presentation.Filtres;

namespace Presentation.Controllers;

[AutorisationFiltre]
public class PrevisionController : BaseController
{
    private readonly AppDbContext _db;

    public PrevisionController(AppDbContext db)
    {
        _db = db;
    }

    public IActionResult Index(int horizon = 7)
    {
        horizon = horizon <= 0 ? 7 : horizon;
        horizon = horizon > 60 ? 60 : horizon;

        var aujourdHui = DateTime.Today;
        var debutHistorique = aujourdHui.AddDays(-30);

        var finHistorique = aujourdHui.AddDays(1);
        var ventes = _db.Ventes
            .Include(v => v.Lignes)
            .Where(v => v.TenantId == TenantId && v.Statut == StatutVente.Validee && v.DateVente >= debutHistorique && v.DateVente < finHistorique)
            .ToList();

        var jours = new List<(DateTime Date, decimal Total)>();
        for (var d = debutHistorique; d <= aujourdHui; d = d.AddDays(1))
        {
            jours.Add((d, ventes.Where(v => v.DateVente.Date == d.Date).Sum(v => v.Total)));
        }

        var premiereMoitie = jours.Take(15).Average(x => x.Total);
        var deuxiemeMoitie = jours.Skip(15).Average(x => x.Total);
        var moyenne = jours.Average(x => x.Total);
        var tendance = premiereMoitie == 0 ? 0 : ((deuxiemeMoitie - premiereMoitie) / premiereMoitie) * 100;
        var coefficient = 1 + (tendance / 100m);
        if (coefficient < 0.5m) coefficient = 0.5m;
        if (coefficient > 1.8m) coefficient = 1.8m;

        var previsions = new List<PrevisionVenteVm>();
        for (int i = 1; i <= horizon; i++)
        {
            var date = aujourdHui.AddDays(i);
            var effetWeekend = date.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday ? 1.15m : 1m;
            var totalPrevu = moyenne * coefficient * effetWeekend;

            previsions.Add(new PrevisionVenteVm
            {
                Periode = date.ToString("dd/MM/yyyy"),
                MoyenneJournaliere = moyenne,
                TendancePourcentage = tendance,
                ChiffreAffairesPrevu = totalPrevu
            });
        }

        ViewBag.Horizon = horizon;
        ViewBag.Moyenne = moyenne;
        ViewBag.Tendance = tendance;
        ViewBag.TotalPrevu = previsions.Sum(p => p.ChiffreAffairesPrevu);
        ViewBag.Labels = previsions.Select(p => p.Periode).ToList();
        ViewBag.Values = previsions.Select(p => Math.Round(p.ChiffreAffairesPrevu, 0)).ToList();

        ViewBag.TopProduits = ventes
            .SelectMany(v => v.Lignes)
            .GroupBy(l => l.ProduitNom)
            .Select(g => new TopProduitVm { Produit = g.Key, Quantite = g.Sum(x => x.Quantite), Total = g.Sum(x => x.Total) })
            .OrderByDescending(x => x.Total)
            .Take(5)
            .ToList();

        return View(previsions);
    }
}

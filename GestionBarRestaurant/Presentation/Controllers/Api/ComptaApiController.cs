using Application.ViewModels;
using Domaine;
using Domaine.Models;
using Infrastructure.Donnees;
using Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Presentation.Filtres;

namespace Presentation.Controllers.Api;

/// <summary>
/// API REST d'interfaçage comptable (OHADA). Authentification par clé API
/// (en-tête X-API-Key), cloisonnée par établissement. Lecture seule.
/// </summary>
[ApiController]
[Route("api/v1")]
[CleApiAuth]
public class ComptaApiController : ControllerBase
{
    private readonly ComptabiliteService _compta;
    private readonly AppDbContext _db;

    public ComptaApiController(ComptabiliteService compta, AppDbContext db)
    {
        _compta = compta;
        _db = db;
    }

    private int TenantId => HttpContext.Items["TenantId"] is int t ? t : 0;

    private (DateTime du, DateTime au) Periode(string? du, string? au)
    {
        var d = DateTime.TryParse(du, out var dd) ? dd : DateTime.Today.AddMonths(-1);
        var a = DateTime.TryParse(au, out var aa) ? aa : DateTime.Today;
        return (d, a);
    }

    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { ok = true, tenant = TenantId, utc = DateTime.UtcNow });

    [HttpGet("rapport")]
    public IActionResult Rapport(string? du, string? au)
    {
        var (d, a) = Periode(du, au);
        return Ok(_compta.GenererRapport(TenantId, d, a));
    }

    [HttpGet("ecritures")]
    public IActionResult Ecritures(string? du, string? au)
    {
        var (d, a) = Periode(du, au);
        return Ok(_compta.GenererRapport(TenantId, d, a).Ecritures);
    }

    [HttpGet("balance")]
    public IActionResult Balance(string? du, string? au)
    {
        var (d, a) = Periode(du, au);
        return Ok(_compta.GenererRapport(TenantId, d, a).Balance);
    }

    [HttpGet("ventes")]
    public IActionResult Ventes(string? du, string? au)
    {
        var (d, a) = Periode(du, au);
        var fin = a.Date.AddDays(1);
        var ventes = _db.Ventes
            .Where(v => v.TenantId == TenantId && v.Statut == StatutVente.Validee && v.DateVente >= d.Date && v.DateVente < fin)
            .OrderBy(v => v.DateVente)
            .ToList() // matérialisation avant projection (Total est une propriété calculée)
            .Select(v => new
            {
                v.Id,
                date = v.DateVente,
                ticket = v.NumeroTicket,
                client = v.ClientNom,
                modePaiement = v.ModePaiement,
                total = v.Total
            })
            .ToList();
        return Ok(ventes);
    }

    [HttpGet("depenses")]
    public IActionResult Depenses(string? du, string? au)
    {
        var (d, a) = Periode(du, au);
        var fin = a.Date.AddDays(1);
        var depenses = _db.Depenses
            .Where(x => x.TenantId == TenantId && x.DateDepense >= d.Date && x.DateDepense < fin)
            .OrderBy(x => x.DateDepense)
            .Select(x => new
            {
                x.Id,
                date = x.DateDepense,
                x.Libelle,
                x.Categorie,
                x.Beneficiaire,
                montant = x.Montant
            })
            .ToList();
        return Ok(depenses);
    }

    [HttpGet("exports/ecritures.csv")]
    public IActionResult ExportCsv(string? du, string? au)
    {
        var (d, a) = Periode(du, au);
        var rapport = _compta.GenererRapport(TenantId, d, a);
        var csv = ComptaCsv.Ecritures(rapport);
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "ecritures.csv");
    }
}

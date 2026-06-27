using Application.ViewModels;
using Domaine.Models;
using Infrastructure.Donnees;
using Infrastructure.Securite;
using Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Presentation.Filtres;

namespace Presentation.Controllers;

/// <summary>
/// Comptabilité simplifiée OHADA : trésorerie, écritures, balance, paramétrage
/// et gestion des clés d'API. Accès réservé via le module « comptabilite ».
/// </summary>
[AutorisationFiltre(1, 2, 3)]
public class ComptabiliteController : BaseController
{
    private readonly AppDbContext _db;
    private readonly ComptabiliteService _compta;

    public ComptabiliteController(AppDbContext db, ComptabiliteService compta)
    {
        _db = db;
        _compta = compta;
    }

    private (DateTime du, DateTime au) Periode(string? du, string? au)
    {
        var defautDebut = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var d = DateTime.TryParse(du, out var dd) ? dd : defautDebut;
        var a = DateTime.TryParse(au, out var aa) ? aa : DateTime.Today;
        if (a < d) a = d;
        return (d, a);
    }

    public IActionResult Index(string? du, string? au)
    {
        var (d, a) = Periode(du, au);
        var rapport = _compta.GenererRapport(TenantId, d, a);
        ViewBag.Du = d.ToString("yyyy-MM-dd");
        ViewBag.Au = a.ToString("yyyy-MM-dd");
        return View(rapport);
    }

    public IActionResult Ecritures(string? du, string? au)
    {
        var (d, a) = Periode(du, au);
        ViewBag.Du = d.ToString("yyyy-MM-dd");
        ViewBag.Au = a.ToString("yyyy-MM-dd");
        return View(_compta.GenererRapport(TenantId, d, a));
    }

    public IActionResult Balance(string? du, string? au)
    {
        var (d, a) = Periode(du, au);
        ViewBag.Du = d.ToString("yyyy-MM-dd");
        ViewBag.Au = a.ToString("yyyy-MM-dd");
        return View(_compta.GenererRapport(TenantId, d, a));
    }

    public IActionResult Rapprochement(string? du, string? au)
    {
        var (d, a) = Periode(du, au);
        ViewBag.Du = d.ToString("yyyy-MM-dd");
        ViewBag.Au = a.ToString("yyyy-MM-dd");
        ViewBag.Devise = _compta.ObtenirParametrage(TenantId).Devise;
        return View(_compta.Rapprochement(TenantId, d, a));
    }

    public IActionResult GrandLivre(string? du, string? au)
    {
        var (d, a) = Periode(du, au);
        ViewBag.Du = d.ToString("yyyy-MM-dd");
        ViewBag.Au = a.ToString("yyyy-MM-dd");
        return View(_compta.GrandLivre(TenantId, d, a));
    }

    public IActionResult CompteResultat(string? du, string? au)
    {
        var (d, a) = Periode(du, au);
        ViewBag.Du = d.ToString("yyyy-MM-dd");
        ViewBag.Au = a.ToString("yyyy-MM-dd");
        return View(_compta.CompteResultat(TenantId, d, a));
    }

    public IActionResult DeclarationTva(string? du, string? au)
    {
        var (d, a) = Periode(du, au);
        ViewBag.Du = d.ToString("yyyy-MM-dd");
        ViewBag.Au = a.ToString("yyyy-MM-dd");
        return View(_compta.DeclarationTva(TenantId, d, a));
    }

    public IActionResult ExportEcritures(string? du, string? au)
    {
        var (d, a) = Periode(du, au);
        var rapport = _compta.GenererRapport(TenantId, d, a);
        var csv = ComptaCsv.Ecritures(rapport);
        var nom = $"ecritures_{d:yyyyMMdd}_{a:yyyyMMdd}.csv";
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", nom);
    }

    [HttpGet]
    public IActionResult Parametrage()
    {
        if (!PeutModifier("comptabilite"))
        {
            TempData["Erreur"] = "Accès au paramétrage comptable réservé.";
            return RedirectToAction(nameof(Index));
        }
        return View(_compta.ObtenirParametrage(TenantId));
    }

    [HttpPost]
    public IActionResult Parametrage(ParametrageComptable modele)
    {
        if (!PeutModifier("comptabilite"))
        {
            TempData["Erreur"] = "Accès au paramétrage comptable réservé.";
            return RedirectToAction(nameof(Index));
        }
        _compta.MettreAJourParametrage(TenantId, modele);
        TempData["Succes"] = "Paramétrage comptable enregistré.";
        return RedirectToAction(nameof(Parametrage));
    }

    [HttpGet]
    public IActionResult ApiCles()
    {
        var cles = _db.ClesApi
            .Where(c => c.TenantId == TenantId)
            .OrderByDescending(c => c.DateCreation)
            .ToList();
        return View(cles);
    }

    [HttpPost]
    public IActionResult CreerCle(string libelle)
    {
        if (!PeutModifier("comptabilite"))
        {
            TempData["Erreur"] = "Création de clé API réservée.";
            return RedirectToAction(nameof(ApiCles));
        }

        var (cleComplete, prefixe, hash) = CleApiHelper.Generer();
        _db.ClesApi.Add(new CleApi
        {
            TenantId = TenantId,
            Libelle = string.IsNullOrWhiteSpace(libelle) ? "Clé API" : libelle.Trim(),
            Prefixe = prefixe,
            CleHash = hash,
            Scope = "lecture",
            Actif = true,
            DateCreation = DateTime.Now
        });
        _db.SaveChanges();

        TempData["Succes"] = "Clé API créée. Copiez-la maintenant, elle ne sera plus affichée.";
        TempData["NouvelleCle"] = cleComplete;
        return RedirectToAction(nameof(ApiCles));
    }

    [HttpPost]
    public IActionResult RevoquerCle(int id)
    {
        var cle = _db.ClesApi.FirstOrDefault(c => c.Id == id && c.TenantId == TenantId);
        if (cle != null)
        {
            cle.Actif = false;
            _db.SaveChanges();
            TempData["Succes"] = "Clé API révoquée.";
        }
        return RedirectToAction(nameof(ApiCles));
    }
}

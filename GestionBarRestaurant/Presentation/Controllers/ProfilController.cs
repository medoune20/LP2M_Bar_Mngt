using Domaine;
using Domaine.Models;
using Infrastructure.Donnees;
using Microsoft.AspNetCore.Mvc;
using Presentation.Filtres;

namespace Presentation.Controllers;

[AutorisationFiltre(1)]
public class ProfilController : BaseController
{
    private readonly AppDbContext _db;
    public ProfilController(AppDbContext db) { _db = db; }

    public IActionResult Index()
    {
        var profils = _db.ProfilsAcces.Where(p => p.TenantId == TenantId).OrderBy(p => p.Nom).ToList();
        ViewBag.Compteurs = _db.Utilisateurs
            .Where(u => u.TenantId == TenantId && u.ProfilAccesId > 0)
            .GroupBy(u => u.ProfilAccesId)
            .ToDictionary(g => g.Key, g => g.Count());
        return View(profils);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Creer(string nom)
    {
        nom = (nom ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(nom))
        {
            TempData["Erreur"] = "Le nom du profil est obligatoire.";
            return RedirectToAction(nameof(Index));
        }
        if (_db.ProfilsAcces.Any(p => p.TenantId == TenantId && p.Nom.ToLower() == nom.ToLower()))
        {
            TempData["Erreur"] = "Ce profil existe déjà.";
            return RedirectToAction(nameof(Index));
        }
        var profil = new ProfilAcces { TenantId = TenantId, Nom = nom, Permissions = "", Actif = true };
        _db.ProfilsAcces.Add(profil);
        _db.SaveChanges();
        return RedirectToAction(nameof(Modifier), new { id = profil.Id });
    }

    [HttpGet]
    public IActionResult Modifier(int id)
    {
        var profil = _db.ProfilsAcces.FirstOrDefault(p => p.Id == id && p.TenantId == TenantId);
        return profil == null ? NotFound() : View(profil);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Modifier(int id, string nom, bool actif)
    {
        var profil = _db.ProfilsAcces.FirstOrDefault(p => p.Id == id && p.TenantId == TenantId);
        if (profil == null) return NotFound();

        // Construire la chaîne de permissions à partir des choix par module (radio : "", "C" ou "M").
        var parts = new List<string>();
        foreach (var (cle, _) in ModulesApp.Liste)
        {
            var val = Request.Form[$"mod_{cle}"].ToString();
            if (val == "C") parts.Add($"{cle}=C");
            else if (val == "M") parts.Add($"{cle}=M");
        }

        if (!EstProfilSysteme(profil.Nom))
            profil.Nom = string.IsNullOrWhiteSpace(nom) ? profil.Nom : nom.Trim();
        profil.Permissions = string.Join(";", parts);
        profil.Actif = EstProfilSysteme(profil.Nom) || actif;
        _db.SaveChanges();
        TempData["Succes"] = "Profil enregistré. Les utilisateurs concernés verront les changements à leur prochaine connexion.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Supprimer(int id)
    {
        var profil = _db.ProfilsAcces.FirstOrDefault(p => p.Id == id && p.TenantId == TenantId);
        if (profil == null) return NotFound();
        if (EstProfilSysteme(profil.Nom))
        {
            TempData["Erreur"] = "Les profils système Administrateur, Manager et Caissier ne peuvent pas être supprimés.";
            return RedirectToAction(nameof(Index));
        }
        if (_db.Utilisateurs.Any(u => u.TenantId == TenantId && u.ProfilAccesId == id))
        {
            TempData["Erreur"] = "Des utilisateurs utilisent ce profil. Réaffectez-les d'abord.";
            return RedirectToAction(nameof(Index));
        }
        _db.ProfilsAcces.Remove(profil);
        _db.SaveChanges();
        TempData["Succes"] = "Profil supprimé.";
        return RedirectToAction(nameof(Index));
    }

    private static bool EstProfilSysteme(string nom) =>
        string.Equals(nom, "Administrateur", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(nom, "Manager", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(nom, "Caissier", StringComparison.OrdinalIgnoreCase);
}

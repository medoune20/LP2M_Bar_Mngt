using Microsoft.AspNetCore.Mvc;
using Presentation.Filtres;

namespace Presentation.Controllers;

// Règle multi-tenant : la base SQLite contient les données de TOUS les tenants.
// Son téléchargement est donc réservé au super administrateur de la plateforme.
[AutorisationFiltre(1)]
public class SauvegardeController : BaseController
{
    private readonly IWebHostEnvironment _env;
    public SauvegardeController(IWebHostEnvironment env) { _env = env; }

    public IActionResult Index()
    {
        if (!IsSuperAdmin)
        {
            TempData["Erreur"] = "La sauvegarde globale est réservée au super administrateur.";
            return RedirectToAction("Index", "Accueil");
        }

        var dbPath = Path.Combine(Environment.GetEnvironmentVariable("DATA_DIR") ?? Path.Combine(_env.ContentRootPath, "Data"), "gestionbar_analytics_v3.db");
        ViewBag.DbPath = dbPath;
        ViewBag.DbExists = System.IO.File.Exists(dbPath);
        ViewBag.DbSize = System.IO.File.Exists(dbPath) ? new FileInfo(dbPath).Length : 0;
        return View();
    }

    public IActionResult Telecharger()
    {
        if (!IsSuperAdmin) return Forbid();
        var dbPath = Path.Combine(Environment.GetEnvironmentVariable("DATA_DIR") ?? Path.Combine(_env.ContentRootPath, "Data"), "gestionbar_analytics_v3.db");
        if (!System.IO.File.Exists(dbPath))
        {
            TempData["Erreur"] = "La base SQLite est introuvable.";
            return RedirectToAction(nameof(Index));
        }
        var copie = Path.Combine(Path.GetTempPath(), $"gestionbar_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db");
        System.IO.File.Copy(dbPath, copie, true);
        var bytes = System.IO.File.ReadAllBytes(copie);
        var nom = $"gestionbar_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db";
        return File(bytes, "application/octet-stream", nom);
    }
}

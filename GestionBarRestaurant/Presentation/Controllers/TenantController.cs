using Domaine;
using Domaine.Models;
using Infrastructure.Donnees;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Presentation.Filtres;

namespace Presentation.Controllers;

[AutorisationFiltre(1)]
public class TenantController : BaseController
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly Infrastructure.Services.EmailService _email;
    public TenantController(AppDbContext db, IWebHostEnvironment env, Infrastructure.Services.EmailService email) { _db = db; _env = env; _email = email; }

    // --- Demandes d'inscription (super administrateur) ---
    public IActionResult Demandes()
    {
        if (!IsSuperAdmin) return RedirectToAction("Index", "Accueil");
        var tenantsInactifs = _db.Tenants.Where(t => !t.Actif).Select(t => t.Id).ToList();
        var demandes = _db.Utilisateurs
            .Where(u => !u.Actif && u.DateInscription != null && tenantsInactifs.Contains(u.TenantId))
            .OrderByDescending(u => u.DateInscription)
            .ToList();
        ViewBag.Tenants = _db.Tenants.Where(t => tenantsInactifs.Contains(t.Id)).ToList();
        return View(demandes);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ActiverDemande(int utilisateurId)
    {
        if (!IsSuperAdmin) return Forbid();
        var u = _db.Utilisateurs.FirstOrDefault(x => x.Id == utilisateurId);
        if (u == null) return NotFound();
        var t = _db.Tenants.FirstOrDefault(x => x.Id == u.TenantId);
        if (t == null) return NotFound();
        DatabaseInitializer.GarantirTenantDefaults(_db, t.Id, ajouterCatalogue: true);
        u.Role = RoleUtilisateur.Administrateur;
        u.ProfilAccesId = DatabaseInitializer.IdProfil(_db, t.Id, RoleUtilisateur.Administrateur);
        u.Actif = true;
        t.Actif = true;
        _db.SaveChanges();
        _email.Envoyer(u.Email, "Votre compte est activé - LP2M APPS",
            $"<p>Bonjour {u.Nom},</p><p>Le compte de <b>{t.Nom}</b> est activé. Vous pouvez vous connecter avec votre identifiant.</p>");
        TempData["Succes"] = $"Compte « {t.Nom} » activé.";
        return RedirectToAction(nameof(Demandes));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RejeterDemande(int utilisateurId)
    {
        if (!IsSuperAdmin) return Forbid();
        var u = _db.Utilisateurs.FirstOrDefault(x => x.Id == utilisateurId);
        if (u == null) return NotFound();
        var t = _db.Tenants.FirstOrDefault(x => x.Id == u.TenantId && !x.Actif);
        var email = u.Email; var nom = u.Nom; var etab = t?.Nom ?? "";
        // Supprimer la demande (tenant inactif + son admin + client comptoir).
        if (t != null)
        {
            _db.Clients.RemoveRange(_db.Clients.Where(c => c.TenantId == t.Id));
            _db.Utilisateurs.RemoveRange(_db.Utilisateurs.Where(x => x.TenantId == t.Id));
            _db.Tenants.Remove(t);
        }
        else { _db.Utilisateurs.Remove(u); }
        _db.SaveChanges();
        _email.Envoyer(email, "Demande non retenue - LP2M APPS",
            $"<p>Bonjour {nom},</p><p>Votre demande d'inscription pour « {etab} » n'a pas été retenue.</p>");
        TempData["Succes"] = "Demande rejetée.";
        return RedirectToAction(nameof(Demandes));
    }

    // Liste de TOUS les tenants : réservée au super administrateur.
    public IActionResult Index()
    {
        if (!IsSuperAdmin) return RedirectToAction(nameof(Modifier), new { id = TenantId });
        return View(_db.Tenants.AsNoTracking().OrderBy(t => t.Nom).ToList());
    }

    [HttpPost]
    public IActionResult Changer(int id)
    {
        if (!IsSuperAdmin)
        {
            TempData["Erreur"] = "Seul le super administrateur peut changer de tenant.";
            return RedirectToAction("Index", "Accueil");
        }
        var tenant = _db.Tenants.FirstOrDefault(t => t.Id == id);
        if (tenant == null || !tenant.Actif)
        {
            TempData["Erreur"] = "Tenant introuvable ou inactif.";
            return RedirectToAction(nameof(Index));
        }
        RafraichirTenantSession(_db, id);
        TempData["Succes"] = $"Tenant actif : {tenant.Nom}.";
        return RedirectToAction("Index", "Accueil");
    }

    [HttpGet]
    public IActionResult Nouveau() => IsSuperAdmin ? View(new Tenant()) : RedirectToAction(nameof(Index));

    [HttpPost]
    public async Task<IActionResult> Nouveau(Tenant tenant, IFormFile? logo)
    {
        if (!IsSuperAdmin) return RedirectToAction(nameof(Index));
        NormaliserTenant(tenant);
        ValiderCodeUnique(tenant.Code, tenant.Id);
        if (!ModelState.IsValid) return View(tenant);
        tenant.LogoPath = await SauverLogo(logo) ?? tenant.LogoPath;
        _db.Tenants.Add(tenant);
        _db.SaveChanges();
        DatabaseInitializer.GarantirTenantDefaults(_db, tenant.Id, ajouterCatalogue: true);
        TempData["Succes"] = "Tenant créé avec profils/rôles, catégories, client comptoir, fidélité et catalogue par défaut.";
        return RedirectToAction(nameof(Index));
    }

    // Édition : le super admin modifie n'importe quel tenant ;
    // l'administrateur d'un tenant ne modifie QUE le sien.
    [HttpGet]
    public IActionResult Modifier(int id)
    {
        if (!IsSuperAdmin && id != TenantId) return RedirectToAction(nameof(Modifier), new { id = TenantId });
        var tenant = _db.Tenants.AsNoTracking().FirstOrDefault(t => t.Id == id);
        return tenant == null ? NotFound() : View(tenant);
    }

    [HttpPost]
    public async Task<IActionResult> Modifier(Tenant tenant, IFormFile? logo)
    {
        if (!IsSuperAdmin && tenant.Id != TenantId) return RedirectToAction(nameof(Modifier), new { id = TenantId });
        NormaliserTenant(tenant);
        ValiderCodeUnique(tenant.Code, tenant.Id);
        if (!ModelState.IsValid) return View(tenant);
        var existant = _db.Tenants.FirstOrDefault(t => t.Id == tenant.Id);
        if (existant == null) return NotFound();

        existant.Nom = tenant.Nom;
        existant.Code = tenant.Code;
        existant.Telephone = tenant.Telephone;
        existant.Adresse = tenant.Adresse;
        existant.CouleurPrincipale = tenant.CouleurPrincipale;
        // Informations facture / publicité
        existant.Slogan = tenant.Slogan?.Trim() ?? string.Empty;
        existant.Email = tenant.Email?.Trim() ?? string.Empty;
        existant.SiteWeb = tenant.SiteWeb?.Trim() ?? string.Empty;
        existant.Ville = tenant.Ville?.Trim() ?? string.Empty;
        existant.RegistreCommerce = tenant.RegistreCommerce?.Trim() ?? string.Empty;
        existant.NumeroContribuable = tenant.NumeroContribuable?.Trim() ?? string.Empty;
        existant.PiedFacture = tenant.PiedFacture?.Trim() ?? string.Empty;

        var nouveauLogo = await SauverLogo(logo);
        if (nouveauLogo != null) existant.LogoPath = nouveauLogo;

        // Seul le super administrateur peut activer/désactiver un tenant.
        if (IsSuperAdmin) existant.Actif = tenant.Actif;

        _db.SaveChanges();
        if (TenantId == tenant.Id) RafraichirTenantSession(_db, tenant.Id);
        TempData["Succes"] = "Établissement mis à jour.";
        return RedirectToAction(IsSuperAdmin ? nameof(Index) : nameof(Modifier), IsSuperAdmin ? null : new { id = TenantId });
    }

    private async Task<string?> SauverLogo(IFormFile? logo)
    {
        if (logo == null || logo.Length == 0) return null;
        if (logo.Length > 2 * 1024 * 1024) return null;
        var ext = Path.GetExtension(logo.FileName).ToLowerInvariant();
        if (ext != ".png" && ext != ".jpg" && ext != ".jpeg" && ext != ".webp") return null;
        var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var dossier = Path.Combine(webRoot, "uploads", "logos");
        Directory.CreateDirectory(dossier);
        var nom = $"{Guid.NewGuid():N}{ext}";
        await using (var flux = new FileStream(Path.Combine(dossier, nom), FileMode.Create))
        {
            await logo.CopyToAsync(flux);
        }
        return $"/uploads/logos/{nom}";
    }

    private void NormaliserTenant(Tenant tenant)
    {
        tenant.Nom = (tenant.Nom ?? string.Empty).Trim();
        tenant.Code = (tenant.Code ?? string.Empty).Trim().ToUpperInvariant();
        // Le code n'est plus obligatoire dans le formulaire : on le génère
        // automatiquement à partir du nom lorsqu'il est laissé vide.
        if (string.IsNullOrWhiteSpace(tenant.Code))
        {
            tenant.Code = GenererCodeTenant(tenant.Nom);
        }
        tenant.Telephone = (tenant.Telephone ?? string.Empty).Trim();
        tenant.Adresse = (tenant.Adresse ?? string.Empty).Trim();
        tenant.CouleurPrincipale = string.IsNullOrWhiteSpace(tenant.CouleurPrincipale) ? "#165DFF" : tenant.CouleurPrincipale.Trim();
    }

    private string GenererCodeTenant(string baseName)
    {
        var source = string.IsNullOrWhiteSpace(baseName) ? "BAR" : baseName;
        var codeBase = new string((source.ToUpperInvariant() + "0000").Where(char.IsLetterOrDigit).ToArray());
        var prefixe = codeBase.Length >= 4 ? codeBase[..4] : codeBase;
        var code = prefixe + DateTime.Now.ToString("HHmmssfff");
        while (_db.Tenants.Any(t => t.Code == code)) code = prefixe + DateTime.Now.Ticks.ToString()[^6..];
        return code;
    }

    private void ValiderCodeUnique(string code, int tenantId)
    {
        if (string.IsNullOrWhiteSpace(code)) return;
        if (_db.Tenants.Any(t => t.Id != tenantId && t.Code.ToLower() == code.ToLower()))
        {
            ModelState.AddModelError(nameof(Tenant.Code), "Ce code tenant existe déjà.");
        }
    }
}

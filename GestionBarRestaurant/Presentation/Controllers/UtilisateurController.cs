using Domaine;
using Domaine.Models;
using Infrastructure.Donnees;
using Infrastructure.Securite;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Presentation.Filtres;

namespace Presentation.Controllers;

[AutorisationFiltre(1)]
public class UtilisateurController : BaseController
{
    private readonly AppDbContext _db;
    public UtilisateurController(AppDbContext db) { _db = db; }

    public IActionResult Index()
    {
        var utilisateurs = IsSuperAdmin
            ? _db.Utilisateurs.AsNoTracking().OrderBy(u => u.TenantId).ThenBy(u => u.Nom).ToList()
            : _db.Utilisateurs.AsNoTracking().Where(u => u.TenantId == TenantId).OrderBy(u => u.Nom).ToList();
        ViewBag.Tenants = _db.Tenants.AsNoTracking().OrderBy(t => t.Nom).ToList();
        ViewBag.Profils = ProfilsDuTenant(TenantId);
        return View(utilisateurs);
    }

    [HttpGet]
    public IActionResult Nouveau()
    {
        ViewBag.Tenants = _db.Tenants.AsNoTracking().OrderBy(t => t.Nom).ToList();
        ViewBag.Profils = ProfilsDuTenant(TenantId);
        return View(new Utilisateur { TenantId = TenantId, Role = RoleUtilisateur.Caissier, ProfilAccesId = DatabaseInitializer.IdProfil(_db, TenantId, RoleUtilisateur.Caissier), Actif = true });
    }

    [HttpPost]
    public IActionResult Nouveau(Utilisateur utilisateur)
    {
        utilisateur.Login = (utilisateur.Login ?? string.Empty).Trim();
        utilisateur.Nom = (utilisateur.Nom ?? string.Empty).Trim();

        if (!IsSuperAdmin)
        {
            utilisateur.TenantId = TenantId;
            utilisateur.IsSuperAdmin = false;
        }

        AlignerRoleEtProfil(utilisateur);
        ValiderUtilisateur(utilisateur, nouveau: true);

        if (!ModelState.IsValid)
        {
            ViewBag.Tenants = _db.Tenants.AsNoTracking().OrderBy(t => t.Nom).ToList();
            ViewBag.Profils = ProfilsDuTenant(utilisateur.TenantId > 0 ? utilisateur.TenantId : TenantId);
            return View(utilisateur);
        }

        utilisateur.MotDePasse = PasswordHelper.Hasher(utilisateur.MotDePasse);
        _db.Utilisateurs.Add(utilisateur);
        _db.SaveChanges();
        TempData["Succes"] = "Utilisateur créé.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Modifier(int id)
    {
        var utilisateur = _db.Utilisateurs
            .AsNoTracking()
            .FirstOrDefault(u => u.Id == id && (IsSuperAdmin || u.TenantId == TenantId));
        if (utilisateur == null) return NotFound();

        // Ne jamais renvoyer le hash vers l'écran.
        utilisateur.MotDePasse = string.Empty;
        ViewBag.Tenants = _db.Tenants.AsNoTracking().OrderBy(t => t.Nom).ToList();
        ViewBag.Profils = ProfilsDuTenant(utilisateur.TenantId);
        return View(utilisateur);
    }

    [HttpPost]
    public IActionResult Modifier(Utilisateur utilisateur)
    {
        utilisateur.Login = (utilisateur.Login ?? string.Empty).Trim();
        utilisateur.Nom = (utilisateur.Nom ?? string.Empty).Trim();

        var existant = _db.Utilisateurs.FirstOrDefault(u => u.Id == utilisateur.Id && (IsSuperAdmin || u.TenantId == TenantId));
        if (existant == null) return NotFound();

        AlignerRoleEtProfil(utilisateur);
        ValiderUtilisateur(utilisateur, nouveau: false);

        if (!ModelState.IsValid)
        {
            utilisateur.MotDePasse = string.Empty;
            ViewBag.Tenants = _db.Tenants.AsNoTracking().OrderBy(t => t.Nom).ToList();
            ViewBag.Profils = ProfilsDuTenant(utilisateur.TenantId > 0 ? utilisateur.TenantId : TenantId);
            return View(utilisateur);
        }

        AlignerRoleEtProfil(utilisateur);

        existant.Nom = utilisateur.Nom;
        existant.Login = utilisateur.Login;
        if (!string.IsNullOrWhiteSpace(utilisateur.MotDePasse))
        {
            existant.MotDePasse = PasswordHelper.Hasher(utilisateur.MotDePasse);
        }
        existant.Role = utilisateur.Role;
        existant.Actif = utilisateur.Actif;
        existant.ProfilAccesId = utilisateur.ProfilAccesId;
        if (IsSuperAdmin)
        {
            existant.TenantId = utilisateur.TenantId;
            existant.IsSuperAdmin = utilisateur.IsSuperAdmin;
        }
        _db.SaveChanges();
        TempData["Succes"] = "Utilisateur modifié.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult BasculerActif(int id)
    {
        var utilisateur = _db.Utilisateurs.FirstOrDefault(u => u.Id == id && (IsSuperAdmin || u.TenantId == TenantId));
        if (utilisateur != null)
        {
            utilisateur.Actif = !utilisateur.Actif;
            _db.SaveChanges();
        }
        return RedirectToAction(nameof(Index));
    }


    private List<ProfilAcces> ProfilsDuTenant(int tenantId) => _db.ProfilsAcces
        .Where(pr => pr.TenantId == tenantId && pr.Actif)
        .AsEnumerable()
        .OrderBy(pr => pr.Nom == "Caissier" ? 1 : pr.Nom == "Manager" ? 2 : pr.Nom == "Administrateur" ? 3 : 9)
        .ThenBy(pr => pr.Nom)
        .ToList();

    private void AlignerRoleEtProfil(Utilisateur utilisateur)
    {
        if (utilisateur.ProfilAccesId > 0)
        {
            var profil = _db.ProfilsAcces.FirstOrDefault(p => p.Id == utilisateur.ProfilAccesId && p.TenantId == utilisateur.TenantId && p.Actif);
            if (profil == null)
            {
                ModelState.AddModelError(nameof(Utilisateur.ProfilAccesId), "Profil/rôle introuvable pour ce tenant.");
                return;
            }

            utilisateur.Role = DatabaseInitializer.RoleDepuisProfil(profil.Nom);
            return;
        }

        utilisateur.ProfilAccesId = DatabaseInitializer.IdProfil(_db, utilisateur.TenantId, utilisateur.Role);
    }

    private void ValiderUtilisateur(Utilisateur utilisateur, bool nouveau)
    {
        if (string.IsNullOrWhiteSpace(utilisateur.Nom))
        {
            ModelState.AddModelError(nameof(Utilisateur.Nom), "Le nom est obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(utilisateur.Login))
        {
            ModelState.AddModelError(nameof(Utilisateur.Login), "Le login est obligatoire.");
        }

        if (nouveau && string.IsNullOrWhiteSpace(utilisateur.MotDePasse))
        {
            ModelState.AddModelError(nameof(Utilisateur.MotDePasse), "Le mot de passe est obligatoire.");
        }

        if (!string.IsNullOrWhiteSpace(utilisateur.MotDePasse) && utilisateur.MotDePasse.Length < 6)
        {
            ModelState.AddModelError(nameof(Utilisateur.MotDePasse), "Le mot de passe doit contenir au moins 6 caractères.");
        }

        if (_db.Utilisateurs.Any(u => u.Id != utilisateur.Id && u.Login.ToLower() == utilisateur.Login.ToLower()))
        {
            ModelState.AddModelError(nameof(Utilisateur.Login), "Ce login existe déjà.");
        }

        if (!_db.Tenants.Any(t => t.Id == utilisateur.TenantId && t.Actif))
        {
            ModelState.AddModelError(nameof(Utilisateur.TenantId), "Le tenant sélectionné est introuvable ou inactif.");
        }
    }
}

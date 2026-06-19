using System.Security.Claims;
using Domaine;
using Domaine.Models;
using Infrastructure.Donnees;
using Infrastructure.Securite;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Presentation.Controllers;

public class AuthController : Controller
{
    private readonly AppDbContext _db;
    private readonly EmailService _email;
    private readonly IConfiguration _configuration;

    public AuthController(AppDbContext db, EmailService email, IConfiguration configuration)
    {
        _db = db;
        _email = email;
        _configuration = configuration;
    }

    private bool GoogleEstConfigure =>
        !string.IsNullOrWhiteSpace(_configuration["Authentication:Google:ClientId"]) &&
        !string.IsNullOrWhiteSpace(_configuration["Authentication:Google:ClientSecret"]);

    [HttpGet]
    public IActionResult Connexion()
    {
        if (HttpContext.Session.GetInt32("UtilisateurRole").HasValue)
            return RedirectToAction("Index", "Accueil");

        ViewBag.GoogleActive = GoogleEstConfigure;
        return View();
    }

    [HttpPost]
    public IActionResult Connexion(string login, string motdepasse)
    {
        ViewBag.GoogleActive = GoogleEstConfigure;
        login = (login ?? string.Empty).Trim();
        motdepasse ??= string.Empty;
        var loginMin = login.ToLowerInvariant();

        var utilisateur = _db.Utilisateurs
            .FirstOrDefault(u => u.Actif && u.Login.ToLower() == loginMin);

        if (utilisateur != null && utilisateur.VerrouJusqua.HasValue && utilisateur.VerrouJusqua.Value > DateTime.Now)
        {
            var minutes = Math.Max(1, (int)Math.Ceiling((utilisateur.VerrouJusqua.Value - DateTime.Now).TotalMinutes));
            ViewBag.Erreur = $"Compte temporairement verrouillé suite à plusieurs échecs. Réessayez dans {minutes} minute(s).";
            return View();
        }

        if (utilisateur == null)
        {
            var enAttente = _db.Utilisateurs
                .FirstOrDefault(u => !u.Actif && u.Login.ToLower() == loginMin);

            if (enAttente != null && PasswordHelper.Verifier(motdepasse, enAttente.MotDePasse))
            {
                ViewBag.Erreur = enAttente.EmailConfirme
                    ? "Votre compte est en attente de validation par l'administrateur."
                    : "Veuillez d'abord confirmer votre email. Le compte sera ensuite validé par l'administrateur.";
                return View();
            }
        }

        if (utilisateur == null || !PasswordHelper.Verifier(motdepasse, utilisateur.MotDePasse))
        {
            if (utilisateur != null)
            {
                utilisateur.TentativesEchouees++;
                if (utilisateur.TentativesEchouees >= 5)
                {
                    utilisateur.VerrouJusqua = DateTime.Now.AddMinutes(15);
                    utilisateur.TentativesEchouees = 0;
                }
                _db.SaveChanges();
            }

            ViewBag.Erreur = "Identifiants incorrects ou compte désactivé.";
            return View();
        }

        utilisateur.TentativesEchouees = 0;
        utilisateur.VerrouJusqua = null;
        utilisateur.DateDerniereConnexion = DateTime.Now;

        if (!PasswordHelper.EstHash(utilisateur.MotDePasse))
            utilisateur.MotDePasse = PasswordHelper.Hasher(motdepasse);

        _db.SaveChanges();
        return ConnecterUtilisateur(utilisateur);
    }

    [HttpGet]
    public IActionResult GoogleLogin()
    {
        if (!GoogleEstConfigure)
        {
            TempData["Erreur"] = "Connexion Google non configurée. Renseignez Authentication:Google:ClientId et ClientSecret.";
            return RedirectToAction(nameof(Connexion));
        }

        var redirectUrl = Url.Action(nameof(GoogleCallback), "Auth");
        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet]
    public async Task<IActionResult> GoogleCallback()
    {
        var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!result.Succeeded || result.Principal == null)
        {
            TempData["Erreur"] = "Connexion Google impossible. Réessayez.";
            return RedirectToAction(nameof(Connexion));
        }

        var email = result.Principal.FindFirstValue(ClaimTypes.Email)?.Trim() ?? string.Empty;
        var nom = result.Principal.FindFirstValue(ClaimTypes.Name)?.Trim() ?? string.Empty;
        var googleSubject = result.Principal.FindFirstValue(ClaimTypes.NameIdentifier)?.Trim() ?? string.Empty;

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(googleSubject))
        {
            TempData["Erreur"] = "Google n'a pas retourné les informations nécessaires au compte.";
            return RedirectToAction(nameof(Connexion));
        }

        var emailMin = email.ToLowerInvariant();
        var utilisateur = _db.Utilisateurs.FirstOrDefault(u =>
            u.GoogleSubject == googleSubject || (!string.IsNullOrEmpty(u.Email) && u.Email.ToLower() == emailMin));

        if (utilisateur != null)
        {
            utilisateur.GoogleSubject = googleSubject;
            utilisateur.FournisseurConnexion = "Google";
            utilisateur.Email = email;
            utilisateur.EmailConfirme = true;
            utilisateur.DateDerniereConnexion = DateTime.Now;
            _db.SaveChanges();

            if (!utilisateur.Actif)
            {
                TempData["Erreur"] = "Votre compte Google est enregistré, mais il attend encore la validation de l'administrateur.";
                return RedirectToAction(nameof(Connexion));
            }

            return ConnecterUtilisateur(utilisateur);
        }

        CreerDemandeGoogle(email, nom, googleSubject);
        TempData["Succes"] = "Compte Google reçu. Une demande d'ouverture d'établissement a été créée et attend la validation du super administrateur.";
        return RedirectToAction(nameof(Connexion));
    }

    [HttpGet]
    public IActionResult Inscription()
    {
        if (HttpContext.Session.GetInt32("UtilisateurRole").HasValue)
            return RedirectToAction("Index", "Accueil");

        ViewBag.GoogleActive = GoogleEstConfigure;
        return View();
    }

    [HttpPost]
    public IActionResult Inscription(string etablissement, string ville, string nomComplet, string email, string login, string motdepasse, string motdepasseConfirmation)
    {
        ViewBag.GoogleActive = GoogleEstConfigure;
        etablissement = (etablissement ?? string.Empty).Trim();
        ville = (ville ?? string.Empty).Trim();
        nomComplet = (nomComplet ?? string.Empty).Trim();
        email = (email ?? string.Empty).Trim();
        login = (login ?? string.Empty).Trim();
        motdepasse ??= string.Empty;
        motdepasseConfirmation ??= string.Empty;

        if (etablissement.Length < 2 || nomComplet.Length < 2 || login.Length < 3 || !email.Contains('@'))
        {
            ViewBag.Erreur = "Merci de remplir tous les champs obligatoires (email valide).";
            return View();
        }

        var erreurMotDePasse = PasswordHelper.ValiderComplexite(motdepasse);
        if (erreurMotDePasse != null)
        {
            ViewBag.Erreur = erreurMotDePasse;
            return View();
        }

        if (motdepasse != motdepasseConfirmation)
        {
            ViewBag.Erreur = "Le mot de passe et sa confirmation ne correspondent pas.";
            return View();
        }

        var loginMin = login.ToLowerInvariant();
        if (_db.Utilisateurs.Any(u => u.Login.ToLower() == loginMin))
        {
            ViewBag.Erreur = "Cet identifiant est déjà utilisé. Choisissez-en un autre.";
            return View();
        }

        var tenant = new Tenant
        {
            Nom = etablissement,
            Code = GenererCodeTenant(etablissement),
            Ville = ville,
            Email = email,
            CouleurPrincipale = "#0078D4",
            Actif = false
        };

        _db.Tenants.Add(tenant);
        _db.SaveChanges();
        DatabaseInitializer.GarantirTenantDefaults(_db, tenant.Id, ajouterCatalogue: true);
        var profilAdmin = DatabaseInitializer.IdProfil(_db, tenant.Id, RoleUtilisateur.Administrateur);

        var token = Guid.NewGuid().ToString("N");
        var admin = new Utilisateur
        {
            TenantId = tenant.Id,
            Nom = nomComplet,
            Login = login,
            MotDePasse = PasswordHelper.Hasher(motdepasse),
            Role = RoleUtilisateur.Administrateur,
            ProfilAccesId = profilAdmin,
            Email = email,
            EmailConfirme = false,
            TokenConfirmation = token,
            DateInscription = DateTime.Now,
            FournisseurConnexion = "Local",
            Actif = false
        };

        _db.Utilisateurs.Add(admin);
        _db.SaveChanges();

        var lien = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/Auth/ConfirmerEmail?id={admin.Id}&token={token}";
        _email.Envoyer(email, "Confirmez votre inscription - Gestion Bar",
            $"<p>Bonjour {nomComplet},</p>" +
            $"<p>Merci pour votre inscription pour <b>{etablissement}</b>.</p>" +
            $"<p>Confirmez votre email en cliquant ici : <a href='{lien}'>Confirmer mon email</a></p>" +
            "<p>Votre compte sera ensuite activé par l'administrateur.</p>");

        NotifierSuperAdmins(etablissement, email);
        TempData["Succes"] = _email.EstConfigure
            ? "Inscription enregistrée. Vérifiez votre email pour confirmer, puis attendez la validation de l'administrateur."
            : "Inscription enregistrée. Elle sera validée par l'administrateur.";

        return RedirectToAction(nameof(Connexion));
    }

    [HttpGet]
    public IActionResult ConfirmerEmail(int id, string token)
    {
        var utilisateur = _db.Utilisateurs.FirstOrDefault(x => x.Id == id);

        if (utilisateur == null || string.IsNullOrEmpty(token) || utilisateur.TokenConfirmation != token)
        {
            TempData["Erreur"] = "Lien de confirmation invalide ou expiré.";
            return RedirectToAction(nameof(Connexion));
        }

        utilisateur.EmailConfirme = true;
        utilisateur.TokenConfirmation = string.Empty;
        _db.SaveChanges();

        TempData["Succes"] = "Email confirmé. Votre compte sera activé par l'administrateur.";
        return RedirectToAction(nameof(Connexion));
    }

    [HttpPost]
    public IActionResult Deconnexion()
    {
        HttpContext.Session.Clear();
        return RedirectToAction(nameof(Connexion));
    }

    private IActionResult ConnecterUtilisateur(Utilisateur utilisateur)
    {
        var tenant = _db.Tenants.FirstOrDefault(t => t.Id == utilisateur.TenantId && t.Actif);
        if (tenant == null)
        {
            ViewBag.GoogleActive = GoogleEstConfigure;
            ViewBag.Erreur = "Le tenant associé à ce compte est introuvable ou inactif.";
            return View(nameof(Connexion));
        }

        if (utilisateur.ProfilAccesId == 0)
        {
            utilisateur.ProfilAccesId = DatabaseInitializer.IdProfil(_db, utilisateur.TenantId, utilisateur.Role);
            _db.SaveChanges();
        }

        var profil = _db.ProfilsAcces.FirstOrDefault(pr => pr.Id == utilisateur.ProfilAccesId && pr.TenantId == utilisateur.TenantId && pr.Actif);
        if (profil != null)
        {
            var roleProfil = DatabaseInitializer.RoleDepuisProfil(profil.Nom);
            if (roleProfil != utilisateur.Role)
            {
                utilisateur.Role = roleProfil;
                _db.SaveChanges();
            }
        }
        var permsProfil = profil?.Permissions ?? ModulesApp.PermissionsPourRole(utilisateur.Role);

        HttpContext.Session.Clear();
        HttpContext.Session.SetInt32("UtilisateurId", utilisateur.Id);
        HttpContext.Session.SetInt32("UtilisateurRole", (int)utilisateur.Role);
        HttpContext.Session.SetString("UtilisateurNom", utilisateur.Nom ?? string.Empty);
        HttpContext.Session.SetString("IsSuperAdmin", utilisateur.IsSuperAdmin ? "true" : "false");
        HttpContext.Session.SetInt32("ProfilAccesId", utilisateur.ProfilAccesId);
        HttpContext.Session.SetString("Permissions", permsProfil);
        HttpContext.Session.SetInt32("TenantId", tenant.Id);
        HttpContext.Session.SetString("TenantNom", tenant.Nom ?? string.Empty);
        HttpContext.Session.SetString("TenantCode", tenant.Code ?? string.Empty);
        HttpContext.Session.SetString("TenantColor", tenant.CouleurPrincipale ?? string.Empty);

        return RedirectToAction("Index", "Accueil");
    }

    private void CreerDemandeGoogle(string email, string nom, string googleSubject)
    {
        var tenant = new Tenant
        {
            Nom = $"Établissement de {nom}",
            Code = GenererCodeTenant("GOOGLE"),
            Email = email,
            Ville = "Abidjan",
            CouleurPrincipale = "#0078D4",
            Actif = false
        };
        _db.Tenants.Add(tenant);
        _db.SaveChanges();
        DatabaseInitializer.GarantirTenantDefaults(_db, tenant.Id, ajouterCatalogue: true);

        var admin = new Utilisateur
        {
            TenantId = tenant.Id,
            Nom = string.IsNullOrWhiteSpace(nom) ? email : nom,
            Login = email,
            MotDePasse = string.Empty,
            Role = RoleUtilisateur.Administrateur,
            ProfilAccesId = DatabaseInitializer.IdProfil(_db, tenant.Id, RoleUtilisateur.Administrateur),
            Email = email,
            EmailConfirme = true,
            DateInscription = DateTime.Now,
            FournisseurConnexion = "Google",
            GoogleSubject = googleSubject,
            Actif = false
        };
        _db.Utilisateurs.Add(admin);
        _db.SaveChanges();
        NotifierSuperAdmins(tenant.Nom, email);
    }

    private string GenererCodeTenant(string baseName)
    {
        var codeBase = new string((baseName.ToUpperInvariant() + "0000").Where(char.IsLetterOrDigit).ToArray());
        var prefixe = codeBase.Length >= 4 ? codeBase[..4] : codeBase;
        var code = prefixe + DateTime.Now.ToString("HHmmssfff");
        while (_db.Tenants.Any(t => t.Code == code)) code = prefixe + DateTime.Now.Ticks.ToString()[^6..];
        return code;
    }

    private void NotifierSuperAdmins(string etablissement, string email)
    {
        foreach (var sa in _db.Utilisateurs.Where(u => u.IsSuperAdmin && u.Actif && u.Email != "").ToList())
        {
            _email.Envoyer(sa.Email,
                "Nouvelle demande d'inscription - Gestion Bar",
                $"<p>Nouvelle demande : <b>{etablissement}</b> ({email}).</p><p>À valider dans « Demandes d'inscription ».</p>");
        }
    }
}

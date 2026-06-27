using GestionHopital.Infrastructure.Donnees;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionHopital.Presentation.Controllers;

public class AuthController : Controller
{
    private readonly AppDbContext _db;
    public AuthController(AppDbContext db) { _db = db; }

    [HttpGet]
    public IActionResult Connexion(string? retour)
    {
        if (HttpContext.Session.GetInt32("UtilisateurId") > 0)
            return RedirectToAction("Index", "Accueil");
        ViewBag.Retour = retour;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Connexion(string login, string motDePasse, string? retour)
    {
        var hash = HashMdp(motDePasse);
        var user = await _db.Utilisateurs.FirstOrDefaultAsync(u => u.Login == login && u.MotDePasse == hash && u.Actif);
        if (user == null)
        {
            ViewBag.Erreur = "Identifiants incorrects.";
            return View();
        }
        user.DernièreConnexion = DateTime.Now;
        await _db.SaveChangesAsync();

        HttpContext.Session.SetInt32("UtilisateurId", user.Id);
        HttpContext.Session.SetString("UtilisateurNom", user.NomComplet);
        HttpContext.Session.SetString("UtilisateurLogin", user.Login);
        HttpContext.Session.SetInt32("Role", (int)user.Role);
        if (user.MedecinId.HasValue)
            HttpContext.Session.SetInt32("MedecinId", user.MedecinId.Value);

        return Redirect(retour ?? "/Accueil");
    }

    [HttpGet]
    public IActionResult Déconnexion()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Connexion");
    }

    private static string HashMdp(string mdp)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(mdp));
        return Convert.ToHexString(bytes).ToLower();
    }
}

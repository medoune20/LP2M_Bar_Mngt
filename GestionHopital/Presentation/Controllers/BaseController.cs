using GestionHopital.Domaine;
using GestionHopital.Infrastructure.Donnees;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace GestionHopital.Presentation.Controllers;

public class BaseController : Controller
{
    protected readonly AppDbContext _db;

    public BaseController(AppDbContext db) { _db = db; }

    protected int UtilisateurId => HttpContext.Session.GetInt32("UtilisateurId") ?? 0;
    protected string UtilisateurNom => HttpContext.Session.GetString("UtilisateurNom") ?? "";
    protected string UtilisateurLogin => HttpContext.Session.GetString("UtilisateurLogin") ?? "";
    protected RoleUtilisateur Role => (RoleUtilisateur)(HttpContext.Session.GetInt32("Role") ?? 0);
    protected bool EstConnecté => UtilisateurId > 0;
    protected bool EstAdmin => Role is RoleUtilisateur.SuperAdmin or RoleUtilisateur.Administrateur;
    protected bool EstMédecin => Role == RoleUtilisateur.Médecin;
    protected int? MedecinId => HttpContext.Session.GetInt32("MedecinId");

    public override void OnActionExecuting(ActionExecutingContext ctx)
    {
        base.OnActionExecuting(ctx);
        var action = ctx.ActionDescriptor.RouteValues["action"] ?? "";
        var ctrl = ctx.ActionDescriptor.RouteValues["controller"] ?? "";

        if (ctrl == "Auth") return;

        if (!EstConnecté)
        {
            ctx.Result = RedirectToAction("Connexion", "Auth");
            return;
        }
        ViewBag.UtilisateurNom = UtilisateurNom;
        ViewBag.UtilisateurLogin = UtilisateurLogin;
        ViewBag.Role = Role;
        ViewBag.EstAdmin = EstAdmin;
    }

    protected void Succès(string msg) => TempData["Succès"] = msg;
    protected void Erreur(string msg) => TempData["Erreur"] = msg;
    protected void Avertissement(string msg) => TempData["Avertissement"] = msg;

    protected string GénérerNuméro(string préfixe)
    {
        var now = DateTime.Now;
        return $"{préfixe}-{now:yyyy}-{now:MMddHHmmss}";
    }
}

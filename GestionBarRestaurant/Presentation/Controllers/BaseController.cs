using Infrastructure.Donnees;
using Microsoft.AspNetCore.Mvc;

namespace Presentation.Controllers;

public abstract class BaseController : Controller
{
    protected int TenantId => HttpContext.Session.GetInt32("TenantId") ?? 1;
    protected int UtilisateurId => HttpContext.Session.GetInt32("UtilisateurId") ?? 0;
    protected int RoleId => HttpContext.Session.GetInt32("UtilisateurRole") ?? 0;
    protected string TenantNom => HttpContext.Session.GetString("TenantNom") ?? "Tenant";
    protected string UtilisateurNom => HttpContext.Session.GetString("UtilisateurNom") ?? "Utilisateur";
    protected bool IsSuperAdmin => HttpContext.Session.GetString("IsSuperAdmin") == "true";

    // Cloisonnement : Administrateur(1) et Manager(3) voient tout le périmètre du tenant ;
    // le Caissier(2) ne voit que sa propre activité.
    protected bool EstAdmin => RoleId == 1;
    protected bool EstManager => RoleId == 3;
    protected bool EstCaissier => RoleId == 2;
    protected bool VoitToutLeTenant => EstAdmin || EstManager || IsSuperAdmin;

    protected string Permissions => HttpContext.Session.GetString("Permissions") ?? "";
    protected bool AProfilAcces => (HttpContext.Session.GetInt32("ProfilAccesId") ?? 0) > 0;
    protected bool PeutConsulter(string module) => Domaine.ModulesApp.Autorise(IsSuperAdmin, RoleId, Permissions, AProfilAcces, module, false);
    protected bool PeutModifier(string module) => Domaine.ModulesApp.Autorise(IsSuperAdmin, RoleId, Permissions, AProfilAcces, module, true);

    protected void RafraichirTenantSession(AppDbContext db, int tenantId)
    {
        var tenant = db.Tenants.FirstOrDefault(t => t.Id == tenantId);
        if (tenant != null)
        {
            HttpContext.Session.SetInt32("TenantId", tenant.Id);
            HttpContext.Session.SetString("TenantNom", tenant.Nom);
            HttpContext.Session.SetString("TenantCode", tenant.Code);
            HttpContext.Session.SetString("TenantColor", tenant.CouleurPrincipale);
        }
    }
}

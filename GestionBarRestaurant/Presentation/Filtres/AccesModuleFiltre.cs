using Domaine;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Presentation.Filtres;

/// <summary>
/// Filtre global : applique les droits par module (profil/rôle).
/// GET = consultation, POST = modification. Le super admin a tout.
/// </summary>
public class AccesModuleFiltre : IActionFilter
{
    private static readonly Dictionary<string, string> MapControleur = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Accueil", "dashboard" },
        { "Produit", "produits" },
        { "Categorie", "categories" },
        { "Stock", "stock" },
        { "Client", "clients" },
        { "Fidelite", "fidelite" },
        { "Caisse", "caisse" },
        { "Depense", "depenses" },
        { "Rapport", "rapports" },
        { "Comptabilite", "comptabilite" },
        { "Salle", "restaurant" },
        { "Prevision", "previsions" },
        { "EvaluationCaissier", "evaluation_caissiers" },
        { "Utilisateur", "utilisateurs" },
        { "Profil", "profils" },
        { "Tenant", "tenants" },
        { "Sauvegarde", "sauvegarde" }
    };

    public void OnActionExecuting(ActionExecutingContext context)
    {
        var ctrl = context.RouteData.Values["controller"] as string;
        var action = context.RouteData.Values["action"] as string;
        if (ctrl == null) return;

        var module = ModulePour(ctrl, action);
        if (module == null) return;

        var s = context.HttpContext.Session;
        var role = s.GetInt32("UtilisateurRole");
        if (role == null) return;

        var superAdmin = s.GetString("IsSuperAdmin") == "true";
        var perms = s.GetString("Permissions") ?? string.Empty;
        var aProfil = (s.GetInt32("ProfilAccesId") ?? 0) > 0;
        var ecriture = string.Equals(context.HttpContext.Request.Method, "POST", StringComparison.OrdinalIgnoreCase);

        if (!ModulesApp.Autorise(superAdmin, role.Value, perms, aProfil, module, ecriture))
        {
            context.HttpContext.Session.SetString("AccesRefuse", $"Accès non autorisé au module « {module} ».");
            context.Result = new RedirectToActionResult("Index", "Accueil", null);
        }
    }

    private static string? ModulePour(string ctrl, string? action)
    {
        if (string.Equals(ctrl, "Vente", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(action, "Rapide", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(action, "CreerClientRapide", StringComparison.OrdinalIgnoreCase)
                ? "caisse_rapide"
                : "ventes";
        }

        if (MapControleur.TryGetValue(ctrl, out var module)) return module;
        return null;
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}

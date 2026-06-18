using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Presentation.Filtres;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AutorisationFiltre : Attribute, IAuthorizationFilter
{
    public int[]? Roles { get; }

    public AutorisationFiltre()
    {
    }

    public AutorisationFiltre(params int[] roles)
    {
        Roles = roles;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        int? roleUtilisateur = context.HttpContext.Session.GetInt32("UtilisateurRole");

        if (!roleUtilisateur.HasValue)
        {
            context.Result = new RedirectToActionResult("Connexion", "Auth", null);
            return;
        }

        if (Roles is { Length: > 0 } && !Roles.Contains(roleUtilisateur.Value))
        {
            context.Result = new RedirectToActionResult("Index", "Accueil", null);
        }
    }
}

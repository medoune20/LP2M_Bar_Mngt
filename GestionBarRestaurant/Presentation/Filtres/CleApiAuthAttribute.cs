using Domaine.Models;
using Infrastructure.Donnees;
using Infrastructure.Securite;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Presentation.Filtres;

/// <summary>
/// Authentifie un appel d'API via l'en-tête <c>X-API-Key</c>. En cas de succès,
/// l'identifiant du tenant est placé dans <c>HttpContext.Items["TenantId"]</c>.
/// </summary>
public sealed class CleApiAuthAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var entete = context.HttpContext.Request.Headers["X-API-Key"].ToString();
        if (string.IsNullOrWhiteSpace(entete))
        {
            context.Result = new UnauthorizedObjectResult(new { erreur = "Clé API manquante (en-tête X-API-Key)." });
            return;
        }

        var hash = CleApiHelper.Hash(entete.Trim());
        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var cle = db.Set<CleApi>().FirstOrDefault(c => c.Actif && c.CleHash == hash);
        if (cle == null)
        {
            context.Result = new UnauthorizedObjectResult(new { erreur = "Clé API invalide ou révoquée." });
            return;
        }

        cle.DerniereUtilisation = DateTime.Now;
        db.SaveChanges();
        context.HttpContext.Items["TenantId"] = cle.TenantId;
    }
}

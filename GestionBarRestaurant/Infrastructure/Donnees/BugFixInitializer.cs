using Domaine;
using Domaine.Models;

namespace Infrastructure.Donnees;

/// <summary>
/// Correctifs idempotents appliqués au démarrage.
/// Objectif : réparer les données existantes sans casser les bases SQLite déjà en production.
/// </summary>
public static class BugFixInitializer
{
    public static void Appliquer(AppDbContext db)
    {
        CorrigerPermissionsProfils(db);
        CreerTablesRestaurantParDefaut(db);
    }

    private static void CorrigerPermissionsProfils(AppDbContext db)
    {
        var profils = db.ProfilsAcces.ToList();
        var modifie = false;

        foreach (var profil in profils)
        {
            var role = DatabaseInitializer.RoleDepuisProfil(profil.Nom);
            var permissionsReference = ModulesApp.PermissionsPourRole(role);
            var permissionsCorrigees = FusionnerPermissions(profil.Permissions, permissionsReference);

            if (!string.Equals(profil.Permissions, permissionsCorrigees, StringComparison.Ordinal))
            {
                profil.Permissions = permissionsCorrigees;
                profil.Actif = true;
                modifie = true;
            }
        }

        if (modifie)
        {
            db.SaveChanges();
        }
    }

    private static string FusionnerPermissions(string? permissionsExistantes, string permissionsReference)
    {
        var droits = ParsePermissions(permissionsExistantes);
        var reference = ParsePermissions(permissionsReference);

        foreach (var item in reference)
        {
            if (!droits.ContainsKey(item.Key))
            {
                droits[item.Key] = item.Value;
            }
        }

        return string.Join(';', droits.Select(kv => $"{kv.Key}={kv.Value}"));
    }

    private static Dictionary<string, string> ParsePermissions(string? permissions)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(permissions)) return result;

        foreach (var part in permissions.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = part.Split('=', 2, StringSplitOptions.TrimEntries);
            if (kv.Length == 2 && !string.IsNullOrWhiteSpace(kv[0]) && !string.IsNullOrWhiteSpace(kv[1]))
            {
                result[kv[0]] = kv[1].ToUpperInvariant();
            }
        }

        return result;
    }

    private static void CreerTablesRestaurantParDefaut(AppDbContext db)
    {
        var tenants = db.Tenants.Where(t => t.Actif).Select(t => t.Id).ToList();
        var modifie = false;

        foreach (var tenantId in tenants)
        {
            if (db.Tables.Any(t => t.TenantId == tenantId))
            {
                continue;
            }

            db.Tables.AddRange(TablesParDefaut(tenantId));
            modifie = true;
        }

        if (modifie)
        {
            db.SaveChanges();
        }
    }

    private static IEnumerable<TableResto> TablesParDefaut(int tenantId)
    {
        var ordre = 1;

        foreach (var nom in new[] { "T1", "T2", "T3", "T4", "T5", "T6" })
        {
            yield return new TableResto
            {
                TenantId = tenantId,
                Nom = nom,
                Zone = "Salle",
                Capacite = 4,
                Ordre = ordre++,
                Actif = true
            };
        }

        foreach (var nom in new[] { "Terrasse 1", "Terrasse 2", "Terrasse 3", "Terrasse 4" })
        {
            yield return new TableResto
            {
                TenantId = tenantId,
                Nom = nom,
                Zone = "Terrasse",
                Capacite = 4,
                Ordre = ordre++,
                Actif = true
            };
        }

        foreach (var nom in new[] { "VIP 1", "VIP 2" })
        {
            yield return new TableResto
            {
                TenantId = tenantId,
                Nom = nom,
                Zone = "VIP",
                Capacite = 6,
                Ordre = ordre++,
                Actif = true
            };
        }
    }
}

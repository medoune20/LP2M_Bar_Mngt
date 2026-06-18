namespace Domaine;

/// <summary>
/// Liste centrale des modules et logique d'autorisation par profil/rôle.
/// Dans l'interface, le profil et le rôle métier sont alignés :
/// Administrateur, Manager, Caissier. Les permissions sont stockées sous la forme
/// "module=CM" (C = consulter, M = modifier ; M implique la consultation).
/// </summary>
public static class ModulesApp
{
    public static readonly (string Cle, string Nom)[] Liste = new[]
    {
        ("dashboard",          "Tableau de bord"),
        ("caisse_rapide",      "Caisse rapide POS"),
        ("ventes",             "Ventes / tickets"),
        ("caisse",             "Sessions de caisse"),
        ("produits",           "Produits"),
        ("categories",         "Catégories"),
        ("stock",              "Stock / inventaire"),
        ("clients",            "Clients"),
        ("fidelite",           "Fidélité"),
        ("depenses",           "Dépenses"),
        ("rapports",           "Rapports"),
        ("previsions",         "Prévisions"),
        ("evaluation_caissiers","Évaluation caissiers"),
        ("utilisateurs",       "Utilisateurs"),
        ("profils",            "Profils / rôles"),
        ("tenants",            "Tenants / établissements"),
        ("sauvegarde",         "Sauvegarde / restauration"),
        ("parametres",         "Paramètres établissement")
    };

    public const string PermissionsAdministrateur =
        "dashboard=CM;caisse_rapide=CM;ventes=CM;caisse=CM;produits=CM;categories=CM;stock=CM;clients=CM;fidelite=CM;depenses=CM;rapports=CM;previsions=CM;evaluation_caissiers=CM;utilisateurs=CM;profils=CM;tenants=CM;sauvegarde=CM;parametres=CM";

    public const string PermissionsManager =
        "dashboard=C;caisse_rapide=CM;ventes=CM;caisse=CM;produits=CM;categories=CM;stock=CM;clients=CM;fidelite=CM;depenses=CM;rapports=C;previsions=C;evaluation_caissiers=C;parametres=C";

    public const string PermissionsCaissier =
        "dashboard=C;caisse_rapide=CM;ventes=C;caisse=C;produits=C;categories=C;stock=C;clients=C;fidelite=C";

    public static string PermissionsPourRole(RoleUtilisateur role) => role switch
    {
        RoleUtilisateur.Administrateur => PermissionsAdministrateur,
        RoleUtilisateur.Manager => PermissionsManager,
        RoleUtilisateur.Caissier => PermissionsCaissier,
        _ => string.Empty
    };

    private static string Flags(string? perms, string module)
    {
        if (string.IsNullOrWhiteSpace(perms)) return string.Empty;
        foreach (var part in perms.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2, StringSplitOptions.TrimEntries);
            if (kv.Length == 2 && string.Equals(kv[0], module, StringComparison.OrdinalIgnoreCase))
                return kv[1].ToUpperInvariant();
        }
        return string.Empty;
    }

    public static bool PeutConsulter(string? perms, string module)
    {
        var f = Flags(perms, module);
        return f.Contains('C') || f.Contains('M');
    }

    public static bool PeutModifier(string? perms, string module)
        => Flags(perms, module).Contains('M');

    /// <summary>
    /// Accès effectif. Le super admin garde tout. Les profils système alignés sur
    /// les rôles remplacent les anciens droits implicites afin d'éviter les écarts
    /// entre "rôle" et "profil".
    /// </summary>
    public static bool Autorise(bool superAdmin, int role, string? perms, bool aProfil, string module, bool ecriture)
    {
        if (superAdmin) return true;

        var droits = aProfil && !string.IsNullOrWhiteSpace(perms)
            ? perms
            : PermissionsPourRole((RoleUtilisateur)role);

        return ecriture ? PeutModifier(droits, module) : PeutConsulter(droits, module);
    }
}

namespace Domaine;

/// <summary>
/// Salons (canaux) de la messagerie interne. Chaque salon est accessible à un
/// sous-ensemble de rôles. Le cloisonnement par établissement (TenantId) reste
/// toujours appliqué en plus du salon : un salon n'agrège jamais deux tenants.
///
/// Rôles : 1 = Administrateur, 2 = Caissier, 3 = Manager.
/// </summary>
public static class SalonsChat
{
    public sealed record Salon(string Cle, string Nom, string Icone, int[] Roles);

    public static readonly Salon[] Liste =
    {
        new("general",     "Général",     "bi-chat-dots",  new[] { 1, 2, 3 }),
        new("encadrement", "Encadrement", "bi-shield-lock", new[] { 1, 3 }),
        new("caisse",      "Salle & Caisse", "bi-cash-coin", new[] { 1, 2, 3 })
    };

    public static Salon? Trouver(string? cle) =>
        Liste.FirstOrDefault(s => string.Equals(s.Cle, cle, StringComparison.OrdinalIgnoreCase));

    public static string Normaliser(string? cle) => Trouver(cle)?.Cle ?? "general";

    public static bool Accessible(bool superAdmin, int role, string? cle)
    {
        var salon = Trouver(cle);
        if (salon == null) return false;
        return superAdmin || salon.Roles.Contains(role);
    }

    public static IEnumerable<Salon> Accessibles(bool superAdmin, int role) =>
        Liste.Where(s => superAdmin || s.Roles.Contains(role));

    /// <summary>Clés des salons accessibles, pour filtrer les requêtes SQL.</summary>
    public static string[] ClesAccessibles(bool superAdmin, int role) =>
        Accessibles(superAdmin, role).Select(s => s.Cle).ToArray();
}

namespace Domaine;

/// <summary>
/// Référentiel minimal du plan comptable SYSCOHADA (révisé) utilisé par
/// l'application : intitulés des comptes mobilisés par le moteur d'écritures.
/// </summary>
public static class ComptabiliteOhada
{
    public static readonly Dictionary<string, string> Comptes = new()
    {
        ["401"] = "Fournisseurs",
        ["411"] = "Clients",
        ["4431"] = "État, TVA facturée (collectée)",
        ["4452"] = "État, TVA récupérable (déductible)",
        ["4711"] = "Apports / opérations diverses",
        ["521"] = "Banques",
        ["571"] = "Caisse",
        ["601"] = "Achats de marchandises",
        ["627"] = "Services extérieurs / charges diverses",
        ["701"] = "Ventes de marchandises",
        ["706"] = "Services vendus",
    };

    public static string Intitule(string compte)
        => Comptes.TryGetValue(compte ?? string.Empty, out var v) ? v : (compte ?? string.Empty);

    public static readonly (string Code, string Libelle)[] Journaux =
    {
        ("VT", "Journal des ventes"),
        ("AC", "Journal des achats"),
        ("CD", "Journal de caisse (dépenses)"),
        ("TR", "Journal de trésorerie"),
        ("OD", "Opérations diverses"),
    };
}

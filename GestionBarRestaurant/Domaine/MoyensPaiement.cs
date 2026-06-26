namespace Domaine;

public enum CategoriePaiement
{
    Especes = 1,
    Mobile = 2,
    Carte = 3,
    Credit = 4
}

/// <summary>
/// Moyens de paiement standard (Côte d'Ivoire) et classification. La
/// classification sert au rapprochement de caisse et au mapping comptable
/// (espèces → caisse, mobile money → compte dédié, carte → banque, crédit → clients).
/// </summary>
public static class MoyensPaiement
{
    public sealed record Moyen(string Cle, string Libelle, CategoriePaiement Categorie, string Icone);

    public static readonly Moyen[] Liste =
    {
        new("Espèces", "Espèces", CategoriePaiement.Especes, "bi-cash-stack"),
        new("Wave", "Wave", CategoriePaiement.Mobile, "bi-phone"),
        new("Orange Money", "Orange Money", CategoriePaiement.Mobile, "bi-phone"),
        new("MTN MoMo", "MTN MoMo", CategoriePaiement.Mobile, "bi-phone"),
        new("Moov Money", "Moov Money", CategoriePaiement.Mobile, "bi-phone"),
        new("Carte bancaire", "Carte bancaire", CategoriePaiement.Carte, "bi-credit-card"),
        new("Crédit", "Crédit", CategoriePaiement.Credit, "bi-journal-text"),
    };

    public static CategoriePaiement Categoriser(string? mode)
    {
        var m = (mode ?? string.Empty).Trim().ToLowerInvariant();
        if (m.Length == 0 || m.Contains("esp") || m.Contains("cash") || m.Contains("comptant")) return CategoriePaiement.Especes;
        if (m.Contains("crédit") || m.Contains("credit") || m.Contains("avoir")) return CategoriePaiement.Credit;
        if (m.Contains("wave") || m.Contains("orange") || m.Contains("mtn") || m.Contains("moov")
            || m.Contains("momo") || m.Contains("mobile") || m.Contains("money")) return CategoriePaiement.Mobile;
        if (m.Contains("carte") || m.Contains("card") || m.Contains("tpe") || m.Contains("visa") || m.Contains("master")) return CategoriePaiement.Carte;
        return CategoriePaiement.Especes;
    }

    public static string LibelleCategorie(CategoriePaiement c) => c switch
    {
        CategoriePaiement.Especes => "Espèces",
        CategoriePaiement.Mobile => "Mobile Money",
        CategoriePaiement.Carte => "Carte bancaire",
        CategoriePaiement.Credit => "Crédit",
        _ => "Autre"
    };
}

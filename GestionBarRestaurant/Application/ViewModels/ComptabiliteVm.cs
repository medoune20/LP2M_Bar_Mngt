using System.Globalization;
using System.Text;

namespace Application.ViewModels;

public class LigneEcritureVm
{
    public string Compte { get; set; } = string.Empty;
    public string Intitule { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}

public class EcritureVm
{
    public DateTime Date { get; set; }
    public string Journal { get; set; } = string.Empty;
    public string Piece { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public string Tiers { get; set; } = string.Empty;
    public List<LigneEcritureVm> Lignes { get; set; } = new();
}

public class BalanceLigneVm
{
    public string Compte { get; set; } = string.Empty;
    public string Intitule { get; set; } = string.Empty;
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal SoldeDebiteur => Math.Max(0, TotalDebit - TotalCredit);
    public decimal SoldeCrediteur => Math.Max(0, TotalCredit - TotalDebit);
}

/// <summary>Rapport comptable d'une période (trésorerie + écritures + balance).</summary>
public class RapportComptaVm
{
    public DateTime Du { get; set; }
    public DateTime Au { get; set; }
    public bool AssujettiTva { get; set; }
    public decimal TauxTva { get; set; }
    public string Devise { get; set; } = "XOF";

    public decimal Recettes { get; set; }
    public decimal Depenses { get; set; }
    public decimal TvaCollectee { get; set; }
    public decimal TvaDeductible { get; set; }
    public decimal Resultat { get; set; }
    public decimal SoldeTresorerie { get; set; }

    public List<EcritureVm> Ecritures { get; set; } = new();
    public List<BalanceLigneVm> Balance { get; set; } = new();
}

/// <summary>Génération des exports CSV normalisés (import comptable / ERP).</summary>
public static class ComptaCsv
{
    public static string Ecritures(RapportComptaVm r)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Date;Journal;Piece;Compte;Intitule;Libelle;Tiers;Debit;Credit");
        foreach (var e in r.Ecritures)
        {
            foreach (var l in e.Lignes)
            {
                sb.Append(e.Date.ToString("yyyy-MM-dd")).Append(';')
                  .Append(Esc(e.Journal)).Append(';')
                  .Append(Esc(e.Piece)).Append(';')
                  .Append(Esc(l.Compte)).Append(';')
                  .Append(Esc(l.Intitule)).Append(';')
                  .Append(Esc(e.Libelle)).Append(';')
                  .Append(Esc(e.Tiers)).Append(';')
                  .Append(Montant(l.Debit)).Append(';')
                  .Append(Montant(l.Credit)).Append('\n');
            }
        }
        return sb.ToString();
    }

    private static string Montant(decimal v) => v.ToString("0", CultureInfo.InvariantCulture);

    private static string Esc(string? s)
    {
        s ??= string.Empty;
        if (s.Contains(';') || s.Contains('"') || s.Contains('\n'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}

namespace Domaine.Models;

/// <summary>
/// Paramétrage comptable OHADA par établissement (tenant) : régime de TVA,
/// devise, exercice et comptes du plan comptable SYSCOHADA utilisés par le
/// moteur de génération d'écritures.
/// </summary>
public class ParametrageComptable
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    /// <summary>Établissement assujetti à la TVA (sinon impôt synthétique / TPE).</summary>
    public bool AssujettiTva { get; set; } = true;

    /// <summary>Taux de TVA en pourcentage (Côte d'Ivoire : 18).</summary>
    public decimal TauxTva { get; set; } = 18m;

    public string Devise { get; set; } = "XOF";
    public int Exercice { get; set; } = DateTime.Now.Year;

    // Comptes SYSCOHADA paramétrables.
    public string CompteCaisse { get; set; } = "571";
    public string CompteBanque { get; set; } = "521";
    public string CompteClients { get; set; } = "411";
    public string CompteFournisseurs { get; set; } = "401";
    public string CompteVentes { get; set; } = "701";
    public string CompteTvaCollectee { get; set; } = "4431";
    public string CompteTvaDeductible { get; set; } = "4452";
    public string CompteAchats { get; set; } = "601";
    public string CompteCharges { get; set; } = "627";
    public string CompteApports { get; set; } = "4711";
}

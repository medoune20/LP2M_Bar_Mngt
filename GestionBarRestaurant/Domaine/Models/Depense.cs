using System.ComponentModel.DataAnnotations;

namespace Domaine.Models;

public class Depense
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    [Required(ErrorMessage = "Le libellé est obligatoire")]
    public string Libelle { get; set; } = string.Empty;

    public string Categorie { get; set; } = "Général";

    [Range(1, double.MaxValue, ErrorMessage = "Le montant doit être supérieur à 0")]
    public decimal Montant { get; set; }

    public DateTime DateDepense { get; set; } = DateTime.Now;

    public string Beneficiaire { get; set; } = string.Empty;

    public string Commentaire { get; set; } = string.Empty;

    public string SaisiPar { get; set; } = string.Empty;

    /// <summary>Session de caisse à laquelle la dépense est rattachée (verrouillée après clôture).</summary>
    public int? CaisseSessionId { get; set; }
}

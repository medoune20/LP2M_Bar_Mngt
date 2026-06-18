using System.ComponentModel.DataAnnotations;
using Domaine;

namespace Domaine.Models;

public class Client
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    [Required(ErrorMessage = "Le nom du client est obligatoire")]
    public string Nom { get; set; } = string.Empty;

    public string Telephone { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public TypeClient TypeClient { get; set; } = TypeClient.Particulier;

    public string Adresse { get; set; } = string.Empty;

    public decimal SoldeCredit { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Le plafond de crédit doit être positif")]
    [Display(Name = "Plafond de crédit")]
    public decimal PlafondCredit { get; set; }

    public int PointsFidelite { get; set; }

    public DateTime? DerniereVisite { get; set; }

    public decimal TotalAchats { get; set; }

    public bool Actif { get; set; } = true;

    public DateTime DateCreation { get; set; } = DateTime.Now;
}

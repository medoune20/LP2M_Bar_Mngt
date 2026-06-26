using System.ComponentModel.DataAnnotations;

namespace Domaine.Models;

/// <summary>Table de salle (service restaurant), propre à un établissement.</summary>
public class TableResto
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    [Required(ErrorMessage = "Le nom de la table est obligatoire")]
    [Display(Name = "Nom / numéro")]
    public string Nom { get; set; } = string.Empty;

    [Display(Name = "Zone")]
    public string Zone { get; set; } = "Salle";

    [Display(Name = "Couverts")]
    public int Capacite { get; set; } = 4;

    public int Ordre { get; set; }
    public bool Actif { get; set; } = true;
}

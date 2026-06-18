using System.ComponentModel.DataAnnotations;

namespace Domaine.Models;

public class Categorie
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    [Required(ErrorMessage = "Le nom de la catégorie est obligatoire")]
    [Display(Name = "Nom de la catégorie")]
    public string Nom { get; set; } = string.Empty;

    [Display(Name = "Ordre d'affichage")]
    public int Ordre { get; set; } = 0;

    [Display(Name = "Couleur")]
    public string Couleur { get; set; } = "#165DFF";

    public bool Actif { get; set; } = true;
}

using System.ComponentModel.DataAnnotations;

namespace Domaine.Models;

public class RegleFidelite
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    [Display(Name = "Nom du programme")]
    public string NomProgramme { get; set; } = "Programme fidélité";

    [Display(Name = "Montant pour 1 point")]
    public decimal MontantPourUnPoint { get; set; } = 1000;

    [Display(Name = "Valeur d'un point")]
    public decimal ValeurPoint { get; set; } = 10;

    [Display(Name = "Seuil minimum d'utilisation")]
    public int SeuilUtilisationPoints { get; set; } = 100;

    public bool Actif { get; set; } = true;
}

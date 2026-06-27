using System.ComponentModel.DataAnnotations;

namespace GestionHopital.Domaine.Models;

public class Etablissement
{
    public int Id { get; set; }
    [Required, MaxLength(200)]
    public string Nom { get; set; } = "";
    [MaxLength(200)]
    public string? Adresse { get; set; }
    [MaxLength(50)]
    public string? Téléphone { get; set; }
    [MaxLength(200)]
    public string? Email { get; set; }
    [MaxLength(200)]
    public string? SiteWeb { get; set; }
    [MaxLength(200)]
    public string? Directeur { get; set; }
    [MaxLength(50)]
    public string? Couleur { get; set; }
    [MaxLength(500)]
    public string? Logo { get; set; }
    public DateTime DateCréation { get; set; } = DateTime.Now;
}

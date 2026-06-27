using System.ComponentModel.DataAnnotations;

namespace GestionHopital.Domaine.Models;

public class Hospitalisation
{
    public int Id { get; set; }
    [Required, MaxLength(20)]
    public string NuméroHospitalisation { get; set; } = "";
    [Required]
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }
    [Required]
    public int DépartementId { get; set; }
    public Departement? Departement { get; set; }
    [Required]
    public int LitId { get; set; }
    public LitHospitalisation? Lit { get; set; }
    [Required]
    public int MedecinRéférentId { get; set; }
    public Medecin? MedecinRéférent { get; set; }
    [Required]
    public DateTime DateAdmission { get; set; } = DateTime.Now;
    public DateTime? DateSortie { get; set; }
    [Required, MaxLength(500)]
    public string MotifAdmission { get; set; } = "";
    [MaxLength(500)]
    public string? DiagnosticFinal { get; set; }
    [MaxLength(2000)]
    public string? CompteRendu { get; set; }
    [MaxLength(500)]
    public string? ConditionsSortie { get; set; }
    public StatutHospitalisation Statut { get; set; } = StatutHospitalisation.EnCours;

    public TimeSpan? Durée => DateSortie.HasValue ? DateSortie.Value - DateAdmission : DateTime.Now - DateAdmission;
    public int DuréeJours => (int)(Durée?.TotalDays ?? 0);

    public string StatutCssClass => Statut switch
    {
        StatutHospitalisation.EnCours => "primary",
        StatutHospitalisation.Sorti => "success",
        StatutHospitalisation.Transféré => "warning",
        _ => "secondary"
    };
}

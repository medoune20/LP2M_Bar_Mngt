using System.ComponentModel.DataAnnotations;

namespace GestionHopital.Domaine.Models;

public class Examen
{
    public int Id { get; set; }
    [Required, MaxLength(20)]
    public string NuméroExamen { get; set; } = "";
    [Required]
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }
    public int? ConsultationId { get; set; }
    public Consultation? Consultation { get; set; }
    [Required]
    public int MedecinPrescripteurId { get; set; }
    public Medecin? MedecinPrescripteur { get; set; }
    public TypeExamen Type { get; set; } = TypeExamen.Biologie;
    [Required, MaxLength(300)]
    public string Description { get; set; } = "";
    [MaxLength(500)]
    public string? Instructions { get; set; }
    public StatutExamen Statut { get; set; } = StatutExamen.Prescrit;
    public DateTime DatePrescription { get; set; } = DateTime.Now;
    public DateTime? DateRéalisation { get; set; }

    public ResultatExamen? Résultat { get; set; }

    public string StatutCssClass => Statut switch
    {
        StatutExamen.Prescrit => "info",
        StatutExamen.EnAttente => "warning",
        StatutExamen.EnCours => "primary",
        StatutExamen.Résultats => "success",
        StatutExamen.Annulé => "danger",
        _ => "secondary"
    };
    public string TypeIcone => Type switch
    {
        TypeExamen.Biologie => "bi-droplet",
        TypeExamen.Radiologie => "bi-lungs",
        TypeExamen.Échographie => "bi-broadcast",
        TypeExamen.Scanner => "bi-cpu",
        TypeExamen.IRM => "bi-magnet",
        TypeExamen.Électrocardiogramme => "bi-activity",
        _ => "bi-clipboard2-pulse"
    };
}

public class ResultatExamen
{
    public int Id { get; set; }
    [Required]
    public int ExamenId { get; set; }
    public Examen? Examen { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;
    [Required, MaxLength(2000)]
    public string Résultat { get; set; } = "";
    [MaxLength(1000)]
    public string? Observations { get; set; }
    [MaxLength(100)]
    public string? Technicien { get; set; }
    [MaxLength(200)]
    public string? FichierJoint { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace GestionHopital.Domaine.Models;

public class RendezVous
{
    public int Id { get; set; }
    [Required, MaxLength(20)]
    public string NuméroRDV { get; set; } = "";
    [Required]
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }
    [Required]
    public int MedecinId { get; set; }
    public Medecin? Medecin { get; set; }
    [Required]
    public DateTime DateHeure { get; set; }
    public int Durée { get; set; } = 30;   // minutes
    [Required, MaxLength(300)]
    public string Motif { get; set; } = "";
    public TypeConsultation TypeConsultation { get; set; } = TypeConsultation.Générale;
    public StatutRendezVous Statut { get; set; } = StatutRendezVous.Planifié;
    [MaxLength(500)]
    public string? Notes { get; set; }
    public DateTime DateCréation { get; set; } = DateTime.Now;
    public int? ConsultationId { get; set; }
    public Consultation? Consultation { get; set; }

    public string StatutCssClass => Statut switch
    {
        StatutRendezVous.Planifié => "info",
        StatutRendezVous.Confirmé => "primary",
        StatutRendezVous.EnCours => "warning",
        StatutRendezVous.Terminé => "success",
        StatutRendezVous.Annulé => "danger",
        StatutRendezVous.NoShow => "secondary",
        _ => "secondary"
    };
    public string StatutIcone => Statut switch
    {
        StatutRendezVous.Planifié => "bi-calendar-event",
        StatutRendezVous.Confirmé => "bi-calendar-check",
        StatutRendezVous.EnCours => "bi-arrow-repeat",
        StatutRendezVous.Terminé => "bi-check-circle",
        StatutRendezVous.Annulé => "bi-x-circle",
        StatutRendezVous.NoShow => "bi-person-x",
        _ => "bi-calendar"
    };
}

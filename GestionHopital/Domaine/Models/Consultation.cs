using System.ComponentModel.DataAnnotations;

namespace GestionHopital.Domaine.Models;

public class Consultation
{
    public int Id { get; set; }
    public int? RendezVousId { get; set; }
    public RendezVous? RendezVous { get; set; }
    [Required]
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }
    [Required]
    public int MedecinId { get; set; }
    public Medecin? Medecin { get; set; }
    [Required]
    public DateTime DateHeure { get; set; } = DateTime.Now;
    public TypeConsultation Type { get; set; } = TypeConsultation.Générale;
    [Required, MaxLength(300)]
    public string Motif { get; set; } = "";
    [MaxLength(1000)]
    public string? Anamnèse { get; set; }
    [MaxLength(1000)]
    public string? ExamenClinique { get; set; }
    [MaxLength(500)]
    public string? Diagnostic { get; set; }
    [MaxLength(1000)]
    public string? PlanTraitement { get; set; }
    [MaxLength(1000)]
    public string? NotesConclusion { get; set; }
    // Signes vitaux
    public decimal? Poids { get; set; }
    public decimal? Taille { get; set; }
    [MaxLength(20)]
    public string? TensionArtérielle { get; set; }
    public decimal? Température { get; set; }
    public int? Pouls { get; set; }
    public int? Saturation { get; set; }
    public int? Glycémie { get; set; }
    public bool EstUrgence { get; set; } = false;

    public ICollection<Ordonnance> Ordonnances { get; set; } = [];
    public ICollection<Examen> Examens { get; set; } = [];

    public decimal? IMC => (Poids.HasValue && Taille.HasValue && Taille > 0)
        ? Math.Round(Poids.Value / ((Taille.Value / 100) * (Taille.Value / 100)), 1)
        : null;
}

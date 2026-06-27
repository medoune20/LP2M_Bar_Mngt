using System.ComponentModel.DataAnnotations;

namespace GestionHopital.Domaine.Models;

public class Ordonnance
{
    public int Id { get; set; }
    [Required]
    public int ConsultationId { get; set; }
    public Consultation? Consultation { get; set; }
    [Required]
    public int MedecinId { get; set; }
    public Medecin? Medecin { get; set; }
    [Required]
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;
    [MaxLength(500)]
    public string? Instructions { get; set; }
    public bool EstValide { get; set; } = true;

    public ICollection<LigneOrdonnance> Lignes { get; set; } = [];
}

public class LigneOrdonnance
{
    public int Id { get; set; }
    [Required]
    public int OrdonnanceId { get; set; }
    public Ordonnance? Ordonnance { get; set; }
    [Required, MaxLength(200)]
    public string Médicament { get; set; } = "";
    [Required, MaxLength(100)]
    public string Dosage { get; set; } = "";
    [Required, MaxLength(100)]
    public string Fréquence { get; set; } = "";
    [Required, MaxLength(50)]
    public string Durée { get; set; } = "";
    [MaxLength(300)]
    public string? Instructions { get; set; }
    public int Ordre { get; set; } = 1;
}

using System.ComponentModel.DataAnnotations;

namespace GestionHopital.Domaine.Models;

public class Medecin
{
    public int Id { get; set; }
    [Required, MaxLength(100)]
    public string Nom { get; set; } = "";
    [Required, MaxLength(100)]
    public string Prénom { get; set; } = "";
    [Required, MaxLength(150)]
    public string Spécialité { get; set; } = "";
    public int DépartementId { get; set; }
    public Departement? Departement { get; set; }
    [MaxLength(50)]
    public string? Téléphone { get; set; }
    [MaxLength(200)]
    public string? Email { get; set; }
    [MaxLength(50)]
    public string? NuméroOrdre { get; set; }
    public bool Actif { get; set; } = true;
    public DateTime DateCréation { get; set; } = DateTime.Now;

    public ICollection<RendezVous> RendezVous { get; set; } = [];
    public ICollection<Consultation> Consultations { get; set; } = [];
    public ICollection<Hospitalisation> Hospitalisations { get; set; } = [];

    public string NomComplet => $"Dr {Prénom} {Nom}";
    public string Titre => $"Dr {Prénom} {Nom} — {Spécialité}";
}

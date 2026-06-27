using System.ComponentModel.DataAnnotations;

namespace GestionHopital.Domaine.Models;

public class Departement
{
    public int Id { get; set; }
    [Required, MaxLength(150)]
    public string Nom { get; set; } = "";
    [MaxLength(500)]
    public string? Description { get; set; }
    [MaxLength(100)]
    public string? Localisation { get; set; }
    public int CapacitéLits { get; set; } = 0;
    [MaxLength(100)]
    public string? ChefService { get; set; }
    [MaxLength(50)]
    public string? Téléphone { get; set; }
    public bool Actif { get; set; } = true;
    public DateTime DateCréation { get; set; } = DateTime.Now;

    public ICollection<Medecin> Medecins { get; set; } = [];
    public ICollection<LitHospitalisation> Lits { get; set; } = [];
    public ICollection<Hospitalisation> Hospitalisations { get; set; } = [];

    public int LitsLibres => Lits.Count(l => l.Statut == StatutLit.Libre);
    public int LitsOccupés => Lits.Count(l => l.Statut == StatutLit.Occupé);
}

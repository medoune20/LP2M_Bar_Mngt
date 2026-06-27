using System.ComponentModel.DataAnnotations;

namespace GestionHopital.Domaine.Models;

public class Patient
{
    public int Id { get; set; }
    [Required, MaxLength(20)]
    public string NuméroDossier { get; set; } = "";   // IPP auto-généré
    [Required, MaxLength(100)]
    public string Nom { get; set; } = "";
    [Required, MaxLength(100)]
    public string Prénom { get; set; } = "";
    [Required]
    public DateTime DateNaissance { get; set; }
    public SexePatient Sexe { get; set; } = SexePatient.Masculin;
    public GroupeSanguin GroupeSanguin { get; set; } = GroupeSanguin.Inconnu;
    [MaxLength(300)]
    public string? Adresse { get; set; }
    [MaxLength(50)]
    public string? Téléphone { get; set; }
    [MaxLength(200)]
    public string? Email { get; set; }
    [MaxLength(150)]
    public string? NomContactUrgence { get; set; }
    [MaxLength(50)]
    public string? TéléphoneUrgence { get; set; }
    [MaxLength(500)]
    public string? Allergies { get; set; }
    [MaxLength(1000)]
    public string? Antécédents { get; set; }
    [MaxLength(50)]
    public string? NuméroSécuritéSociale { get; set; }
    [MaxLength(100)]
    public string? Mutuelle { get; set; }
    public StatutPatient Statut { get; set; } = StatutPatient.Actif;
    public DateTime DateEnregistrement { get; set; } = DateTime.Now;

    public ICollection<RendezVous> RendezVous { get; set; } = [];
    public ICollection<Consultation> Consultations { get; set; } = [];
    public ICollection<Examen> Examens { get; set; } = [];
    public ICollection<Hospitalisation> Hospitalisations { get; set; } = [];

    public string NomComplet => $"{Prénom} {Nom}";
    public int Âge => (int)((DateTime.Now - DateNaissance).TotalDays / 365.25);
    public string GroupeSanguinLibellé => GroupeSanguin switch
    {
        GroupeSanguin.A_pos => "A+",
        GroupeSanguin.A_neg => "A−",
        GroupeSanguin.B_pos => "B+",
        GroupeSanguin.B_neg => "B−",
        GroupeSanguin.AB_pos => "AB+",
        GroupeSanguin.AB_neg => "AB−",
        GroupeSanguin.O_pos => "O+",
        GroupeSanguin.O_neg => "O−",
        _ => "Inconnu"
    };
    public string StatutCssClass => Statut switch
    {
        StatutPatient.Actif => "success",
        StatutPatient.Hospitalisé => "primary",
        StatutPatient.Sorti => "secondary",
        StatutPatient.Transféré => "warning",
        StatutPatient.Décédé => "danger",
        _ => "secondary"
    };
}

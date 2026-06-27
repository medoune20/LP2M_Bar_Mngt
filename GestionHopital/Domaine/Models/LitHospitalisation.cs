using System.ComponentModel.DataAnnotations;

namespace GestionHopital.Domaine.Models;

public class LitHospitalisation
{
    public int Id { get; set; }
    [Required, MaxLength(20)]
    public string Numéro { get; set; } = "";
    [Required]
    public int DépartementId { get; set; }
    public Departement? Departement { get; set; }
    public StatutLit Statut { get; set; } = StatutLit.Libre;
    [MaxLength(300)]
    public string? Notes { get; set; }

    public ICollection<Hospitalisation> Hospitalisations { get; set; } = [];

    public string StatutCssClass => Statut switch
    {
        StatutLit.Libre => "success",
        StatutLit.Occupé => "danger",
        StatutLit.Maintenance => "warning",
        StatutLit.Réservé => "info",
        _ => "secondary"
    };
    public string StatutIcone => Statut switch
    {
        StatutLit.Libre => "bi-check-circle",
        StatutLit.Occupé => "bi-person-fill",
        StatutLit.Maintenance => "bi-wrench",
        StatutLit.Réservé => "bi-bookmark",
        _ => "bi-question"
    };
}

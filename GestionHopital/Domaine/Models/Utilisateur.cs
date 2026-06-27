using System.ComponentModel.DataAnnotations;

namespace GestionHopital.Domaine.Models;

public class Utilisateur
{
    public int Id { get; set; }
    [Required, MaxLength(100)]
    public string Login { get; set; } = "";
    [Required, MaxLength(200)]
    public string MotDePasse { get; set; } = "";
    [Required, MaxLength(100)]
    public string Nom { get; set; } = "";
    [Required, MaxLength(100)]
    public string Prénom { get; set; } = "";
    [MaxLength(200)]
    public string? Email { get; set; }
    public RoleUtilisateur Role { get; set; } = RoleUtilisateur.Réceptionniste;
    public int? MedecinId { get; set; }
    public bool Actif { get; set; } = true;
    public DateTime DateCréation { get; set; } = DateTime.Now;
    public DateTime? DernièreConnexion { get; set; }

    public string NomComplet => $"{Prénom} {Nom}";
    public string RoleLibellé => Role switch
    {
        RoleUtilisateur.SuperAdmin => "Super Admin",
        RoleUtilisateur.Administrateur => "Administrateur",
        RoleUtilisateur.Médecin => "Médecin",
        RoleUtilisateur.Infirmier => "Infirmier(e)",
        RoleUtilisateur.Réceptionniste => "Réceptionniste",
        RoleUtilisateur.Laborantin => "Laborantin",
        _ => Role.ToString()
    };
}

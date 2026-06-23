using System.ComponentModel.DataAnnotations;
using Domaine;

namespace Domaine.Models;

public class Utilisateur
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    [Required(ErrorMessage = "Le nom est obligatoire")]
    public string Nom { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le login est obligatoire")]
    public string Login { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    public string MotDePasse { get; set; } = string.Empty;

    public RoleUtilisateur Role { get; set; } = RoleUtilisateur.Caissier;

    public bool IsSuperAdmin { get; set; }

    /// <summary>Profil d'accès (0 = aucun, droits par défaut du rôle).</summary>
    public int ProfilAccesId { get; set; }

    // --- Inscription en ligne / confirmation par email ---
    public string Email { get; set; } = string.Empty;
    public bool EmailConfirme { get; set; }
    public string TokenConfirmation { get; set; } = string.Empty;
    public DateTime? DateInscription { get; set; }

    // --- Authentification externe (Google OAuth) ---
    public string FournisseurConnexion { get; set; } = "Local";
    public string GoogleSubject { get; set; } = string.Empty;
    public DateTime? DateDerniereConnexion { get; set; }

    public bool Actif { get; set; } = true;

    /// <summary>Protection contre les attaques par force brute.</summary>
    public int TentativesEchouees { get; set; }
    public DateTime? VerrouJusqua { get; set; }

    /// <summary>Id du dernier message de la messagerie lu (badge de non-lus).</summary>
    public int DerniereLectureChatId { get; set; }

    // --- Réinitialisation de mot de passe (« mot de passe oublié ») ---
    public string TokenReset { get; set; } = string.Empty;
    public DateTime? TokenResetExpiration { get; set; }
}

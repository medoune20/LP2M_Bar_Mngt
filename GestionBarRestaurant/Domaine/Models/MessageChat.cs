using System.ComponentModel.DataAnnotations;

namespace Domaine.Models;

/// <summary>
/// Message de la messagerie interne en temps réel.
/// Chaque message est cloisonné par tenant : seuls les utilisateurs du même
/// établissement (TenantId) peuvent voir et échanger les messages.
/// </summary>
public class MessageChat
{
    public int Id { get; set; }

    /// <summary>Établissement propriétaire du message (cloisonnement multi-tenant).</summary>
    public int TenantId { get; set; }

    /// <summary>Auteur du message.</summary>
    public int UtilisateurId { get; set; }

    /// <summary>Nom affiché de l'auteur (dénormalisé pour l'historique).</summary>
    public string AuteurNom { get; set; } = string.Empty;

    /// <summary>Rôle de l'auteur au moment de l'envoi (1=Admin, 2=Caissier, 3=Manager).</summary>
    public int AuteurRole { get; set; }

    /// <summary>Salon / canal du message (« general », « encadrement », « caisse »…).</summary>
    public string Canal { get; set; } = "general";

    [Required]
    [MaxLength(2000)]
    public string Texte { get; set; } = string.Empty;

    public DateTime DateEnvoi { get; set; } = DateTime.Now;
}

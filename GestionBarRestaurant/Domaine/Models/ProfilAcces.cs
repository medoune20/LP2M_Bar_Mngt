using System.ComponentModel.DataAnnotations;

namespace Domaine.Models;

public class ProfilAcces
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    [Required(ErrorMessage = "Le nom du profil est obligatoire")]
    [Display(Name = "Nom du profil")]
    public string Nom { get; set; } = string.Empty;

    /// <summary>Permissions sérialisées : "produits=CM;stock=C;..."</summary>
    public string Permissions { get; set; } = string.Empty;

    public bool Actif { get; set; } = true;
}

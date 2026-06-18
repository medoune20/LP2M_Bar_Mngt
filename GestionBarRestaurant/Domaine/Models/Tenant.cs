using System.ComponentModel.DataAnnotations;

namespace Domaine.Models;

public class Tenant
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Le nom du tenant est obligatoire")]
    [Display(Name = "Nom de l'établissement")]
    public string Nom { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le code est obligatoire")]
    public string Code { get; set; } = string.Empty;

    public string Telephone { get; set; } = string.Empty;

    public string Adresse { get; set; } = string.Empty;

    public string CouleurPrincipale { get; set; } = "#165DFF";

    // --- Informations utiles pour les factures et la publicité ---
    [Display(Name = "Slogan / accroche")]
    public string Slogan { get; set; } = string.Empty;

    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Site web / page")]
    public string SiteWeb { get; set; } = string.Empty;

    [Display(Name = "Ville")]
    public string Ville { get; set; } = string.Empty;

    [Display(Name = "Registre de commerce (RCCM)")]
    public string RegistreCommerce { get; set; } = string.Empty;

    [Display(Name = "Compte contribuable (NCC)")]
    public string NumeroContribuable { get; set; } = string.Empty;

    [Display(Name = "Logo (chemin)")]
    public string LogoPath { get; set; } = string.Empty;

    [Display(Name = "Mention de bas de facture")]
    public string PiedFacture { get; set; } = string.Empty;

    public bool Actif { get; set; } = true;

    public DateTime DateCreation { get; set; } = DateTime.Now;
}

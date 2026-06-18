using System.ComponentModel.DataAnnotations;

namespace Domaine.Models;

public class Produit
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    [Required(ErrorMessage = "Le nom est obligatoire")]
    [Display(Name = "Nom du produit")]
    public string Nom { get; set; } = string.Empty;

    [Display(Name = "Catégorie")]
    public string Categorie { get; set; } = string.Empty;

    [Range(0, double.MaxValue, ErrorMessage = "Le prix d'achat doit être positif")]
    [Display(Name = "Prix d'achat")]
    public decimal PrixAchat { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Le prix de vente doit être positif")]
    [Display(Name = "Prix de vente")]
    public decimal PrixVente { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Le stock actuel doit être positif")]
    [Display(Name = "Stock actuel")]
    public int StockActuel { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Le stock minimum doit être positif")]
    [Display(Name = "Stock minimum")]
    public int StockMinimum { get; set; } = 5;

    [Display(Name = "Code-barres")]
    public string CodeBarre { get; set; } = string.Empty;

    [Display(Name = "Image")]
    public string ImagePath { get; set; } = string.Empty;

    public bool Actif { get; set; } = true;

    public DateTime DateCreation { get; set; } = DateTime.Now;

    public string QrValue => $"PRODUIT|TENANT:{TenantId}|ID:{Id}|NOM:{Nom}|PRIX:{PrixVente}";
}

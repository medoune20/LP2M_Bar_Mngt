using Domaine;

namespace Domaine.Models;

public class MouvementStock
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public DateTime DateMouvement { get; set; } = DateTime.Now;
    public int ProduitId { get; set; }
    public string ProduitNom { get; set; } = string.Empty;
    public TypeMouvementStock Type { get; set; }
    public int Quantite { get; set; }
    public string Motif { get; set; } = string.Empty;

    /// <summary>Coût d'achat unitaire saisi sur les entrées : sert au calcul du CMUP.</summary>
    public decimal? CoutUnitaire { get; set; }
    public string Utilisateur { get; set; } = string.Empty;
}

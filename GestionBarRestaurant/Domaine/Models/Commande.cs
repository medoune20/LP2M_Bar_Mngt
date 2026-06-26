using Domaine;

namespace Domaine.Models;

/// <summary>
/// Addition d'une table (service restaurant) : une commande ouverte regroupe les
/// articles servis avant l'encaissement (qui crée la vente correspondante).
/// </summary>
public class Commande
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int TableId { get; set; }
    public string TableNom { get; set; } = string.Empty;

    public string Numero { get; set; } = string.Empty;
    public StatutCommande Statut { get; set; } = StatutCommande.Ouverte;

    public DateTime DateOuverture { get; set; } = DateTime.Now;
    public DateTime? DateCloture { get; set; }

    public string OuvertePar { get; set; } = string.Empty;
    public int Couverts { get; set; } = 1;
    public string ClientNom { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;

    /// <summary>Vente générée à l'encaissement (null tant que la commande est ouverte).</summary>
    public int? VenteId { get; set; }

    public List<LigneCommande> Lignes { get; set; } = new();

    public decimal Total => Lignes.Sum(l => l.Total);
    public int NombreArticles => Lignes.Sum(l => l.Quantite);
}

public class LigneCommande
{
    public int Id { get; set; }
    public int CommandeId { get; set; }
    public int ProduitId { get; set; }
    public string ProduitNom { get; set; } = string.Empty;
    public int Quantite { get; set; } = 1;
    public decimal PrixUnitaire { get; set; }
    public decimal PrixAchatUnitaire { get; set; }
    public string Note { get; set; } = string.Empty;
    public StatutPreparation Preparation { get; set; } = StatutPreparation.EnAttente;

    public decimal Total => Quantite * PrixUnitaire;
}

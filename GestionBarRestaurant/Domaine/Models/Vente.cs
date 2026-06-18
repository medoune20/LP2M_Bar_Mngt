using Domaine;

namespace Domaine.Models;

public class Vente
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public DateTime DateVente { get; set; } = DateTime.Now;
    public string NumeroTicket { get; set; } = string.Empty;
    public int? ClientId { get; set; }
    public string ClientNom { get; set; } = "Client comptoir";
    public string ModePaiement { get; set; } = "Espèces";
    public string ReferencePaiement { get; set; } = string.Empty;
    public decimal Remise { get; set; }
    public int PointsFideliteUtilises { get; set; }
    public int PointsFideliteGagnes { get; set; }
    public decimal RemiseFidelite { get; set; }
    public decimal MontantRecu { get; set; }
    public decimal TotalBrut { get; set; }
    public decimal Total => Math.Max(0, TotalBrut - Remise - RemiseFidelite);
    public decimal MonnaieRendue => MontantRecu > Total ? MontantRecu - Total : 0;
    public string Vendeur { get; set; } = string.Empty;
    public int VendeurId { get; set; }

    // Règles de gestion : rattachement à la session de caisse et cycle de vie.
    public int? CaisseSessionId { get; set; }
    public StatutVente Statut { get; set; } = StatutVente.Validee;
    public DateTime? DateAnnulation { get; set; }
    public string MotifAnnulation { get; set; } = string.Empty;
    public string AnnuleePar { get; set; } = string.Empty;

    public List<LigneVente> Lignes { get; set; } = new();

    // Marge brute réelle (coût figé au moment de la vente).
    public decimal CoutTotal => Lignes.Sum(l => l.Quantite * l.PrixAchatUnitaire);
    public decimal MargeBrute => Total - CoutTotal;
}

public class LigneVente
{
    public int Id { get; set; }
    public int ProduitId { get; set; }
    public string ProduitNom { get; set; } = string.Empty;
    public int Quantite { get; set; }
    public decimal PrixUnitaire { get; set; }

    // Coût d'achat figé au moment de la vente : permet le calcul de la marge
    // réelle même si le prix d'achat du produit change ensuite.
    public decimal PrixAchatUnitaire { get; set; }

    public decimal Total => Quantite * PrixUnitaire;
}

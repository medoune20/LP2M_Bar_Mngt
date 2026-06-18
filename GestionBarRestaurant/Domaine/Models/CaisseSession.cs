using Domaine;

namespace Domaine.Models;

public class CaisseSession
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public DateTime DateOuverture { get; set; } = DateTime.Now;
    public DateTime? DateFermeture { get; set; }
    public decimal MontantOuverture { get; set; }

    /// <summary>Encaissements en ESPÈCES uniquement (ce qui entre réellement dans le tiroir).</summary>
    public decimal Encaissements { get; set; }

    /// <summary>Encaissements des autres modes (Mobile Money, carte, etc.) : suivis mais hors tiroir.</summary>
    public decimal EncaissementsAutres { get; set; }

    public decimal Decaissements { get; set; }
    public decimal? MontantCloture { get; set; }
    public string Caissier { get; set; } = string.Empty;
    public int CaissierId { get; set; }
    public string Libelle { get; set; } = "Caisse";
    public string ClotureePar { get; set; } = string.Empty;
    public string CommentaireCloture { get; set; } = string.Empty;
    public StatutCaisse Statut { get; set; } = StatutCaisse.Ouverte;

    /// <summary>Solde théorique du tiroir : fond de caisse + espèces encaissées - décaissements.</summary>
    public decimal SoldeTheorique => MontantOuverture + Encaissements - Decaissements;

    public decimal? Ecart => MontantCloture.HasValue ? MontantCloture.Value - SoldeTheorique : null;

    /// <summary>Chiffre d'affaires total de la session, tous modes de paiement confondus.</summary>
    public decimal ChiffreAffaires => Encaissements + EncaissementsAutres;
}

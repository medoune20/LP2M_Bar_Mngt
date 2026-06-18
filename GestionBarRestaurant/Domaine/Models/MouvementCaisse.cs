using Domaine;

namespace Domaine.Models;

/// <summary>
/// Journal des mouvements d'espèces hors ventes/dépenses :
/// apports de fond, retraits (remise en banque, coffre) et règlements de créances clients.
/// </summary>
public class MouvementCaisse
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int CaisseSessionId { get; set; }
    public DateTime DateMouvement { get; set; } = DateTime.Now;
    public TypeMouvementCaisse Type { get; set; }
    public decimal Montant { get; set; }
    public string Motif { get; set; } = string.Empty;
    public string Utilisateur { get; set; } = string.Empty;
    public int? ClientId { get; set; }
    public string ClientNom { get; set; } = string.Empty;
}

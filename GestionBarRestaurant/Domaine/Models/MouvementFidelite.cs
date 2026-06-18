namespace Domaine.Models;

public class MouvementFidelite
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int ClientId { get; set; }
    public string ClientNom { get; set; } = string.Empty;
    public int? VenteId { get; set; }
    public DateTime DateMouvement { get; set; } = DateTime.Now;
    public int Points { get; set; }
    public string TypeMouvement { get; set; } = "Gain";
    public string Commentaire { get; set; } = string.Empty;
    public string Utilisateur { get; set; } = string.Empty;
}

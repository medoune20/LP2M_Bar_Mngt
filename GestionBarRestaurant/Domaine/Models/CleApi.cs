namespace Domaine.Models;

/// <summary>
/// Clé d'API par établissement, pour l'interfaçage des données comptables avec
/// un outil ou un ERP externe. La clé complète n'est affichée qu'une fois ;
/// seul son empreinte SHA-256 est conservée.
/// </summary>
public class CleApi
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Libelle { get; set; } = string.Empty;

    /// <summary>Préfixe affichable (début de la clé) pour identifier la clé.</summary>
    public string Prefixe { get; set; } = string.Empty;

    /// <summary>Empreinte SHA-256 de la clé complète.</summary>
    public string CleHash { get; set; } = string.Empty;

    /// <summary>Portée d'accès (lecture seule des données comptables par défaut).</summary>
    public string Scope { get; set; } = "lecture";

    public bool Actif { get; set; } = true;
    public DateTime DateCreation { get; set; } = DateTime.Now;
    public DateTime? DerniereUtilisation { get; set; }
}

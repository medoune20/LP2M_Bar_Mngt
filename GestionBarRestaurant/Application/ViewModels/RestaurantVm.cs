using Domaine.Models;

namespace Application.ViewModels;

/// <summary>État d'une table de salle : la table et son addition ouverte (le cas échéant).</summary>
public class TableEtatVm
{
    public TableResto Table { get; set; } = null!;
    public Commande? Commande { get; set; }

    public bool Occupee => Commande != null;
    public decimal Total => Commande?.Total ?? 0;
}

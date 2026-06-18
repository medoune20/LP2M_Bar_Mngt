namespace Application.ViewModels;

public class PrevisionVenteVm
{
    public string Periode { get; set; } = string.Empty;
    public decimal ChiffreAffairesPrevu { get; set; }
    public decimal MoyenneJournaliere { get; set; }
    public decimal TendancePourcentage { get; set; }
}

namespace Application.ViewModels;

public class EvaluationCaissierVm
{
    public string Caissier { get; set; } = string.Empty;
    public int NombreTickets { get; set; }
    public decimal ChiffreAffaires { get; set; }
    public decimal PanierMoyen { get; set; }
    public decimal RemisesAccordees { get; set; }
    public int ProduitsVendus { get; set; }
    public decimal ScorePerformance { get; set; }
    public string Appreciation { get; set; } = string.Empty;
}

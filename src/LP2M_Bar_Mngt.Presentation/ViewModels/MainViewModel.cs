using System.Windows.Input;
using LP2M_Bar_Mngt.Application.Abstractions;
using LP2M_Bar_Mngt.Application.DTOs;

namespace LP2M_Bar_Mngt.Presentation.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IApplicationDatabaseInitializer _databaseInitializer;
    private readonly IDashboardReadService _dashboardReadService;
    private readonly IOperationsService _operationsService;
    private readonly AsyncRelayCommand _refreshCommand;
    private readonly RelayCommand _navigateCommand;
    private DashboardSummary _summary = DashboardSummary.Empty;
    private OperationsSnapshot _snapshot = OperationsSnapshot.Empty;
    private IReadOnlyList<ModuleFeatureViewModel> _currentModuleFeatures = Array.Empty<ModuleFeatureViewModel>();
    private IReadOnlyList<ModuleRowDto> _currentModuleRows = Array.Empty<ModuleRowDto>();
    private bool _isBusy;
    private string _selectedModuleKey = "dashboard";
    private string _currentModuleTitle = "Tableau de bord";
    private string _currentModuleDescription = "Vue globale de l'activite locale du bar.";
    private string _currentModuleStatus = "Module pret.";
    private string _statusMessage = "Initialisation...";

    public MainViewModel(
        IApplicationDatabaseInitializer databaseInitializer,
        IDashboardReadService dashboardReadService,
        IOperationsService operationsService,
        string databasePath)
    {
        _databaseInitializer = databaseInitializer;
        _dashboardReadService = dashboardReadService;
        _operationsService = operationsService;
        DatabasePath = databasePath;
        NavigationItems =
        [
            new NavigationItemViewModel("dashboard", "Tableau de bord"),
            new NavigationItemViewModel("cash", "Caisse"),
            new NavigationItemViewModel("sales", "Ventes"),
            new NavigationItemViewModel("products", "Produits"),
            new NavigationItemViewModel("stock", "Stock"),
            new NavigationItemViewModel("expenses", "Depenses"),
            new NavigationItemViewModel("reports", "Rapports"),
            new NavigationItemViewModel("users", "Utilisateurs")
        ];

        _navigateCommand = new RelayCommand(parameter => SelectModule(parameter?.ToString() ?? "dashboard"));
        _refreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
        SelectModule("dashboard", updateStatus: false);
    }

    public DashboardSummary Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    public IReadOnlyList<NavigationItemViewModel> NavigationItems { get; }

    public IReadOnlyList<ModuleFeatureViewModel> CurrentModuleFeatures
    {
        get => _currentModuleFeatures;
        private set => SetProperty(ref _currentModuleFeatures, value);
    }

    public IReadOnlyList<ModuleRowDto> CurrentModuleRows
    {
        get => _currentModuleRows;
        private set => SetProperty(ref _currentModuleRows, value);
    }

    public string CurrentModuleTitle
    {
        get => _currentModuleTitle;
        private set => SetProperty(ref _currentModuleTitle, value);
    }

    public string CurrentModuleDescription
    {
        get => _currentModuleDescription;
        private set => SetProperty(ref _currentModuleDescription, value);
    }

    public string CurrentModuleStatus
    {
        get => _currentModuleStatus;
        private set => SetProperty(ref _currentModuleStatus, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                _refreshCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string DatabasePath { get; }

    public ICommand NavigateCommand => _navigateCommand;

    public ICommand RefreshCommand => _refreshCommand;

    public Task InitializeAsync()
    {
        return RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        IsBusy = true;
        StatusMessage = "Preparation de la base locale...";

        try
        {
            await _databaseInitializer.InitializeAsync();
            Summary = await _dashboardReadService.GetSummaryAsync();
            _snapshot = await _operationsService.GetSnapshotAsync();
            RefreshCurrentModuleFeatures();
            StatusMessage = $"Pret - derniere mise a jour {DateTime.Now:HH:mm}";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Erreur : {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SelectModule(string key, bool updateStatus = true)
    {
        _selectedModuleKey = key;

        foreach (var item in NavigationItems)
        {
            item.IsSelected = string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase);
        }

        RefreshCurrentModuleFeatures();

        if (updateStatus)
        {
            StatusMessage = $"{CurrentModuleTitle} ouvert.";
        }
    }

    private void RefreshCurrentModuleFeatures()
    {
        var module = _selectedModuleKey switch
        {
            "cash" => Module(
                "Caisse",
                "Ouverture, suivi et cloture des sessions de caisse par caissier.",
                "Module operationnel : les sessions et mouvements sont enregistres dans SQLite.",
                Features(
                    Action("Ouvrir une session", "Pret", "Cree une session de caisse admin avec un fond initial de 100,00.", "Ouvrir", _operationsService.OpenCashSessionAsync),
                    Action("Cloturer une session", "Pret", "Calcule le solde attendu, declare le meme montant et cloture la session ouverte.", "Cloturer", _operationsService.CloseCashSessionAsync),
                    Action("Mouvements de caisse", "Actif", "Les ventes et depenses caisse alimentent automatiquement le journal de caisse.", "Actualiser", RefreshOnlyAsync))),

            "sales" => Module(
                "Ventes",
                "Encaissement, tickets, modes de paiement et historique des ventes.",
                "Module operationnel : vente rapide, reimpression et annulation controlee.",
                Features(
                    Action("Nouvelle vente", "Pret", "Cree un ticket sur le premier produit vendable et deduit le stock.", "Vendre", _operationsService.CreateSaleAsync),
                    Action("Tickets", "Pret", "Prepare le dernier ticket pour reimpression.", "Reimprimer", _operationsService.ReprintLastTicketAsync),
                    Action("Annulation controlee", "Pret", "Annule la derniere vente validee, restaure le stock et ajoute l'audit.", "Annuler", _operationsService.CancelLastSaleAsync))),

            "products" => Module(
                "Produits",
                "Catalogue, categories, prix de vente et seuils de stock faible.",
                $"{Summary.ActiveProductCount} produit(s) actif(s).",
                Features(
                    Action("Catalogue produits", "Pret", "Ajoute un produit stockable avec prix, cout, seuil et stock initial.", "Ajouter", _operationsService.AddProductAsync),
                    Action("Categories", "Pret", "Ajoute une nouvelle categorie active.", "Creer", _operationsService.AddCategoryAsync),
                    Action("Prix et stock", "Pret", "Augmente le prix du premier produit actif et trace l'action.", "Mettre a jour", _operationsService.UpdateProductPriceAsync))),

            "stock" => Module(
                "Stock",
                "Entrees, sorties, ajustements et alertes de stock faible.",
                $"{Summary.LowStockCount} alerte(s) stock faible.",
                Features(
                    Action("Entrees de stock", "Pret", "Reapprovisionne tous les produits sous leur seuil d'alerte.", "Reapprovisionner", _operationsService.RestockLowProductsAsync),
                    Action("Ajustements", "Pret", "Ajoute un ajustement inventaire de +5 sur un produit stockable.", "Ajuster", _operationsService.AdjustStockAsync),
                    Action("Alertes", "Actif", "Controle les alertes de stock faible depuis les niveaux SQLite.", "Verifier", _operationsService.CheckStockAlertsAsync))),

            "expenses" => Module(
                "Depenses",
                "Charges, achats fournisseurs et sorties de caisse.",
                "Module operationnel : depenses hors caisse et depenses payees depuis la caisse.",
                Features(
                    Action("Nouvelle depense", "Pret", "Enregistre une depense fournisseur de demonstration hors caisse.", "Enregistrer", _operationsService.RecordExpenseAsync),
                    Action("Paiement depuis caisse", "Pret", "Enregistre une depense et cree le mouvement de caisse associe.", "Payer caisse", _operationsService.RecordCashExpenseAsync),
                    Action("Historique", "Actif", "L'historique des depenses se recharge depuis SQLite.", "Actualiser", RefreshOnlyAsync))),

            "reports" => Module(
                "Rapports",
                "Analyses ventes, caisse, stock, depenses et produits les plus vendus.",
                "Module operationnel : synthese journaliere et export CSV.",
                Features(
                    Action("Rapport journalier", "Pret", "Calcule les ventes, tickets, depenses et alertes du jour.", "Generer", _operationsService.GenerateDailyReportAsync),
                    Action("Rapport stock", "Actif", "Affiche l'etat des alertes et quantites stockables.", "Actualiser", RefreshOnlyAsync),
                    Action("Exports", "Pret", "Exporte la synthese du jour en CSV dans AppData.", "Exporter", _operationsService.ExportDailyReportAsync))),

            "users" => Module(
                "Utilisateurs",
                "Comptes, roles, permissions et securite locale.",
                $"{Summary.ActiveUserCount} utilisateur(s) actif(s).",
                Features(
                    Action("Connexion", "Pret", "Reinitialise le mot de passe admin temporaire pour les tests.", "Reset admin", _operationsService.ResetAdminPasswordAsync),
                    Action("Roles", "Pret", "Cree un utilisateur caissier avec le role correspondant.", "Ajouter caissier", _operationsService.AddCashierUserAsync),
                    Action("Audit", "Actif", "Ajoute une entree d'audit pour verifier la traçabilite.", "Auditer", _operationsService.WriteAuditEntryAsync))),

            _ => Module(
                "Tableau de bord",
                "Vue globale de l'activite locale du bar.",
                $"Tickets du jour : {Summary.TodayTicketCount} - stock faible : {Summary.LowStockCount}.",
                Features(
                    Action("Synthese du jour", "Actif", $"Chiffre d'affaires : {Summary.TodayRevenue:N2}.", "Actualiser", RefreshOnlyAsync),
                    Action("Session de caisse", "Pret", $"{Summary.OpenCashSessionCount} session(s) ouverte(s).", "Ouvrir caisse", _operationsService.OpenCashSessionAsync),
                    Action("Surveillance stock", "Actif", $"{Summary.LowStockCount} produit(s) sous le seuil.", "Verifier", _operationsService.CheckStockAlertsAsync)))
        };

        CurrentModuleTitle = module.Title;
        CurrentModuleDescription = module.Description;
        CurrentModuleStatus = module.Status;
        CurrentModuleFeatures = module.Features;
        CurrentModuleRows = _selectedModuleKey switch
        {
            "cash" => _snapshot.CashRows,
            "sales" => _snapshot.SalesRows,
            "products" => _snapshot.ProductRows,
            "stock" => _snapshot.StockRows,
            "expenses" => _snapshot.ExpenseRows,
            "reports" => _snapshot.ReportRows,
            "users" => _snapshot.UserRows,
            _ => _snapshot.DashboardRows
        };
    }

    private ModuleFeatureViewModel Action(
        string name,
        string status,
        string details,
        string buttonText,
        Func<CancellationToken, Task<OperationResult>> action)
    {
        return new ModuleFeatureViewModel(
            name,
            status,
            details,
            buttonText,
            new AsyncRelayCommand(() => ExecuteOperationAsync(action), () => !IsBusy));
    }

    private async Task<OperationResult> RefreshOnlyAsync(CancellationToken cancellationToken)
    {
        await RefreshDataAsync(cancellationToken);
        return new OperationResult(true, "Donnees actualisees.");
    }

    private async Task ExecuteOperationAsync(Func<CancellationToken, Task<OperationResult>> operation)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "Traitement en cours...";

        try
        {
            await _databaseInitializer.InitializeAsync();
            var result = await operation(CancellationToken.None);
            await RefreshDataAsync(CancellationToken.None);
            StatusMessage = result.Message;
        }
        catch (Exception exception)
        {
            StatusMessage = $"Erreur : {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshDataAsync(CancellationToken cancellationToken)
    {
        Summary = await _dashboardReadService.GetSummaryAsync(cancellationToken);
        _snapshot = await _operationsService.GetSnapshotAsync(cancellationToken);
        RefreshCurrentModuleFeatures();
    }

    private static IReadOnlyList<ModuleFeatureViewModel> Features(params ModuleFeatureViewModel[] features)
    {
        return features;
    }

    private static ModuleContent Module(
        string title,
        string description,
        string status,
        IReadOnlyList<ModuleFeatureViewModel> features)
    {
        return new ModuleContent(title, description, status, features);
    }

    private sealed record ModuleContent(
        string Title,
        string Description,
        string Status,
        IReadOnlyList<ModuleFeatureViewModel> Features);
}

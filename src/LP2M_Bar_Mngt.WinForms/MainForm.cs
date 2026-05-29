using LP2M_Bar_Mngt.Application.Abstractions;
using LP2M_Bar_Mngt.Application.DTOs;

namespace LP2M_Bar_Mngt.WinForms;

public sealed class MainForm : Form
{
    private readonly IApplicationDatabaseInitializer _databaseInitializer;
    private readonly IDashboardReadService _dashboardReadService;
    private readonly IOperationsService _operationsService;
    private readonly string _databasePath;
    private readonly Dictionary<string, Button> _navigationButtons = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ModuleDefinition> _modules;

    private DashboardSummary _summary = DashboardSummary.Empty;
    private OperationsSnapshot _snapshot = OperationsSnapshot.Empty;
    private string _currentModuleKey = "dashboard";
    private bool _isBusy;

    private readonly Label _moduleTitleLabel = new();
    private readonly Label _moduleDescriptionLabel = new();
    private readonly Label _statusLabel = new();
    private readonly Label _databaseLabel = new();
    private readonly Label _todayRevenueValue = new();
    private readonly Label _todayTicketValue = new();
    private readonly Label _openSessionValue = new();
    private readonly Label _lowStockValue = new();
    private readonly FlowLayoutPanel _actionsPanel = new();
    private readonly DataGridView _moduleGrid = new();
    private readonly DataGridView _stockAlertsGrid = new();

    private readonly Color _background = Color.FromArgb(245, 247, 244);
    private readonly Color _panel = Color.White;
    private readonly Color _sidebar = Color.FromArgb(24, 32, 30);
    private readonly Color _sidebarHover = Color.FromArgb(38, 50, 47);
    private readonly Color _accent = Color.FromArgb(46, 125, 87);
    private readonly Color _text = Color.FromArgb(29, 37, 34);
    private readonly Color _muted = Color.FromArgb(102, 115, 109);
    private readonly Color _border = Color.FromArgb(221, 228, 223);

    public MainForm(
        IApplicationDatabaseInitializer databaseInitializer,
        IDashboardReadService dashboardReadService,
        IOperationsService operationsService,
        string databasePath)
    {
        _databaseInitializer = databaseInitializer;
        _dashboardReadService = dashboardReadService;
        _operationsService = operationsService;
        _databasePath = databasePath;
        WinFormsStartupLogger.Write("MainForm constructor started.");
        _modules =
        [
            new("dashboard", "Tableau de bord", "Vue globale de l'activite locale du bar."),
            new("cash", "Caisse", "Ouverture, suivi et cloture des sessions de caisse."),
            new("sales", "Ventes", "Encaissement, tickets, paiements et annulations."),
            new("products", "Produits", "Catalogue, categories, prix et seuils de stock."),
            new("stock", "Stock", "Entrees, ajustements et alertes de stock faible."),
            new("expenses", "Depenses", "Charges, achats fournisseurs et sorties de caisse."),
            new("reports", "Rapports", "Synthese journaliere, stock et export CSV."),
            new("users", "Utilisateurs", "Comptes, roles, securite et audit.")
        ];

        BuildLayout();
        WinFormsStartupLogger.Write("MainForm constructor completed.");
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        WinFormsStartupLogger.Write("MainForm shown.");
        await RefreshAllAsync("Initialisation terminee.");
        WinFormsStartupLogger.Write("MainForm initial data loaded.");
    }

    private void BuildLayout()
    {
        Text = "LP2M_Bar_Mngt - Windows Forms";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1180, 760);
        Size = new Size(1400, 860);
        BackColor = _background;
        Font = new Font("Segoe UI", 9F, FontStyle.Regular);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = _background
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(root);

        root.Controls.Add(BuildSidebar(), 0, 0);
        root.Controls.Add(BuildMainContent(), 1, 0);
    }

    private Control BuildSidebar()
    {
        var sidebarPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = _sidebar,
            Padding = new Padding(18, 24, 18, 18)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            BackColor = _sidebar
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        sidebarPanel.Controls.Add(layout);

        var header = new Panel { Height = 72, Dock = DockStyle.Top, BackColor = _sidebar };
        header.Controls.Add(new Label
        {
            Text = "LP2M",
            Dock = DockStyle.Top,
            Height = 34,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 24F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        });
        header.Controls.Add(new Label
        {
            Text = "Bar Management",
            Dock = DockStyle.Bottom,
            Height = 22,
            ForeColor = Color.FromArgb(170, 183, 177),
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleLeft
        });
        layout.Controls.Add(header, 0, 0);

        var menuPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = _sidebar,
            Padding = new Padding(0, 12, 0, 0)
        };
        layout.Controls.Add(menuPanel, 0, 1);

        foreach (var module in _modules)
        {
            var button = CreateNavigationButton(module);
            _navigationButtons[module.Key] = button;
            menuPanel.Controls.Add(button);
        }

        var footer = new Label
        {
            Dock = DockStyle.Fill,
            Height = 56,
            Text = "Standalone SQLite\r\nMode hors connexion",
            ForeColor = Color.FromArgb(238, 245, 241),
            TextAlign = ContentAlignment.BottomLeft
        };
        layout.Controls.Add(footer, 0, 2);

        return sidebarPanel;
    }

    private Control BuildMainContent()
    {
        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24),
            RowCount = 4,
            ColumnCount = 1,
            BackColor = _background
        };
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

        mainPanel.Controls.Add(BuildHeader(), 0, 0);
        mainPanel.Controls.Add(BuildMetrics(), 0, 1);
        mainPanel.Controls.Add(BuildWorkspace(), 0, 2);
        mainPanel.Controls.Add(BuildFooter(), 0, 3);

        return mainPanel;
    }

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = _background
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));

        var titlePanel = new Panel { Dock = DockStyle.Fill, BackColor = _background };
        _moduleTitleLabel.Text = "Tableau de bord";
        _moduleTitleLabel.Dock = DockStyle.Top;
        _moduleTitleLabel.Height = 38;
        _moduleTitleLabel.ForeColor = _text;
        _moduleTitleLabel.Font = new Font("Segoe UI", 22F, FontStyle.Bold);

        _moduleDescriptionLabel.Text = "Vue globale de l'activite locale du bar.";
        _moduleDescriptionLabel.Dock = DockStyle.Top;
        _moduleDescriptionLabel.Height = 22;
        _moduleDescriptionLabel.ForeColor = _muted;

        _statusLabel.Text = "Initialisation...";
        _statusLabel.Dock = DockStyle.Top;
        _statusLabel.Height = 24;
        _statusLabel.ForeColor = _accent;
        _statusLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

        titlePanel.Controls.Add(_statusLabel);
        titlePanel.Controls.Add(_moduleDescriptionLabel);
        titlePanel.Controls.Add(_moduleTitleLabel);
        header.Controls.Add(titlePanel, 0, 0);

        var refreshButton = new Button
        {
            Text = "Actualiser",
            Dock = DockStyle.Top,
            Height = 42,
            FlatStyle = FlatStyle.Flat,
            BackColor = _accent,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        refreshButton.FlatAppearance.BorderSize = 0;
        refreshButton.Click += async (_, _) => await RefreshAllAsync("Donnees actualisees.");
        header.Controls.Add(refreshButton, 1, 0);

        return header;
    }

    private Control BuildMetrics()
    {
        var metrics = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = _background,
            Padding = new Padding(0, 0, 0, 12)
        };

        for (var index = 0; index < 4; index++)
        {
            metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        }

        metrics.Controls.Add(CreateMetricCard("Ventes du jour", _todayRevenueValue), 0, 0);
        metrics.Controls.Add(CreateMetricCard("Tickets", _todayTicketValue), 1, 0);
        metrics.Controls.Add(CreateMetricCard("Sessions ouvertes", _openSessionValue), 2, 0);
        metrics.Controls.Add(CreateMetricCard("Alertes stock", _lowStockValue), 3, 0);

        return metrics;
    }

    private Control BuildWorkspace()
    {
        var workspace = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = _background
        };
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 18));
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));

        var actionsCard = CreateCard();
        var actionsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            BackColor = _panel
        };
        actionsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        actionsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        actionsCard.Controls.Add(actionsLayout);

        actionsLayout.Controls.Add(CreateSectionHeader("Actions", "Operations disponibles pour le module courant."), 0, 0);

        _actionsPanel.Dock = DockStyle.Fill;
        _actionsPanel.FlowDirection = FlowDirection.TopDown;
        _actionsPanel.WrapContents = false;
        _actionsPanel.AutoScroll = true;
        _actionsPanel.BackColor = _panel;
        actionsLayout.Controls.Add(_actionsPanel, 0, 1);

        var dataCard = CreateCard();
        var dataLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            ColumnCount = 1,
            BackColor = _panel
        };
        dataLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        dataLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
        dataLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 14));
        dataLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
        dataCard.Controls.Add(dataLayout);

        dataLayout.Controls.Add(CreateSectionHeader("Donnees du module", "Etat courant lu depuis SQLite."), 0, 0);
        ConfigureModuleGrid(_moduleGrid);
        dataLayout.Controls.Add(_moduleGrid, 0, 1);
        ConfigureStockGrid(_stockAlertsGrid);
        dataLayout.Controls.Add(_stockAlertsGrid, 0, 3);

        workspace.Controls.Add(actionsCard, 0, 0);
        workspace.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = _background }, 1, 0);
        workspace.Controls.Add(dataCard, 2, 0);

        return workspace;
    }

    private Control BuildFooter()
    {
        _databaseLabel.Text = _databasePath;
        _databaseLabel.Dock = DockStyle.Fill;
        _databaseLabel.ForeColor = _muted;
        _databaseLabel.TextAlign = ContentAlignment.MiddleLeft;
        _databaseLabel.AutoEllipsis = true;
        return _databaseLabel;
    }

    private Button CreateNavigationButton(ModuleDefinition module)
    {
        var button = new Button
        {
            Text = module.Title,
            Width = 190,
            Height = 40,
            Margin = new Padding(0, 3, 0, 3),
            FlatStyle = FlatStyle.Flat,
            BackColor = _sidebar,
            ForeColor = Color.FromArgb(238, 245, 241),
            TextAlign = ContentAlignment.MiddleLeft,
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderSize = 0;
        button.Click += (_, _) => SelectModule(module.Key);
        return button;
    }

    private Control CreateMetricCard(string title, Label valueLabel)
    {
        var card = CreateCard(new Padding(14));
        card.Margin = new Padding(0, 0, 14, 0);

        var titleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 24,
            ForeColor = _muted
        };

        valueLabel.Text = "0";
        valueLabel.Dock = DockStyle.Fill;
        valueLabel.ForeColor = _text;
        valueLabel.Font = new Font("Segoe UI", 20F, FontStyle.Bold);
        valueLabel.TextAlign = ContentAlignment.MiddleLeft;

        card.Controls.Add(valueLabel);
        card.Controls.Add(titleLabel);
        return card;
    }

    private Panel CreateCard()
    {
        return CreateCard(new Padding(16));
    }

    private Panel CreateCard(Padding padding)
    {
        return new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = _panel,
            Padding = padding,
            Margin = new Padding(0)
        };
    }

    private Control CreateSectionHeader(string title, string subtitle)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = _panel };
        panel.Controls.Add(new Label
        {
            Text = subtitle,
            Dock = DockStyle.Bottom,
            Height = 22,
            ForeColor = _muted
        });
        panel.Controls.Add(new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 30,
            ForeColor = _text,
            Font = new Font("Segoe UI", 13F, FontStyle.Bold)
        });
        return panel;
    }

    private void ConfigureModuleGrid(DataGridView grid)
    {
        ConfigureGridBase(grid);
        grid.Columns.Add(CreateTextColumn("Section", "Section", 120));
        grid.Columns.Add(CreateTextColumn("Element", "Item", 210));
        grid.Columns.Add(CreateTextColumn("Valeur", "Value", 90));
        grid.Columns.Add(CreateTextColumn("Details", "Details", 260, true));
    }

    private void ConfigureStockGrid(DataGridView grid)
    {
        ConfigureGridBase(grid);
        grid.Columns.Add(CreateTextColumn("Produit", "ProductName", 180, true));
        grid.Columns.Add(CreateTextColumn("Categorie", "CategoryName", 120));
        grid.Columns.Add(CreateTextColumn("Qte", "Quantity", 70));
        grid.Columns.Add(CreateTextColumn("Seuil", "Threshold", 70));
    }

    private void ConfigureGridBase(DataGridView grid)
    {
        grid.Dock = DockStyle.Fill;
        grid.AutoGenerateColumns = false;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.ReadOnly = true;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.BackgroundColor = _panel;
        grid.BorderStyle = BorderStyle.None;
        grid.GridColor = _border;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(237, 245, 240);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = _text;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(207, 226, 215);
        grid.DefaultCellStyle.SelectionForeColor = _text;
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
    }

    private static DataGridViewTextBoxColumn CreateTextColumn(string header, string property, int width, bool fill = false)
    {
        return new DataGridViewTextBoxColumn
        {
            HeaderText = header,
            DataPropertyName = property,
            Width = width,
            AutoSizeMode = fill ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None
        };
    }

    private async Task RefreshAllAsync(string message)
    {
        if (_isBusy)
        {
            return;
        }

        _isBusy = true;
        SetStatus("Traitement en cours...");

        try
        {
            await _databaseInitializer.InitializeAsync();
            _summary = await _dashboardReadService.GetSummaryAsync();
            _snapshot = await _operationsService.GetSnapshotAsync();
            UpdateMetrics();
            RenderCurrentModule();
            SetStatus(message);
        }
        catch (Exception exception)
        {
            SetStatus($"Erreur : {exception.Message}", isError: true);
            MessageBox.Show(this, exception.Message, "Erreur LP2M_Bar_Mngt", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _isBusy = false;
        }
    }

    private void SelectModule(string moduleKey)
    {
        _currentModuleKey = moduleKey;
        RenderCurrentModule();
        SetStatus($"{CurrentModule.Title} ouvert.");
    }

    private void RenderCurrentModule()
    {
        var module = CurrentModule;
        _moduleTitleLabel.Text = module.Title;
        _moduleDescriptionLabel.Text = module.Description;

        foreach (var item in _navigationButtons)
        {
            var isSelected = string.Equals(item.Key, _currentModuleKey, StringComparison.OrdinalIgnoreCase);
            item.Value.BackColor = isSelected ? _accent : _sidebar;
            item.Value.Font = new Font("Segoe UI", 9F, isSelected ? FontStyle.Bold : FontStyle.Regular);
        }

        RenderActions(GetActionsForModule(_currentModuleKey));
        _moduleGrid.DataSource = GetRowsForModule(_currentModuleKey).ToList();
        _stockAlertsGrid.DataSource = _summary.LowStockAlerts.ToList();
    }

    private void RenderActions(IReadOnlyList<ActionDefinition> actions)
    {
        _actionsPanel.SuspendLayout();
        _actionsPanel.Controls.Clear();

        foreach (var action in actions)
        {
            _actionsPanel.Controls.Add(CreateActionCard(action));
        }

        _actionsPanel.ResumeLayout();
    }

    private Control CreateActionCard(ActionDefinition action)
    {
        var card = new Panel
        {
            Width = Math.Max(420, _actionsPanel.ClientSize.Width - 28),
            Height = 104,
            BackColor = Color.FromArgb(250, 252, 250),
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(12)
        };

        var button = new Button
        {
            Text = action.ButtonText,
            Width = 118,
            Height = 38,
            Dock = DockStyle.Right,
            FlatStyle = FlatStyle.Flat,
            BackColor = _accent,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderSize = 0;
        button.Click += async (_, _) => await ExecuteActionAsync(action);
        card.Controls.Add(button);

        var title = new Label
        {
            Text = action.Name,
            Dock = DockStyle.Top,
            Height = 24,
            ForeColor = _text,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        };

        var details = new Label
        {
            Text = action.Details,
            Dock = DockStyle.Fill,
            ForeColor = _muted,
            AutoEllipsis = true
        };

        card.Controls.Add(details);
        card.Controls.Add(title);
        return card;
    }

    private async Task ExecuteActionAsync(ActionDefinition action)
    {
        if (_isBusy)
        {
            return;
        }

        _isBusy = true;
        SetStatus("Traitement en cours...");

        try
        {
            await _databaseInitializer.InitializeAsync();
            var result = await action.Execute(CancellationToken.None);
            _summary = await _dashboardReadService.GetSummaryAsync();
            _snapshot = await _operationsService.GetSnapshotAsync();
            UpdateMetrics();
            RenderCurrentModule();
            SetStatus(result.Message, isError: !result.Success);
        }
        catch (Exception exception)
        {
            SetStatus($"Erreur : {exception.Message}", isError: true);
            MessageBox.Show(this, exception.Message, "Erreur LP2M_Bar_Mngt", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _isBusy = false;
        }
    }

    private void UpdateMetrics()
    {
        _todayRevenueValue.Text = _summary.TodayRevenue.ToString("N2");
        _todayTicketValue.Text = _summary.TodayTicketCount.ToString();
        _openSessionValue.Text = _summary.OpenCashSessionCount.ToString();
        _lowStockValue.Text = _summary.LowStockCount.ToString();
    }

    private IReadOnlyList<ActionDefinition> GetActionsForModule(string key)
    {
        return key switch
        {
            "cash" =>
            [
                new("Ouvrir une session", "Cree une session de caisse admin avec un fond initial de 100,00.", "Ouvrir", _operationsService.OpenCashSessionAsync),
                new("Cloturer une session", "Calcule le solde attendu puis cloture la session ouverte.", "Cloturer", _operationsService.CloseCashSessionAsync),
                new("Mouvements de caisse", "Recharge le journal de caisse depuis la base locale.", "Actualiser", RefreshOnlyAsync)
            ],
            "sales" =>
            [
                new("Nouvelle vente", "Cree un ticket rapide et deduit le stock du produit vendu.", "Vendre", _operationsService.CreateSaleAsync),
                new("Tickets", "Prepare le dernier ticket pour reimpression.", "Reimprimer", _operationsService.ReprintLastTicketAsync),
                new("Annulation controlee", "Annule la derniere vente validee avec trace d'audit.", "Annuler", _operationsService.CancelLastSaleAsync)
            ],
            "products" =>
            [
                new("Catalogue produits", "Ajoute un produit stockable avec stock initial.", "Ajouter", _operationsService.AddProductAsync),
                new("Categories", "Ajoute une categorie active au catalogue.", "Creer", _operationsService.AddCategoryAsync),
                new("Prix et stock", "Augmente le prix du premier produit actif.", "Mettre a jour", _operationsService.UpdateProductPriceAsync)
            ],
            "stock" =>
            [
                new("Entrees de stock", "Reapprovisionne tous les produits sous seuil.", "Reapprovisionner", _operationsService.RestockLowProductsAsync),
                new("Ajustements", "Ajoute un ajustement inventaire de +5.", "Ajuster", _operationsService.AdjustStockAsync),
                new("Alertes", "Controle les alertes de stock faible.", "Verifier", _operationsService.CheckStockAlertsAsync)
            ],
            "expenses" =>
            [
                new("Nouvelle depense", "Enregistre une depense fournisseur hors caisse.", "Enregistrer", _operationsService.RecordExpenseAsync),
                new("Paiement depuis caisse", "Enregistre une depense et son mouvement de caisse.", "Payer caisse", _operationsService.RecordCashExpenseAsync),
                new("Historique", "Recharge les depenses depuis SQLite.", "Actualiser", RefreshOnlyAsync)
            ],
            "reports" =>
            [
                new("Rapport journalier", "Calcule la synthese du jour.", "Generer", _operationsService.GenerateDailyReportAsync),
                new("Rapport stock", "Recharge les indicateurs et alertes stock.", "Actualiser", RefreshOnlyAsync),
                new("Exports", "Exporte la synthese du jour en CSV.", "Exporter", _operationsService.ExportDailyReportAsync)
            ],
            "users" =>
            [
                new("Connexion", "Reinitialise le mot de passe admin de demonstration.", "Reset admin", _operationsService.ResetAdminPasswordAsync),
                new("Roles", "Cree un compte caissier avec mot de passe temporaire.", "Ajouter caissier", _operationsService.AddCashierUserAsync),
                new("Audit", "Ajoute une entree d'audit de verification.", "Auditer", _operationsService.WriteAuditEntryAsync)
            ],
            _ =>
            [
                new("Synthese du jour", "Recharge toutes les donnees du tableau de bord.", "Actualiser", RefreshOnlyAsync),
                new("Session de caisse", "Ouvre rapidement une session de caisse.", "Ouvrir caisse", _operationsService.OpenCashSessionAsync),
                new("Surveillance stock", "Controle les alertes de stock faible.", "Verifier", _operationsService.CheckStockAlertsAsync)
            ]
        };
    }

    private IReadOnlyList<ModuleRowDto> GetRowsForModule(string key)
    {
        return key switch
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

    private Task<OperationResult> RefreshOnlyAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(new OperationResult(true, "Donnees actualisees."));
    }

    private void SetStatus(string message, bool isError = false)
    {
        _statusLabel.Text = message;
        _statusLabel.ForeColor = isError ? Color.FromArgb(176, 71, 58) : _accent;
    }

    private ModuleDefinition CurrentModule
    {
        get
        {
            return _modules.FirstOrDefault(module => string.Equals(module.Key, _currentModuleKey, StringComparison.OrdinalIgnoreCase))
                ?? _modules[0];
        }
    }

    private sealed record ModuleDefinition(string Key, string Title, string Description);

    private sealed record ActionDefinition(
        string Name,
        string Details,
        string ButtonText,
        Func<CancellationToken, Task<OperationResult>> Execute);
}

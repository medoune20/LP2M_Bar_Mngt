using System.Windows;
using LP2M_Bar_Mngt.Infrastructure.Data;
using LP2M_Bar_Mngt.Infrastructure.Security;
using LP2M_Bar_Mngt.Presentation.Diagnostics;
using LP2M_Bar_Mngt.Presentation.ViewModels;

namespace LP2M_Bar_Mngt.Presentation;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        StartupLogger.Write("Application startup requested.");
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        DispatcherUnhandledException += (_, args) =>
        {
            StartupLogger.WriteException("Dispatcher unhandled exception", args.Exception);
            MessageBox.Show(
                args.Exception.Message,
                "Erreur LP2M_Bar_Mngt",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                StartupLogger.WriteException("AppDomain unhandled exception", exception);
            }
        };

        try
        {
            base.OnStartup(e);
            StartupLogger.Write("Creating services.");

            var connectionFactory = SqliteConnectionFactory.CreateDefault();
            var passwordHasher = new PasswordHasher();
            var databaseInitializer = new SqliteDatabaseInitializer(connectionFactory, passwordHasher);
            var dashboardReadService = new SqliteDashboardReadService(connectionFactory);
            var operationsService = new SqliteOperationsService(connectionFactory, passwordHasher);
            var viewModel = new MainViewModel(databaseInitializer, dashboardReadService, operationsService, connectionFactory.DatabasePath);

            StartupLogger.Write("Creating main window.");
            var window = new MainWindow(viewModel)
            {
                ShowActivated = true,
                Topmost = true,
                WindowState = WindowState.Normal
            };

            MainWindow = window;
            window.Show();
            window.Activate();
            window.Focus();
            window.Topmost = false;
            StartupLogger.Write("Main window shown.");
        }
        catch (Exception exception)
        {
            StartupLogger.WriteException("Startup failure", exception);
            MessageBox.Show(
                $"Impossible de demarrer LP2M_Bar_Mngt.{Environment.NewLine}{exception.Message}{Environment.NewLine}{Environment.NewLine}Log : {StartupLogger.LogFilePath}",
                "Erreur de demarrage",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }
}

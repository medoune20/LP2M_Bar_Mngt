namespace LP2M_Bar_Mngt.WinForms;

using LP2M_Bar_Mngt.Infrastructure.Data;
using LP2M_Bar_Mngt.Infrastructure.Security;

static class Program
{
    [STAThread]
    static void Main()
    {
        WinFormsStartupLogger.Write("Application startup requested.");

        try
        {
            ApplicationConfiguration.Initialize();
            System.Windows.Forms.Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            System.Windows.Forms.Application.ThreadException += (_, args) =>
            {
                WinFormsStartupLogger.WriteException("Thread exception", args.Exception);
                MessageBox.Show(args.Exception.Message, "Erreur LP2M_Bar_Mngt", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                if (args.ExceptionObject is Exception exception)
                {
                    WinFormsStartupLogger.WriteException("Unhandled exception", exception);
                }
            };

            WinFormsStartupLogger.Write("Creating services.");
            var connectionFactory = SqliteConnectionFactory.CreateDefault();
            var passwordHasher = new PasswordHasher();
            var databaseInitializer = new SqliteDatabaseInitializer(connectionFactory, passwordHasher);
            var dashboardReadService = new SqliteDashboardReadService(connectionFactory);
            var operationsService = new SqliteOperationsService(connectionFactory, passwordHasher);

            WinFormsStartupLogger.Write("Creating main form.");
            var form = new MainForm(databaseInitializer, dashboardReadService, operationsService, connectionFactory.DatabasePath);
            WinFormsStartupLogger.Write("Running main form.");
            System.Windows.Forms.Application.Run(form);
            WinFormsStartupLogger.Write("Application closed.");
        }
        catch (Exception exception)
        {
            WinFormsStartupLogger.WriteException("Startup failure", exception);
            MessageBox.Show(
                $"Impossible de demarrer LP2M_Bar_Mngt.{Environment.NewLine}{exception.Message}{Environment.NewLine}{Environment.NewLine}Log : {WinFormsStartupLogger.LogFilePath}",
                "Erreur de demarrage",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }    
}

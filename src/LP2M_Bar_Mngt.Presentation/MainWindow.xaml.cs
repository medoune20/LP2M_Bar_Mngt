using System.Windows;
using LP2M_Bar_Mngt.Presentation.Diagnostics;
using LP2M_Bar_Mngt.Presentation.ViewModels;

namespace LP2M_Bar_Mngt.Presentation;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        StartupLogger.Write("MainWindow constructor started.");
        InitializeComponent();
        DataContext = viewModel;
        StartupLogger.Write("MainWindow constructor completed.");
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        StartupLogger.Write("MainWindow loaded.");

        if (DataContext is MainViewModel viewModel)
        {
            await viewModel.InitializeAsync();
            StartupLogger.Write("MainWindow initialization completed.");
        }
    }
}

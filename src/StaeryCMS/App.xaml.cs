using System.Windows;
using System.Windows.Threading;
using StaeryCMS.Core.Services;
using StaeryCMS.Core.ViewModels;
using StaeryCMS.Services;

namespace StaeryCMS;

/// <summary>Composition root: wires services and view models together and shows the main window.</summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var viewModel = new MainViewModel(
            JsonContentRepository.CreateDefault(),
            new StaticSiteExporter(),
            new DialogService(),
            new ShellService(),
            TimeProvider.System);

        MainWindow = new MainWindow(viewModel);
        MainWindow.Show();
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"Something went wrong:\n\n{e.Exception.Message}",
            "StaeryCMS",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }
}

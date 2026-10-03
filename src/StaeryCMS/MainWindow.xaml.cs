using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using StaeryCMS.Core.ViewModels;

namespace StaeryCMS;

/// <summary>Main window. All behaviour lives in <see cref="MainViewModel"/>; this class only handles window lifetime and focus.</summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _closeConfirmed;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e) => await _viewModel.LoadCommand.ExecuteAsync(null);

    protected override async void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);

        if (_closeConfirmed || e.Cancel)
        {
            return;
        }

        // Closing must be decided synchronously, so cancel now and close again once unsaved work is handled.
        e.Cancel = true;

        if (await _viewModel.PrepareToCloseAsync())
        {
            _closeConfirmed = true;
            _ = Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(Close));
        }
    }

    private void OnFindExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }
}

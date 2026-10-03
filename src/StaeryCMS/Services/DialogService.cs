using System.Windows;
using Microsoft.Win32;
using StaeryCMS.Core.Abstractions;

namespace StaeryCMS.Services;

/// <summary>Message boxes and the folder picker, owned by the main window when it is visible.</summary>
internal sealed class DialogService : IDialogService
{
    private static Window? Owner => Application.Current?.MainWindow is { IsVisible: true } window ? window : null;

    public bool Confirm(string title, string message) =>
        Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public UnsavedChangesDecision AskToSaveChanges(string entryTitle) =>
        Show($"Save changes to \"{entryTitle}\" before continuing?", "Unsaved changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning) switch
        {
            MessageBoxResult.Yes => UnsavedChangesDecision.Save,
            MessageBoxResult.No => UnsavedChangesDecision.Discard,
            _ => UnsavedChangesDecision.Cancel,
        };

    public void ShowError(string title, string message) =>
        Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public string? PickFolder(string title)
    {
        var dialog = new OpenFolderDialog { Title = title };
        var owner = Owner;
        var accepted = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        return accepted == true ? dialog.FolderName : null;
    }

    private static MessageBoxResult Show(string message, string title, MessageBoxButton buttons, MessageBoxImage image) =>
        Owner is { } owner
            ? MessageBox.Show(owner, message, title, buttons, image)
            : MessageBox.Show(message, title, buttons, image);
}

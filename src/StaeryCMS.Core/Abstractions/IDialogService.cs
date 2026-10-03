namespace StaeryCMS.Core.Abstractions;

/// <summary>Answer to a "save changes?" prompt.</summary>
public enum UnsavedChangesDecision
{
    Save,
    Discard,
    Cancel,
}

/// <summary>User interaction that view models need but must not implement themselves.</summary>
public interface IDialogService
{
    bool Confirm(string title, string message);

    UnsavedChangesDecision AskToSaveChanges(string entryTitle);

    void ShowError(string title, string message);

    /// <summary>Returns the chosen folder, or <see langword="null"/> if the user cancelled.</summary>
    string? PickFolder(string title);
}

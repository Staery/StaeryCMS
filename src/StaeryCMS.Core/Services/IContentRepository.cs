using StaeryCMS.Core.Models;

namespace StaeryCMS.Core.Services;

/// <summary>Loads and stores the content library.</summary>
public interface IContentRepository
{
    /// <summary>Location of the underlying store, shown to the user.</summary>
    string Location { get; }

    /// <summary>Returns the stored library, or <see langword="null"/> if nothing has been saved yet.</summary>
    Task<ContentLibrary?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(ContentLibrary library, CancellationToken cancellationToken = default);
}

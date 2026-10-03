using System.Text.Json;
using System.Text.Json.Serialization;
using StaeryCMS.Core.Models;

namespace StaeryCMS.Core.Services;

/// <summary>Stores the content library as a single human-readable JSON file.</summary>
public sealed class JsonContentRepository(string filePath) : IContentRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public string Location { get; } = Path.GetFullPath(filePath);

    /// <summary>Default store under the current user's application data folder.</summary>
    public static JsonContentRepository CreateDefault()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "StaeryCMS");

        return new JsonContentRepository(Path.Combine(folder, "content.json"));
    }

    public async Task<ContentLibrary?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(Location))
        {
            return null;
        }

        ContentLibrary? library;

        try
        {
            await using var stream = File.OpenRead(Location);
            library = await JsonSerializer.DeserializeAsync<ContentLibrary>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            // Move the damaged file aside so the next save cannot overwrite what may still be recoverable.
            var backupPath = $"{Location}.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}";
            File.Move(Location, backupPath);

            throw new InvalidDataException(
                $"The content file was damaged and could not be read ({ex.Message}). It has been moved to '{backupPath}'.", ex);
        }

        return Normalize(library ?? new ContentLibrary());
    }

    public async Task SaveAsync(ContentLibrary library, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(library);

        Directory.CreateDirectory(Path.GetDirectoryName(Location)!);

        // Write to a temporary file first so a crash mid-write never leaves a truncated store behind.
        var tempPath = Location + ".tmp";

        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, library, SerializerOptions, cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, Location, overwrite: true);
    }

    private static ContentLibrary Normalize(ContentLibrary library)
    {
        // Older or hand-edited files may contain nulls where the model expects values.
        library.SiteTitle = string.IsNullOrWhiteSpace(library.SiteTitle) ? ContentLibrary.DefaultSiteTitle : library.SiteTitle;
        library.Items ??= [];
        library.Items.RemoveAll(item => item is null);

        foreach (var item in library.Items)
        {
            item.Title ??= string.Empty;
            item.Slug ??= string.Empty;
            item.Summary ??= string.Empty;
            item.Body ??= string.Empty;
            item.Category ??= string.Empty;
            item.Tags ??= [];
        }

        return library;
    }
}

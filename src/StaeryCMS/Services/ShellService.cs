using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using StaeryCMS.Core.Abstractions;

namespace StaeryCMS.Services;

/// <summary>Opens files, folders and previews with the user's default applications.</summary>
internal sealed class ShellService : IShellService
{
    private static readonly string PreviewFolder = Path.Combine(Path.GetTempPath(), "StaeryCMS", "preview");

    public void Open(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException($"Could not open '{path}': {ex.Message}", ex);
        }
    }

    public void ShowHtml(string html, string fileNameHint)
    {
        Directory.CreateDirectory(PreviewFolder);

        var path = Path.Combine(PreviewFolder, Path.GetFileName(fileNameHint));
        File.WriteAllText(path, html, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        Open(path);
    }
}

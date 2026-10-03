namespace StaeryCMS.Core.Abstractions;

/// <summary>Hands files and folders over to the operating system.</summary>
public interface IShellService
{
    /// <summary>Opens a file or folder with its default application.</summary>
    void Open(string path);

    /// <summary>Writes <paramref name="html"/> to a temporary file and opens it in the default browser.</summary>
    void ShowHtml(string html, string fileNameHint);
}

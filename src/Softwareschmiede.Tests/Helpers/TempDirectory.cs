namespace Softwareschmiede.Tests.Helpers;

/// <summary>Temporäres Verzeichnis, das beim Dispose rekursiv gelöscht wird.</summary>
public sealed class TempDirectory : IDisposable
{
    /// <summary>Pfad des angelegten Verzeichnisses.</summary>
    public string Path { get; }

    /// <summary>Legt ein neues Verzeichnis mit Zufallsnamen unter dem Temp-Pfad an.</summary>
    public TempDirectory()
        : this(System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N")))
    {
    }

    /// <summary>Legt das Verzeichnis am angegebenen Pfad an.</summary>
    /// <param name="path">Der Zielpfad.</param>
    public TempDirectory(string path)
    {
        Path = path;
        Directory.CreateDirectory(Path);
    }

    /// <inheritdoc/>
    public void Dispose() => Directory.Delete(Path, recursive: true);
}

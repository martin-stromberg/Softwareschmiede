namespace Softwareschmiede.Tests.Helpers;

/// <summary>Verwaltet temporäre Testverzeichnisse und löscht sie beim Dispose.</summary>
public sealed class TestTempDirectoryFixture : IDisposable
{
    private readonly List<string> _tempDirectories = [];

    /// <summary>Erstellt ein neues temporäres Verzeichnis mit dem angegebenen Präfix und registriert es zum Aufräumen.</summary>
    /// <param name="prefix">Der Präfix für den Verzeichnisnamen.</param>
    /// <returns>Der Pfad des erstellten Verzeichnisses.</returns>
    public string CreateTempDirectory(string prefix)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"{prefix}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        _tempDirectories.Add(directory);
        return directory;
    }

    /// <summary>Löscht alle erstellten temporären Verzeichnisse.</summary>
    public void Dispose()
    {
        foreach (var directory in _tempDirectories)
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}

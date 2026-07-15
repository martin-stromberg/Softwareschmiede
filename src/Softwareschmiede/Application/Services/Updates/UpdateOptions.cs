namespace Softwareschmiede.Application.Services.Updates;

/// <summary>Konfiguriert Repository, Asset und lokale Arbeitsverzeichnisse für Programmupdates.</summary>
public sealed class UpdateOptions
{
    /// <summary>GitHub-Repository-Owner.</summary>
    public string RepositoryOwner { get; init; } = "martin-stromberg";

    /// <summary>GitHub-Repository-Name.</summary>
    public string RepositoryName { get; init; } = "Softwareschmiede";

    /// <summary>Name des erwarteten Release-Assets.</summary>
    public string AssetName { get; init; } = "release.zip";

    /// <summary>Name des Update-Arbeitsverzeichnisses relativ zum Programmverzeichnis.</summary>
    public string UpdateDirectoryName { get; init; } = "updates";

    /// <summary>Timeout für HTTP-basierte Update-Prüfungen und Downloads.</summary>
    public TimeSpan CheckTimeout { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>Name der ausführbaren Datei im Release-Paket.</summary>
    public string ExecutableName { get; init; } = "Softwareschmiede.exe";
}

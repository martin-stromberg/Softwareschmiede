namespace Softwareschmiede.StatusServer.Update;

/// <summary>Host-seitige Bindung der <c>Update</c>-Konfigurationssektion. Enthält die
/// StatusServer-spezifischen Felder (Repository, Manifestname, Quellwahl), aus denen
/// <see cref="AutoUpdateApplicationExtensions.SetInitialConfiguration"/> die
/// <c>msTools.Updater</c>-Konfiguration aufbaut. Alle zur Laufzeit änderbaren Werte
/// (automatischer Download/Installation, Zeitlimits, Dienst-/Exe-Ziele,
/// Hosted Services) werden aus derselben Sektion ausschließlich auf
/// <see cref="msTools.Updater.AutoUpdateOptions"/> gebunden — nicht auf diese Klasse.</summary>
public sealed class StatusServerUpdateOptions
{
    /// <summary>Der Name der Konfigurationssektion, aus der diese Klasse gebunden wird.</summary>
    public const string SectionName = "Update";

    /// <summary>Der Besitzer (Benutzer oder Organisation) des GitHub-Repositories,
    /// das als Update-Quelle dient.</summary>
    public string RepositoryOwner { get; set; } = "martin-stromberg";

    /// <summary>Der Name des GitHub-Repositories, das als Update-Quelle dient.</summary>
    public string RepositoryName { get; set; } = "Softwareschmiede";

    /// <summary>Der Dateiname des Release-Manifests (separat vom
    /// Desktop-<c>update.json</c>, weil die Paketwahl nur über Plattform +
    /// Runtime-Identifier läuft).</summary>
    public string ManifestAssetName { get; set; } = "statusserver-update.json";

    /// <summary>Das Wurzelverzeichnis für Update-Pakete, Status- und Lock-Dateien.</summary>
    public string WorkingDirectory { get; set; } = "updates";

    /// <summary>Die eingesetzte Update-Quelle: <c>Github</c> oder <c>LocalFolder</c>.</summary>
    public string SourceType { get; set; } = "Github";

    /// <summary>Das lokale Verzeichnis der LocalFolder-Quelle, sofern
    /// <see cref="SourceType"/> <c>LocalFolder</c> ist (Tests/Offline-Betrieb).</summary>
    public string? LocalFolderPath { get; set; }

    /// <summary>Der inklusive Beginn des täglichen Prüfzeitfensters.</summary>
    public TimeOnly SourceCheckStartTime { get; set; } = StatusServerAutoUpdateOptionsMapper.DefaultSourceCheckStartTime;

    /// <summary>Das exklusive Ende des täglichen Prüfzeitfensters.</summary>
    public TimeOnly SourceCheckEndTime { get; set; } = StatusServerAutoUpdateOptionsMapper.DefaultSourceCheckEndTime;
}

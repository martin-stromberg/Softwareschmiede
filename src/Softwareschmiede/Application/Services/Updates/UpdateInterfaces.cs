namespace Softwareschmiede.Application.Services.Updates;

/// <summary>Liest die lokal installierte Programmversion.</summary>
public interface IApplicationVersionProvider
{
    /// <summary>Liest die installierte Version oder gibt <c>null</c> zurück, wenn sie nicht prüfbar ist.</summary>
    Task<InstalledVersionInfo?> GetInstalledVersionAsync(CancellationToken ct = default);
}

/// <summary>Ruft die neueste stabile Release-Information ab.</summary>
public interface IUpdateReleaseClient
{
    /// <summary>Ruft die neueste stabile Release-Information ab oder gibt <c>null</c> zurück, wenn sie nicht nutzbar ist.</summary>
    Task<UpdateInfo?> GetLatestStableReleaseAsync(CancellationToken ct = default);
}

/// <summary>Orchestriert Update-Prüfung, Vorbereitung und Start des externen Updaters.</summary>
public interface IUpdateService
{
    /// <summary>Prüft, ob ein neueres Update verfügbar ist.</summary>
    Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken ct = default);

    /// <summary>Bereitet Download, Entpacken und Skript vor.</summary>
    Task<UpdatePreparationResult> PrepareUpdateAsync(
        UpdateInfo update,
        IProgress<UpdatePreparationProgress>? progress,
        CancellationToken ct = default);

    /// <summary>Startet das vorbereitete externe Update.</summary>
    Task StartPreparedUpdateAsync(UpdatePreparationResult preparation, CancellationToken ct = default);
}

/// <summary>Bewertet aktive CLI-Aufgaben vor einem Update.</summary>
public interface ICliUpdateSafetyService
{
    /// <summary>Liefert riskante aktive CLI-Aufgaben, die nicht auf Eingabe warten.</summary>
    Task<CliUpdateSafetyResult> CheckAsync(CancellationToken ct = default);
}

/// <summary>Verwaltet Download, Entpacken und Validierung eines Update-Pakets.</summary>
public interface IUpdatePackageService
{
    /// <summary>Lädt ein Update herunter, entpackt es und erzeugt die nötigen Update-Artefakte.</summary>
    Task<UpdatePreparationResult> PreparePackageAsync(
        UpdateInfo update,
        IProgress<UpdatePreparationProgress>? progress,
        CancellationToken ct = default);
}

/// <summary>Erzeugt und startet das externe Update-Skript.</summary>
public interface IUpdateScriptService
{
    /// <summary>Schreibt das Update-Skript für die angegebenen Pfade.</summary>
    Task<string> CreateScriptAsync(
        string targetDirectory,
        string extractedDirectory,
        string executableName,
        string logPath,
        CancellationToken ct = default);

    /// <summary>Startet ein vorbereitetes Update-Skript.</summary>
    Task StartScriptAsync(UpdatePreparationResult preparation, CancellationToken ct = default);
}

/// <summary>Startet externe Update-Prozesse testbar gekapselt.</summary>
public interface IUpdateProcessLauncher
{
    /// <summary>Startet den externen Prozess.</summary>
    bool Start(string fileName, IEnumerable<string> arguments, string workingDirectory, bool runElevated);
}

/// <summary>Kapselt das geordnete Beenden der Anwendung nach gestartetem Updater.</summary>
public interface IApplicationShutdownService
{
    /// <summary>Beendet die aktuell laufende Anwendung geordnet.</summary>
    void Shutdown();
}

using msTools.Updater;
using Softwareschmiede.StatusServer.Security;

namespace Softwareschmiede.StatusServer.Update;

/// <summary>Erweiterungen zur Verdrahtung der <c>msTools.Updater</c>-Integration in
/// <c>Program.cs</c>: initiale Bibliotheks-Konfiguration und nachträgliches Anwenden
/// der in <c>ServerSettings</c> persistierten Admin-Einstellungen vor
/// <c>app.Run()</c>.</summary>
public static class AutoUpdateApplicationExtensions
{
    /// <summary>Name der systemd-Transient-Unit, unter der das Installationsskript
    /// auf Linux detached gestartet wird (muss pro Anwendung eindeutig sein).</summary>
    private const string UpdateUnitName = "SoftwareschmiedeStatusServerUpdate";

    /// <summary>Überträgt die StatusServer-spezifische <c>Update</c>-Sektion auf den
    /// <see cref="AutoUpdateBuilder"/>: Bindung der Bibliotheksoptionen aus derselben
    /// Sektion, Update-Unit-Name, Workspace-Pfad, tägliches Prüfzeitfenster
    /// (24h-Intervall), geschützte Dateien (<c>appsettings*.json</c>, <c>data/*</c>
    /// sowie der aufgelöste <c>Data:DatabasePath</c>, sofern er innerhalb des
    /// Anwendungsverzeichnisses, aber außerhalb der <c>data/*</c>-Abdeckung liegt)
    /// und die Update-Quelle je <see cref="StatusServerUpdateOptions.SourceType"/>.
    /// Explizit gesetzte Fluent-Werte gewinnen gegen die Konfigurationsbindung.</summary>
    /// <param name="cfg">Der Builder der <c>UseAutoUpdate</c>-Registrierung.</param>
    /// <param name="updateOptions">Die gebundene <c>Update</c>-Sektion.</param>
    /// <param name="databasePath">Der aufgelöste SQLite-Pfad aus
    /// <c>Data:DatabasePath</c> (relativ zum ContentRoot oder absolut).</param>
    /// <param name="contentRootPath">Das Anwendungsverzeichnis, auf das sich die
    /// <c>PreserveFile</c>-Muster beziehen.</param>
    /// <returns>Der Builder für die Verkettung.</returns>
    public static AutoUpdateBuilder SetInitialConfiguration(
        this AutoUpdateBuilder cfg,
        StatusServerUpdateOptions updateOptions,
        string databasePath,
        string contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(cfg);
        ArgumentNullException.ThrowIfNull(updateOptions);
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);

        cfg.BindConfiguration(StatusServerUpdateOptions.SectionName);
        cfg.WithUpdateUnitName(UpdateUnitName);
        if (!string.IsNullOrWhiteSpace(updateOptions.WorkingDirectory))
            cfg.WithDownloadPath(updateOptions.WorkingDirectory);

        cfg.WithSourceCheck(
            StatusServerAutoUpdateOptionsMapper.DailySourceCheckIntervalMinutes,
            StatusServerAutoUpdateOptionsMapper.BuildSourceCheckTimeRanges(
                updateOptions.SourceCheckStartTime, updateOptions.SourceCheckEndTime));

        // Datenerhalt beim Dateiaustausch: Datenbestand und lokale Konfiguration
        // werden vor dem Update gesichert und danach restauriert.
        cfg.PreserveFile("appsettings*.json");
        cfg.PreserveFile("data/*");
        PreserveDatabaseFiles(cfg, databasePath, contentRootPath);

        if (string.Equals(updateOptions.SourceType, "LocalFolder", StringComparison.OrdinalIgnoreCase))
        {
            cfg.UseLocalFolderSource(
                string.IsNullOrWhiteSpace(updateOptions.LocalFolderPath)
                    ? Path.Combine(updateOptions.WorkingDirectory, "source")
                    : updateOptions.LocalFolderPath,
                updateOptions.ManifestAssetName);
        }
        else
        {
            cfg.UseGithubSource(updateOptions.RepositoryOwner, updateOptions.RepositoryName, updateOptions.ManifestAssetName);
        }

        return cfg;
    }

    /// <summary>Wendet die in der <c>ServerSettings</c>-Tabelle persistierten
    /// Admin-Einstellungen auf den <see cref="AutoUpdateOptions"/>-Singleton an und
    /// baut die Update-Quelle token- und kanalbewusst neu. Muss vor
    /// <c>app.Run()</c> laufen, damit die Bibliotheks-Hintergrunddienste
    /// (<c>AutoUpdateCheckerService</c>/<c>AutoUpdateSchedulerService</c>) beim Start
    /// die Admin-Werte statt nur der appsettings-Defaults sehen. Fehler werden
    /// geloggt und geschluckt — korrupte Einstellungen blockieren den Start nicht.</summary>
    /// <param name="app">Die Webanwendung.</param>
    /// <returns>Eine abgeschlossene Aufgabe.</returns>
    public static async Task ApplyPersistedUpdateSettingsAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        try
        {
            using var scope = app.Services.CreateScope();
            var settings = await scope.ServiceProvider
                .GetRequiredService<ServerSettingsService>()
                .GetUpdateSettingsAsync()
                .ConfigureAwait(false);

            var options = app.Services.GetRequiredService<AutoUpdateOptions>();
            StatusServerAutoUpdateOptionsMapper.ApplySettings(options, settings);

            var updateOptions = app.Services.GetRequiredService<StatusServerUpdateOptions>();
            options.SourceCheck.TimeRanges = StatusServerAutoUpdateOptionsMapper
                .BuildSourceCheckTimeRanges(updateOptions.SourceCheckStartTime, updateOptions.SourceCheckEndTime)
                .ToList();

            app.Services
                .GetRequiredService<StatusServerUpdateSourceFactory>()
                .ReplaceSource(options, settings);
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex,
                "Persistierte Update-Einstellungen konnten beim Start nicht angewendet werden; " +
                "es gelten die appsettings-konfigurierten Defaults.");
        }
    }

    /// <summary>Schützt einen per <c>Data:DatabasePath</c> konfigurierten
    /// SQLite-Pfad, der innerhalb des Anwendungsverzeichnisses, aber außerhalb der
    /// <c>data/*</c>-Abdeckung liegt (der Wildcard ist segmentbezogen — er deckt
    /// weder das Wurzelverzeichnis noch Unterverzeichnisse von <c>data/</c> ab).
    /// Die SQLite-Seitendateien (<c>-wal</c>/<c>-shm</c>/<c>-journal</c>) werden
    /// über <c>&lt;pfad&gt;-*</c> mitgeschützt. Ein Pfad außerhalb des
    /// Anwendungsverzeichnisses wird vom Dateiaustausch ohnehin nicht berührt
    /// und braucht keinen Schutz (ein <c>PreserveFile</c>-Muster darf zudem
    /// weder verankert noch <c>..</c>-Segmente enthalten).</summary>
    /// <param name="cfg">Der Builder der <c>UseAutoUpdate</c>-Registrierung.</param>
    /// <param name="databasePath">Der aufgelöste <c>Data:DatabasePath</c>-Wert.</param>
    /// <param name="contentRootPath">Das Anwendungsverzeichnis.</param>
    private static void PreserveDatabaseFiles(AutoUpdateBuilder cfg, string databasePath, string contentRootPath)
    {
        var absolutePath = Path.GetFullPath(Path.IsPathRooted(databasePath)
            ? databasePath
            : Path.Combine(contentRootPath, databasePath));
        var relative = Path.GetRelativePath(contentRootPath, absolutePath);
        if (relative == ".." || relative.StartsWith("..\\", StringComparison.Ordinal)
            || relative.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            return;
        }

        // Ein PreserveFile-Muster ist relativ zum Anwendungsverzeichnis und nutzt
        // '/' als Segment-Trenner (plattformübergreifend im generierten Skript).
        var normalized = relative.Replace('\\', '/');
        const string dataPrefix = "data/";
        if (normalized.StartsWith(dataPrefix, StringComparison.Ordinal)
            && !normalized[dataPrefix.Length..].Contains('/'))
        {
            // Direkt unter data/ — bereits über PreserveFile("data/*") abgedeckt.
            return;
        }

        cfg.PreserveFile(normalized);
        cfg.PreserveFile(normalized + "-*");
    }
}

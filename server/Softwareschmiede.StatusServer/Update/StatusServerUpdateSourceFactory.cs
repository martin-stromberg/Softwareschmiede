using msTools.Updater;

namespace Softwareschmiede.StatusServer.Update;

/// <summary>Baut die Update-Quelle (<see cref="IAutoUpdateSource"/>) passend zur
/// konfigurierten Quellart und zum persistierten Kanal. Die GitHub-Quelle wird
/// über den öffentlichen Konstruktor mit dem benannten
/// <see cref="IHttpClientFactory"/>-Client <see cref="HttpClientName"/> erzeugt —
/// nur so kann das optionale <c>GitHub:Token</c> als Bearer-Header einfließen
/// (<c>AutoUpdateBuilder.UseGithubSource</c> kennt keinen Token-Parameter) und der
/// Client bleibt factory-verwaltet statt von der Quelle disposed zu werden.</summary>
public sealed class StatusServerUpdateSourceFactory
{
    /// <summary>Name des in <c>Program.cs</c> registrierten <see cref="HttpClient"/>s
    /// für GitHub-Anfragen der Update-Quelle.</summary>
    public const string HttpClientName = "StatusServerUpdate";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAutoUpdatePlatformResolver _platformResolver;
    private readonly StatusServerUpdateOptions _updateOptions;

    /// <summary>Erstellt die Factory.</summary>
    /// <param name="httpClientFactory">Die Factory für den benannten Update-HttpClient.</param>
    /// <param name="platformResolver">Der Plattform-Resolver der Bibliothek
    /// (Paketwahl über Plattform + Runtime-Identifier).</param>
    /// <param name="updateOptions">Die gebundene <c>Update</c>-Konfiguration.</param>
    public StatusServerUpdateSourceFactory(
        IHttpClientFactory httpClientFactory,
        IAutoUpdatePlatformResolver platformResolver,
        StatusServerUpdateOptions updateOptions)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(platformResolver);
        ArgumentNullException.ThrowIfNull(updateOptions);
        _httpClientFactory = httpClientFactory;
        _platformResolver = platformResolver;
        _updateOptions = updateOptions;
    }

    /// <summary>Erzeugt die Update-Quelle passend zu <see cref="StatusServerUpdateOptions.SourceType"/>:
    /// <c>AutoUpdateLocalFolderSource</c> bei <c>LocalFolder</c>, sonst eine
    /// token- und kanalbewusste <see cref="AutoUpdateGithubSource"/>.</summary>
    /// <param name="settings">Die aktuell wirksamen Admin-Einstellungen — der Kanal
    /// steuert <c>includePrereleases</c> der GitHub-Quelle.</param>
    /// <returns>Die Update-Quelle.</returns>
    public IAutoUpdateSource Create(StatusServerUpdateSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (string.Equals(_updateOptions.SourceType, "LocalFolder", StringComparison.OrdinalIgnoreCase))
        {
            var directory = string.IsNullOrWhiteSpace(_updateOptions.LocalFolderPath)
                ? Path.Combine(_updateOptions.WorkingDirectory, "source")
                : _updateOptions.LocalFolderPath;
            return new AutoUpdateLocalFolderSource(directory, _platformResolver, _updateOptions.ManifestAssetName);
        }

        return new AutoUpdateGithubSource(
            _httpClientFactory.CreateClient(HttpClientName),
            _updateOptions.RepositoryOwner,
            _updateOptions.RepositoryName,
            _platformResolver,
            _updateOptions.ManifestAssetName,
            settings.Channel == StatusServerUpdateChannel.ReleaseCandidate);
    }

    /// <summary>Ersetzt <see cref="AutoUpdateOptions.Source"/> durch eine zu
    /// <paramref name="settings"/> neu gebaute Quelle und disposed einen
    /// Vorgänger-<see cref="AutoUpdateGithubSource"/> (dessen HttpClient bleibt
    /// factory-verwaltet). Wird sowohl beim Start
    /// (<c>ApplyPersistedUpdateSettingsAsync</c>) als auch beim Speichern der
    /// Admin-Einstellungen (<see cref="StatusServerUpdateService.ApplySettingsAsync"/>)
    /// verwendet, damit der Austausch nur an einer Stelle gepflegt wird.</summary>
    /// <param name="options">Der laufzeitveränderbare Options-Singleton.</param>
    /// <param name="settings">Die aktuell wirksamen Admin-Einstellungen.</param>
    public void ReplaceSource(AutoUpdateOptions options, StatusServerUpdateSettings settings)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(settings);

        var previous = options.Source;
        options.Source = Create(settings);
        (previous as AutoUpdateGithubSource)?.Dispose();
    }
}

using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using msTools.Updater;

namespace Softwareschmiede.StatusServer.Tests.TestSupport;

/// <summary><see cref="StatusWebApplicationFactory"/> mit lokaler Update-Quelle
/// (<c>Update:SourceType=LocalFolder</c>) und einer festen installierten Version —
/// damit Update-Tests deterministisch und ohne GitHub-Zugriff laufen.</summary>
public class UpdateTestFactory : StatusWebApplicationFactory
{
    private readonly string _localFolderPath;

    /// <summary>Erstellt die Factory.</summary>
    /// <param name="localFolderPath">Das Verzeichnis der lokalen Update-Quelle
    /// (z. B. <see cref="TempUpdateSource.DirectoryPath"/>).</param>
    /// <param name="installedVersion">Die gemeldete installierte Version.</param>
    public UpdateTestFactory(string localFolderPath, string installedVersion = "1.0.0")
    {
        _localFolderPath = localFolderPath;
        InstalledVersion = installedVersion;
    }

    /// <summary>Die Version, die der ersetzte <see cref="IInstalledVersionProvider"/> meldet.</summary>
    public string InstalledVersion { get; }

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Update:SourceType", "LocalFolder");
        builder.UseSetting("Update:LocalFolderPath", _localFolderPath);
        builder.ConfigureTestServices(services =>
        {
            // Ohne release-metadata.json im Test-Host wäre die installierte Version
            // unbekannt — ein fester Provider macht den Versionsvergleich deterministisch.
            services.RemoveAll<IInstalledVersionProvider>();
            services.AddSingleton<IInstalledVersionProvider>(new FixedInstalledVersionProvider(InstalledVersion));
        });
    }

    private sealed class FixedInstalledVersionProvider(string version) : IInstalledVersionProvider
    {
        /// <inheritdoc/>
        public Task<InstalledReleaseInfo> GetAsync(CancellationToken ct)
            => Task.FromResult(new InstalledReleaseInfo(
                version, null, null, null, RuntimeInformation.RuntimeIdentifier));
    }
}

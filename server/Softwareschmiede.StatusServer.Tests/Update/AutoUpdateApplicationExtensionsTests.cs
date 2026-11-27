using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using msTools.Updater;
using Softwareschmiede.StatusServer.Update;

namespace Softwareschmiede.StatusServer.Tests.Update;

/// <summary>Tests für <see cref="AutoUpdateApplicationExtensions.SetInitialConfiguration"/> —
/// Schwerpunkt: der Datenerhalt beim Dateiaustausch (geschützte Dateien in
/// <see cref="AutoUpdateOptions.ProtectedFiles"/>), insbesondere ein per
/// <c>Data:DatabasePath</c> konfigurierter SQLite-Pfad außerhalb der
/// <c>data/*</c>-Abdeckung.</summary>
public sealed class AutoUpdateApplicationExtensionsTests
{
    private static readonly string s_contentRoot =
        Path.Combine(Path.GetTempPath(), $"statusserver-contentroot-{Guid.NewGuid():N}");

    private static List<string> ProtectedPaths(string databasePath)
    {
        // Über die echte UseAutoUpdate-Verdrahtung — der Fluent-Builder der
        // Bibliothek hat keine öffentliche Auslese für ProtectedFiles.
        var hostBuilder = Host.CreateApplicationBuilder();
        var updateOptions = new StatusServerUpdateOptions
        {
            SourceType = "LocalFolder",
            LocalFolderPath = Path.GetTempPath(),
        };
        hostBuilder.UseAutoUpdate(
            cfg => cfg.SetInitialConfiguration(updateOptions, databasePath, s_contentRoot));
        using var host = hostBuilder.Build();
        return host.Services.GetRequiredService<AutoUpdateOptions>()
            .ProtectedFiles.Select(f => f.Path).ToList();
    }

    /// <summary>Der Default-Pfad <c>data/statusserver.db</c> ist über
    /// <c>data/*</c> abgedeckt — es werden keine zusätzlichen Einträge registriert.</summary>
    [Fact]
    public void DatabasePath_UnderDataDirectory_AddsNoExtraEntries()
    {
        var paths = ProtectedPaths(Path.Combine(s_contentRoot, "data", "statusserver.db"));

        paths.Should().BeEquivalentTo("appsettings*.json", "data/*");
    }

    /// <summary>Ein konfigurierter DB-Pfad im Wurzelverzeichnis des
    /// Anwendungsverzeichnisses (absolut wie relativ angegeben) wird samt
    /// SQLite-Seitendateien (<c>-wal</c>/<c>-shm</c>) geschützt —
    /// <c>data/*</c> deckt ihn nicht ab.</summary>
    [Fact]
    public void DatabasePath_InAppRoot_IsPreservedWithSideFiles()
    {
        var expected = new[] { "appsettings*.json", "data/*", "statusserver.db", "statusserver.db-*" };

        ProtectedPaths(Path.Combine(s_contentRoot, "statusserver.db"))
            .Should().BeEquivalentTo(expected);
        ProtectedPaths("statusserver.db")
            .Should().BeEquivalentTo(expected, "relative Pfade werden zum ContentRoot aufgelöst");
    }

    /// <summary>Ein DB-Pfad in einem Unterverzeichnis von <c>data/</c> ist nicht
    /// vom segmentbezogenen <c>data/*</c>-Wildcard abgedeckt und wird einzeln
    /// geschützt.</summary>
    [Fact]
    public void DatabasePath_InDataSubdirectory_IsPreserved()
    {
        var paths = ProtectedPaths(Path.Combine(s_contentRoot, "data", "nested", "statusserver.db"));

        paths.Should().BeEquivalentTo(
            "appsettings*.json", "data/*", "data/nested/statusserver.db", "data/nested/statusserver.db-*");
    }

    /// <summary>Ein DB-Pfad außerhalb des Anwendungsverzeichnisses wird vom
    /// Dateiaustausch ohnehin nicht berührt — es wird kein ungültiges
    /// (verankertes/<c>..</c>-haltiges) Muster registriert.</summary>
    [Fact]
    public void DatabasePath_OutsideAppDirectory_AddsNoEntries()
    {
        var outside = Path.Combine(Path.GetTempPath(), $"statusserver-outside-{Guid.NewGuid():N}", "db.sqlite");

        var paths = ProtectedPaths(outside);

        paths.Should().BeEquivalentTo("appsettings*.json", "data/*");
    }
}

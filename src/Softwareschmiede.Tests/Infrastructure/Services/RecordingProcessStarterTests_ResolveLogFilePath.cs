using FluentAssertions;
using Softwareschmiede.Infrastructure.Services;

namespace Softwareschmiede.Tests.Infrastructure.Services;

/// <summary>Tests für die Pfadauflösung von <see cref="RecordingProcessStarter.ResolveLogFilePath"/>.</summary>
public sealed class RecordingProcessStarterTests_ResolveLogFilePath
{
    /// <summary>Prüft den Fallback auf das temporäre Verzeichnis bei <c>null</c> testDbPath.</summary>
    [Fact]
    public void ResolveLogFilePath_WithNullTestDbPath_ReturnsPathInTempDirectoryBack()
    {
        var path = RecordingProcessStarter.ResolveLogFilePath(null);

        path.Should().Be(Path.Combine(Path.GetTempPath(), RecordingProcessStarter.LogFileName));
    }

    /// <summary>Prüft den Fallback auf das temporäre Verzeichnis bei leerem testDbPath.</summary>
    [Fact]
    public void ResolveLogFilePath_WithEmptyTestDbPath_ReturnsPathInTempDirectoryBack()
    {
        var path = RecordingProcessStarter.ResolveLogFilePath(string.Empty);

        path.Should().Be(Path.Combine(Path.GetTempPath(), RecordingProcessStarter.LogFileName));
    }

    /// <summary>Prüft, dass die Logdatei bei gesetztem testDbPath neben der Test-Datenbank abgelegt wird.</summary>
    [Fact]
    public void ResolveLogFilePath_WithTestDbPath_ReturnsLogfileBesideDbBack()
    {
        var testDbPath = Path.Combine(Path.GetTempPath(), $"resolve_log_datei_pfad_tests_{Guid.NewGuid():N}", "test.db");

        var path = RecordingProcessStarter.ResolveLogFilePath(testDbPath);

        path.Should().Be(Path.Combine(Path.GetDirectoryName(testDbPath)!, RecordingProcessStarter.LogFileName));
    }

    /// <summary>Prüft den Fallback auf das temporäre Verzeichnis, wenn aus testDbPath kein Verzeichnisanteil ermittelbar ist (z. B. bei einem reinen Laufwerkswurzelpfad).</summary>
    [Fact]
    public void ResolveLogFilePath_WithTestDbPathWithoutResolvableDirectory_ReturnsPathInTempDirectoryBack()
    {
        var path = RecordingProcessStarter.ResolveLogFilePath(@"C:\");

        path.Should().Be(Path.Combine(Path.GetTempPath(), RecordingProcessStarter.LogFileName));
    }
}

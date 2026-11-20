using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Services;

namespace Softwareschmiede.Tests.Infrastructure.Services;

/// <summary>Tests für den AufzeichnenderProzessStarter.</summary>
public sealed class AufzeichnenderProzessStarterTests : IDisposable
{
    private readonly string _testDbPath;

    /// <summary>Legt einen temporären Test-DB-Pfad an.</summary>
    public AufzeichnenderProzessStarterTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"aufzeichnender_prozess_starter_tests_{Guid.NewGuid():N}.db");
    }

    /// <summary>Löscht die Logdatei, falls angelegt.</summary>
    public void Dispose()
    {
        var logFilePath = RecordingProcessStarter.ResolveLogFilePath(_testDbPath);
        if (File.Exists(logFilePath))
            File.Delete(logFilePath);
    }

    /// <summary>Prüft, dass Starten die ProzessStartAnfrage als Zeile an die Logdatei neben der Test-DB anhängt.</summary>
    [Fact]
    public void Starten_SchreibtAnfrageInLogdatei()
    {
        var logFilePath = RecordingProcessStarter.ResolveLogFilePath(_testDbPath);
        var starter = new RecordingProcessStarter(NullLogger<RecordingProcessStarter>.Instance, logFilePath);
        var request = new ProcessStartRequest("explorer.exe", @"C:\Temp\Arbeitsverzeichnis", UseShellExecute: false);

        starter.Start(request);

        File.Exists(logFilePath).Should().BeTrue();
        var inhalt = File.ReadAllText(logFilePath);
        inhalt.Should().Contain("explorer.exe");
        inhalt.Should().Contain(@"C:\Temp\Arbeitsverzeichnis");
    }

    /// <summary>Prüft, dass Starten die Zeile exakt im Format "DateiName|Argumente|ShellAusfuehren" gefolgt von einem Zeilenumbruch schreibt.</summary>
    [Fact]
    public void Starten_SchreibtZeileImExaktenPipeGetrenntenFormat()
    {
        var logFilePath = RecordingProcessStarter.ResolveLogFilePath(_testDbPath);
        var starter = new RecordingProcessStarter(NullLogger<RecordingProcessStarter>.Instance, logFilePath);
        var request = new ProcessStartRequest("explorer.exe", @"C:\Temp\Arbeitsverzeichnis", UseShellExecute: true);

        starter.Start(request);

        var inhalt = File.ReadAllText(logFilePath);
        inhalt.Should().Be($@"explorer.exe|C:\Temp\Arbeitsverzeichnis|True{Environment.NewLine}");
    }
}

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
        var logDateiPfad = AufzeichnenderProzessStarter.ResolveLogDateiPfad(_testDbPath);
        if (File.Exists(logDateiPfad))
            File.Delete(logDateiPfad);
    }

    /// <summary>Prüft, dass Starten die ProzessStartAnfrage als Zeile an die Logdatei neben der Test-DB anhängt.</summary>
    [Fact]
    public void Starten_SchreibtAnfrageInLogdatei()
    {
        var logDateiPfad = AufzeichnenderProzessStarter.ResolveLogDateiPfad(_testDbPath);
        var starter = new AufzeichnenderProzessStarter(NullLogger<AufzeichnenderProzessStarter>.Instance, logDateiPfad);
        var anfrage = new ProzessStartAnfrage("explorer.exe", @"C:\Temp\Arbeitsverzeichnis", ShellAusfuehren: false);

        starter.Starten(anfrage);

        File.Exists(logDateiPfad).Should().BeTrue();
        var inhalt = File.ReadAllText(logDateiPfad);
        inhalt.Should().Contain("explorer.exe");
        inhalt.Should().Contain(@"C:\Temp\Arbeitsverzeichnis");
    }

    /// <summary>Prüft, dass Starten die Zeile exakt im Format "DateiName|Argumente|ShellAusfuehren" gefolgt von einem Zeilenumbruch schreibt.</summary>
    [Fact]
    public void Starten_SchreibtZeileImExaktenPipeGetrenntenFormat()
    {
        var logDateiPfad = AufzeichnenderProzessStarter.ResolveLogDateiPfad(_testDbPath);
        var starter = new AufzeichnenderProzessStarter(NullLogger<AufzeichnenderProzessStarter>.Instance, logDateiPfad);
        var anfrage = new ProzessStartAnfrage("explorer.exe", @"C:\Temp\Arbeitsverzeichnis", ShellAusfuehren: true);

        starter.Starten(anfrage);

        var inhalt = File.ReadAllText(logDateiPfad);
        inhalt.Should().Be($@"explorer.exe|C:\Temp\Arbeitsverzeichnis|True{Environment.NewLine}");
    }
}

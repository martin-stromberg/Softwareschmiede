using FluentAssertions;
using Softwareschmiede.Infrastructure.Services;

namespace Softwareschmiede.Tests.Infrastructure.Services;

/// <summary>Tests für die Pfadauflösung von <see cref="AufzeichnenderProzessStarter.ResolveLogDateiPfad"/>.</summary>
public sealed class AufzeichnenderProzessStarterTests_ResolveLogDateiPfad
{
    /// <summary>Prüft den Fallback auf das temporäre Verzeichnis bei <c>null</c> testDbPath.</summary>
    [Fact]
    public void ResolveLogDateiPfad_MitNullTestDbPath_GibtPfadImTempVerzeichnisZurueck()
    {
        var pfad = AufzeichnenderProzessStarter.ResolveLogDateiPfad(null);

        pfad.Should().Be(Path.Combine(Path.GetTempPath(), AufzeichnenderProzessStarter.LogDateiName));
    }

    /// <summary>Prüft den Fallback auf das temporäre Verzeichnis bei leerem testDbPath.</summary>
    [Fact]
    public void ResolveLogDateiPfad_MitLeeremTestDbPath_GibtPfadImTempVerzeichnisZurueck()
    {
        var pfad = AufzeichnenderProzessStarter.ResolveLogDateiPfad(string.Empty);

        pfad.Should().Be(Path.Combine(Path.GetTempPath(), AufzeichnenderProzessStarter.LogDateiName));
    }

    /// <summary>Prüft, dass die Logdatei bei gesetztem testDbPath neben der Test-Datenbank abgelegt wird.</summary>
    [Fact]
    public void ResolveLogDateiPfad_MitTestDbPath_GibtLogdateiNebenDbZurueck()
    {
        var testDbPath = Path.Combine(Path.GetTempPath(), $"resolve_log_datei_pfad_tests_{Guid.NewGuid():N}", "test.db");

        var pfad = AufzeichnenderProzessStarter.ResolveLogDateiPfad(testDbPath);

        pfad.Should().Be(Path.Combine(Path.GetDirectoryName(testDbPath)!, AufzeichnenderProzessStarter.LogDateiName));
    }

    /// <summary>Prüft den Fallback auf das temporäre Verzeichnis, wenn aus testDbPath kein Verzeichnisanteil ermittelbar ist (z. B. bei einem reinen Laufwerkswurzelpfad).</summary>
    [Fact]
    public void ResolveLogDateiPfad_MitTestDbPathOhneErmittelbaresVerzeichnis_GibtPfadImTempVerzeichnisZurueck()
    {
        var pfad = AufzeichnenderProzessStarter.ResolveLogDateiPfad(@"C:\");

        pfad.Should().Be(Path.Combine(Path.GetTempPath(), AufzeichnenderProzessStarter.LogDateiName));
    }
}

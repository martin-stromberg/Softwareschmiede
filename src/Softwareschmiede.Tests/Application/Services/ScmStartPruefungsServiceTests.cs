using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;
using Xunit;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Signalfassade der ereignisgesteuerten Startprüfung (Issue 410, Schritt 3):
/// Pausen-Signale und sinkende Laufzahlen werden koalesziert angenommen; steigende
/// Laufzahlen und Dispose-Pfade lösen nichts aus.</summary>
public sealed class ScmStartPruefungsServiceTests
{
    /// <summary>Ein explizites Pausen-Signal wird als ausstehende Prüfung koalesziert.</summary>
    [Fact]
    public void Ausloeser_SignalWirdAngenommen()
    {
        using var fixture = new Fixture();
        fixture.Ausloeser.StartpruefungAnfordern();
        fixture.Ausloeser.StartpruefungAnfordern();

        Assert.Equal(1, fixture.Service.AusstehendePruefungen);
    }

    /// <summary>Nur eine sinkende Laufzahl (frei werdender Platz) löst die Prüfung aus;
    /// steigende oder gleiche Werte — etwa ein neuer CLI-Start — lösen nichts aus.</summary>
    [Fact]
    public void Laufzahl_NurRueckgangLoestAus()
    {
        using var fixture = new Fixture();

        fixture.Laufzaehlung.Raise(x => x.RunningCountChanged += null, 1, 2);
        Assert.Equal(0, fixture.Service.AusstehendePruefungen);

        fixture.Laufzaehlung.Raise(x => x.RunningCountChanged += null, 2, 2);
        Assert.Equal(0, fixture.Service.AusstehendePruefungen);

        fixture.Laufzaehlung.Raise(x => x.RunningCountChanged += null, 2, 1);
        Assert.Equal(1, fixture.Service.AusstehendePruefungen);
    }

    /// <summary>Ein Rückgang auf null (letzter laufender Prozess) löst ebenfalls aus.</summary>
    [Fact]
    public void Laufzahl_RueckgangAufNullLoestAus()
    {
        using var fixture = new Fixture();
        fixture.Laufzaehlung.Raise(x => x.RunningCountChanged += null, 1, 0);
        Assert.Equal(1, fixture.Service.AusstehendePruefungen);
    }

    /// <summary>Nach Dispose sind die Abonnements getrennt: weitere Signale schlagen weder
    /// fehl noch erhöhen sie den Zähler.</summary>
    [Fact]
    public void Dispose_TrenntAbonnements()
    {
        var fixture = new Fixture();
        fixture.Service.Dispose();

        fixture.Ausloeser.StartpruefungAnfordern();
        fixture.Laufzaehlung.Raise(x => x.RunningCountChanged += null, 2, 1);
        Assert.Equal(0, fixture.Service.AusstehendePruefungen);
    }

    private sealed class Fixture : IDisposable
    {
        public ScmStartPruefungsAusloeser Ausloeser { get; } = new();
        public Mock<IRunningAutomationStatusSource> Laufzaehlung { get; } = new();
        public ScmStartPruefungsService Service { get; }

        public Fixture()
        {
            var scopeFactory = new Mock<IServiceScopeFactory>();
            var gate = new ScmEventStartupGate();
            gate.Ready();
            Service = new ScmStartPruefungsService(
                scopeFactory.Object, Ausloeser, Laufzaehlung.Object, gate,
                TimeProvider.System, NullLogger<ScmStartPruefungsService>.Instance);
        }

        public void Dispose() => Service.Dispose();
    }
}

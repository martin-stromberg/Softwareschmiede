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
        fixture.Trigger.RequestStartCheck();
        fixture.Trigger.RequestStartCheck();

        Assert.Equal(1, fixture.Service.PendingChecks);
    }

    /// <summary>Nur eine sinkende Laufzahl (frei werdender Platz) löst die Prüfung aus;
    /// steigende oder gleiche Werte — etwa ein neuer CLI-Start — lösen nichts aus.</summary>
    [Fact]
    public void Laufzahl_NurRueckgangLoestAus()
    {
        using var fixture = new Fixture();

        fixture.Laufzaehlung.Raise(x => x.RunningCountChanged += null, 1, 2);
        Assert.Equal(0, fixture.Service.PendingChecks);

        fixture.Laufzaehlung.Raise(x => x.RunningCountChanged += null, 2, 2);
        Assert.Equal(0, fixture.Service.PendingChecks);

        fixture.Laufzaehlung.Raise(x => x.RunningCountChanged += null, 2, 1);
        Assert.Equal(1, fixture.Service.PendingChecks);
    }

    /// <summary>Ein Rückgang auf null (letzter laufender Prozess) löst ebenfalls aus.</summary>
    [Fact]
    public void Laufzahl_RueckgangAufNullLoestAus()
    {
        using var fixture = new Fixture();
        fixture.Laufzaehlung.Raise(x => x.RunningCountChanged += null, 1, 0);
        Assert.Equal(1, fixture.Service.PendingChecks);
    }

    /// <summary>Nach Dispose sind die Abonnements getrennt: weitere Signale schlagen weder
    /// fehl noch erhöhen sie den Zähler.</summary>
    [Fact]
    public void Dispose_TrenntAbonnements()
    {
        var fixture = new Fixture();
        fixture.Service.Dispose();

        fixture.Trigger.RequestStartCheck();
        fixture.Laufzaehlung.Raise(x => x.RunningCountChanged += null, 2, 1);
        Assert.Equal(0, fixture.Service.PendingChecks);
    }

    private sealed class Fixture : IDisposable
    {
        public ScmStartCheckTrigger Trigger { get; } = new();
        public Mock<IRunningAutomationStatusSource> Laufzaehlung { get; } = new();
        public ScmStartCheckService Service { get; }

        public Fixture()
        {
            var scopeFactory = new Mock<IServiceScopeFactory>();
            var gate = new ScmEventStartupGate();
            gate.Ready();
            Service = new ScmStartCheckService(
                scopeFactory.Object, Trigger, Laufzaehlung.Object, gate,
                TimeProvider.System, NullLogger<ScmStartCheckService>.Instance);
        }

        public void Dispose() => Service.Dispose();
    }
}

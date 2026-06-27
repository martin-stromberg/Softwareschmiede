using FluentAssertions;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.Tests.Infrastructure.Terminal;

/// <summary>Unit-Tests fuer <see cref="CliRuntimeStatusEvaluator"/>.</summary>
public sealed class CliRuntimeStatusEvaluatorTests
{
    /// <summary>Ein beendeter Prozess ergibt Inaktiv.</summary>
    [Fact]
    public void Determine_ReturnsInaktiv_WhenProcessIsNotRunning()
    {
        var now = DateTimeOffset.UtcNow;

        var status = CliRuntimeStatusEvaluator.Determine(
            false,
            now,
            now,
            null,
            now,
            TimeSpan.FromSeconds(4));

        status.Should().Be(CliRuntimeStatus.Inaktiv);
    }

    /// <summary>Frische Ausgabe ergibt Laeuft.</summary>
    [Fact]
    public void Determine_ReturnsLaeuft_WhenOutputIsRecent()
    {
        var now = DateTimeOffset.UtcNow;

        var status = CliRuntimeStatusEvaluator.Determine(
            true,
            now.AddSeconds(-30),
            now.AddSeconds(-1),
            null,
            now,
            TimeSpan.FromSeconds(4));

        status.Should().Be(CliRuntimeStatus.Laeuft);
    }

    /// <summary>Lange fehlende Aktivitaet ergibt WartetAufEingabe.</summary>
    [Fact]
    public void Determine_ReturnsWartetAufEingabe_WhenActivityIsStale()
    {
        var now = DateTimeOffset.UtcNow;

        var status = CliRuntimeStatusEvaluator.Determine(
            true,
            now.AddSeconds(-30),
            now.AddSeconds(-10),
            null,
            now,
            TimeSpan.FromSeconds(4));

        status.Should().Be(CliRuntimeStatus.WartetAufEingabe);
    }

    /// <summary>Frische Benutzereingabe setzt den Status wieder auf Laeuft.</summary>
    [Fact]
    public void Determine_ReturnsLaeuft_WhenInputIsRecent()
    {
        var now = DateTimeOffset.UtcNow;

        var status = CliRuntimeStatusEvaluator.Determine(
            true,
            now.AddSeconds(-30),
            now.AddSeconds(-10),
            now.AddSeconds(-1),
            now,
            TimeSpan.FromSeconds(4));

        status.Should().Be(CliRuntimeStatus.Laeuft);
    }
}

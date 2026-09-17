using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Softwareschmiede.Application.Services.Updates;

namespace Softwareschmiede.App.Services.Testing;

/// <summary>
/// E2E-Testadapter für <see cref="ICliUpdateSafetyService"/>: liefert nur bei per Szenario
/// aktivierter Steuerung (<c>cliSafety.enabled</c>) kontrolliert riskante Aufgaben, damit der
/// echte Ja/Nein-Sicherheitsdialog der Anwendung ausgelöst wird. Ohne Aktivierung wird ein
/// risikofreies Ergebnis gemeldet.
/// </summary>
public sealed class FixtureCliUpdateSafetyService : ICliUpdateSafetyService
{
    private readonly UpdateE2ETestKontext _kontext;

    /// <inheritdoc cref="FixtureCliUpdateSafetyService"/>
    /// <param name="kontext">Der Laufzeitkontext der Update-E2E-Umgebung.</param>
    public FixtureCliUpdateSafetyService(UpdateE2ETestKontext kontext)
    {
        _kontext = kontext;
    }

    /// <inheritdoc/>
    public Task<CliUpdateSafetyResult> CheckAsync(CancellationToken ct = default)
    {
        var sicherheit = _kontext.LeseSzenario()?.CliSicherheit;
        var riskanteAufgaben = sicherheit is { Aktiv: true }
            ? (IReadOnlyList<string>)sicherheit.RiskanteAufgaben
            : [];

        _kontext.Protokoll.Schreibe(
            UpdateE2EEreignisse.CliSafetyChecked,
            new { riskyTaskCount = riskanteAufgaben.Count });

        return Task.FromResult(new CliUpdateSafetyResult(riskanteAufgaben.Count, riskanteAufgaben));
    }
}

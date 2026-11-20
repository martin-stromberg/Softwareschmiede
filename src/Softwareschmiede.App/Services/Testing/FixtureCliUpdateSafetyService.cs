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
    private readonly UpdateE2ETestContext _context;

    /// <inheritdoc cref="FixtureCliUpdateSafetyService"/>
    /// <param name="context">Der Laufzeitkontext der Update-E2E-Umgebung.</param>
    public FixtureCliUpdateSafetyService(UpdateE2ETestContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public Task<CliUpdateSafetyResult> CheckAsync(CancellationToken ct = default)
    {
        var safe = _context.ReadScenario()?.CliSafe;
        var riskyWorkItems = safe is { Active: true }
            ? (IReadOnlyList<string>)safe.RiskyWorkItems
            : [];

        _context.Log.Write(
            UpdateE2EEvents.CliSafetyChecked,
            new { riskyTaskCount = riskyWorkItems.Count });

        return Task.FromResult(new CliUpdateSafetyResult(riskyWorkItems.Count, riskyWorkItems));
    }
}

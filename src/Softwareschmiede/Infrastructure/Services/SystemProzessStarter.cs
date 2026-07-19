using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Infrastructure.Services;

/// <summary>Reale Implementierung von <see cref="IProzessStarter"/> via <see cref="Process.Start(ProcessStartInfo)"/>.</summary>
public sealed class SystemProzessStarter(ILogger<SystemProzessStarter> logger) : IProzessStarter
{
    /// <inheritdoc/>
    public void Starten(ProzessStartAnfrage anfrage)
    {
        ArgumentNullException.ThrowIfNull(anfrage);

        logger.LogInformation(
            "Prozess wird gestartet: {DateiName} {Argumente} (ShellAusfuehren={ShellAusfuehren})",
            anfrage.DateiName,
            anfrage.Argumente,
            anfrage.ShellAusfuehren);

        var processStartInfo = new ProcessStartInfo
        {
            FileName = anfrage.DateiName,
            Arguments = anfrage.Argumente ?? string.Empty,
            UseShellExecute = anfrage.ShellAusfuehren,
        };

        using var process = Process.Start(processStartInfo);
    }
}

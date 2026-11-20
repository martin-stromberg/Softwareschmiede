using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Infrastructure.Services;

/// <summary>Reale Implementierung von <see cref="IProcessStarter"/> via <see cref="Process.Start(ProcessStartInfo)"/>.</summary>
public sealed class SystemProzessStarter(ILogger<SystemProzessStarter> logger) : IProcessStarter
{
    /// <inheritdoc/>
    public void Start(ProcessStartRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        logger.LogInformation(
            "Prozess wird gestartet: {DateiName} {Argumente} (ShellAusfuehren={ShellAusfuehren})",
            request.FileName,
            request.Arguments,
            request.UseShellExecute);

        var processStartInfo = new ProcessStartInfo
        {
            FileName = request.FileName,
            Arguments = request.Arguments ?? string.Empty,
            UseShellExecute = request.UseShellExecute,
        };

        using var process = Process.Start(processStartInfo);
    }
}

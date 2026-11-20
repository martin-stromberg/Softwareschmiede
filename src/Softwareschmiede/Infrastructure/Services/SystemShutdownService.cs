using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.Infrastructure.Services;

/// <summary>
/// Führt plattformabhängig den OS-Shutdown-Befehl aus.
/// </summary>
public sealed class SystemShutdownService(ILogger<SystemShutdownService> logger) : ISystemShutdownService
{
    /// <inheritdoc />
    public async Task RequestShutdownAsync(bool force, CancellationToken cancellationToken = default)
    {
        var (fileName, arguments) = ResolveShutdownCommand(force);

        logger.LogWarning(
            "OS-Shutdown wird angefordert (Erzwingen={Erzwingen}) über Kommando: {FileName} {Arguments}",
            force,
            fileName,
            arguments);

        var processStartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            CreateNoWindow = true,
            UseShellExecute = false
        };

        using var process = Process.Start(processStartInfo)
            ?? throw new InvalidOperationException("Shutdown-Prozess konnte nicht gestartet werden.");

        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Shutdown-Prozess endete mit ExitCode {process.ExitCode}.");
        }
    }

    /// <summary>
    /// Löst das plattformspezifische Shutdown-Kommando auf. Das Force-Flag wirkt ausschließlich
    /// unter Windows (<c>/f</c>); unter Linux/macOS bleiben die Kommandos unverändert.
    /// </summary>
    internal static (string FileName, string Arguments) ResolveShutdownCommand(bool force)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ("shutdown", force ? "/s /f /t 0" : "/s /t 0");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return ("shutdown", "-h now");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return ("osascript", "-e \"tell app \\\"System Events\\\" to shut down\"");
        }

        throw new PlatformNotSupportedException("Auto-Shutdown wird auf diesem Betriebssystem nicht unterstützt.");
    }
}

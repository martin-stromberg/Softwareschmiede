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
    public async Task RequestShutdownAsync(CancellationToken cancellationToken = default)
    {
        var (fileName, arguments) = ResolveShutdownCommand();

        logger.LogWarning(
            "OS-Shutdown wird angefordert über Kommando: {FileName} {Arguments}",
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

    private static (string FileName, string Arguments) ResolveShutdownCommand()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ("shutdown", "/s /f /t 0");
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

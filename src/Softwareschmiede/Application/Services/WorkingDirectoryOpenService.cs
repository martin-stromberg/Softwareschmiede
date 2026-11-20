using System.Runtime.InteropServices;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Application.Services;

/// <summary>Löst den plattformabhängigen Öffnen-Befehl auf und startet den OS-Dateiexplorer für ein Verzeichnis.</summary>
/// <param name="processStarter">Startet den plattformabhängigen Öffnen-Befehl.</param>
public sealed class WorkingDirectoryOpenService(IProcessStarter processStarter)
{
    /// <summary>Öffnet das übergebene Arbeitsverzeichnis im Standard-Dateiexplorer des Betriebssystems.</summary>
    /// <param name="workingDirectory">Der zu öffnende Verzeichnispfad.</param>
    public void Open(string workingDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        var (fileName, argumente) = ResolveOpenCommand(workingDirectory);

        processStarter.Start(new ProcessStartRequest(fileName, argumente, UseShellExecute: false));
    }

    private static (string FileName, string Arguments) ResolveOpenCommand(string workingDirectory)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("Das Öffnen des Arbeitsverzeichnisses wird auf diesem Betriebssystem nicht unterstützt.");
        }

        var quotedWorkingDirectory = $"\"{workingDirectory}\"";
        return ("explorer.exe", quotedWorkingDirectory);
    }
}

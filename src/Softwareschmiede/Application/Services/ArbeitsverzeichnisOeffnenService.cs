using System.Runtime.InteropServices;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Application.Services;

/// <summary>Löst den plattformabhängigen Öffnen-Befehl auf und startet den OS-Dateiexplorer für ein Verzeichnis.</summary>
/// <param name="processStarter">Startet den plattformabhängigen Öffnen-Befehl.</param>
public sealed class ArbeitsverzeichnisOeffnenService(IProcessStarter processStarter)
{
    /// <summary>Öffnet das übergebene Arbeitsverzeichnis im Standard-Dateiexplorer des Betriebssystems.</summary>
    /// <param name="arbeitsverzeichnis">Der zu öffnende Verzeichnispfad.</param>
    public void Oeffne(string arbeitsverzeichnis)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(arbeitsverzeichnis);

        var (dateiName, argumente) = ResolveOeffnenBefehl(arbeitsverzeichnis);

        processStarter.Start(new ProcessStartRequest(dateiName, argumente, UseShellExecute: false));
    }

    private static (string FileName, string Arguments) ResolveOeffnenBefehl(string arbeitsverzeichnis)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("Das Öffnen des Arbeitsverzeichnisses wird auf diesem Betriebssystem nicht unterstützt.");
        }

        var quotedArbeitsverzeichnis = $"\"{arbeitsverzeichnis}\"";
        return ("explorer.exe", quotedArbeitsverzeichnis);
    }
}

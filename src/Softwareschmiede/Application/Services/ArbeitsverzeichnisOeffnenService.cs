using System.Runtime.InteropServices;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Application.Services;

/// <summary>Löst den plattformabhängigen Öffnen-Befehl auf und startet den OS-Dateiexplorer für ein Verzeichnis.</summary>
/// <param name="prozessStarter">Startet den plattformabhängigen Öffnen-Befehl.</param>
public sealed class ArbeitsverzeichnisOeffnenService(IProzessStarter prozessStarter)
{
    /// <summary>Öffnet das übergebene Arbeitsverzeichnis im Standard-Dateiexplorer des Betriebssystems.</summary>
    /// <param name="arbeitsverzeichnis">Der zu öffnende Verzeichnispfad.</param>
    public void Oeffne(string arbeitsverzeichnis)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(arbeitsverzeichnis);

        var (dateiName, argumente) = ResolveOeffnenBefehl(arbeitsverzeichnis);

        prozessStarter.Starten(new ProzessStartAnfrage(dateiName, argumente, ShellAusfuehren: false));
    }

    private static (string DateiName, string Argumente) ResolveOeffnenBefehl(string arbeitsverzeichnis)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("Das Öffnen des Arbeitsverzeichnisses wird auf diesem Betriebssystem nicht unterstützt.");
        }

        var quotedArbeitsverzeichnis = $"\"{arbeitsverzeichnis}\"";
        return ("explorer.exe", quotedArbeitsverzeichnis);
    }
}

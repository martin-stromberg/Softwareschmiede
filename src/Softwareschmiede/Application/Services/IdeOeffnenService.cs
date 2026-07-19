using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Application.Services;

/// <summary>Findet die <c>*.sln</c>-Dateien eines Arbeitsverzeichnisses und öffnet eine übergebene Solution.</summary>
/// <param name="prozessStarter">Startet den Öffnen-Befehl für die Solution-Datei.</param>
public sealed class IdeOeffnenService(IProzessStarter prozessStarter)
{
    /// <summary>Ermittelt alle <c>*.sln</c>-Dateien auf oberster Ebene des Arbeitsverzeichnisses, alphabetisch sortiert.</summary>
    /// <param name="arbeitsverzeichnis">Der zu durchsuchende Verzeichnispfad, oder <c>null</c>/leer.</param>
    /// <returns>Die gefundenen Solution-Pfade, alphabetisch sortiert; leere Liste bei fehlendem/leerem Pfad oder nicht existierendem Verzeichnis.</returns>
    public IReadOnlyList<string> FindeSolutions(string? arbeitsverzeichnis)
    {
        if (string.IsNullOrWhiteSpace(arbeitsverzeichnis) || !Directory.Exists(arbeitsverzeichnis))
            return [];

        return Directory.EnumerateFiles(arbeitsverzeichnis, "*.sln", SearchOption.TopDirectoryOnly)
            .OrderBy(pfad => pfad, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Öffnet die übergebene Solution-Datei mit dem beim Betriebssystem registrierten Standardhandler.</summary>
    /// <param name="solutionPfad">Der Pfad der zu öffnenden <c>*.sln</c>-Datei.</param>
    public void OeffneSolution(string solutionPfad)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionPfad);

        prozessStarter.Starten(new ProzessStartAnfrage(solutionPfad, Argumente: null, ShellAusfuehren: true));
    }
}

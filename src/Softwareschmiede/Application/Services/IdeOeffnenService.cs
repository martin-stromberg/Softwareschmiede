using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Application.Services;

/// <summary>Findet die <c>*.sln</c>-Dateien eines Arbeitsverzeichnisses und öffnet eine übergebene Solution.</summary>
/// <param name="prozessStarter">Startet den Öffnen-Befehl für die Solution-Datei.</param>
/// <param name="visualStudioCodeLocator">Ermittelt einen startbaren Visual-Studio-Code-Befehl.</param>
public sealed class IdeOeffnenService(
    IProzessStarter prozessStarter,
    IVisualStudioCodeLocator visualStudioCodeLocator)
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

    /// <summary>Gibt an, ob Visual Studio Code aktuell auflösbar ist.</summary>
    public bool IstVisualStudioCodeVerfuegbar() => visualStudioCodeLocator.Locate().IsAvailable;

    /// <summary>Öffnet das Arbeitsverzeichnis in Visual Studio Code.</summary>
    /// <param name="arbeitsverzeichnis">Der zu öffnende Ordner.</param>
    public void OeffneVisualStudioCode(string arbeitsverzeichnis)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(arbeitsverzeichnis);

        if (!Directory.Exists(arbeitsverzeichnis))
            throw new DirectoryNotFoundException($"Das Arbeitsverzeichnis wurde nicht gefunden: {arbeitsverzeichnis}");

        var availability = visualStudioCodeLocator.Locate();
        if (!availability.IsAvailable || string.IsNullOrWhiteSpace(availability.ExecutablePath))
            throw new InvalidOperationException("Visual Studio Code wurde nicht gefunden.");

        prozessStarter.Starten(new ProzessStartAnfrage(
            availability.ExecutablePath,
            QuoteArgument(arbeitsverzeichnis),
            ShellAusfuehren: false));
    }

    private static string QuoteArgument(string argument)
        => $"\"{argument.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
}

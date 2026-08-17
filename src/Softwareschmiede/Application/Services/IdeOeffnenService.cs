using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.PluginImpl;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Application.Services;

/// <summary>Findet die <c>*.sln</c>-Dateien eines Arbeitsverzeichnisses und öffnet eine übergebene Solution.</summary>
/// <param name="prozessStarter">Startet den Öffnen-Befehl für die Solution-Datei.</param>
/// <param name="pluginSelectionService">Löst das für ein Repository zuständige IDE-Plugin auf.</param>
public sealed class IdeOeffnenService(
    IProzessStarter prozessStarter,
    PluginSelectionService? pluginSelectionService = null)
{
    /// <summary>Ermittelt alle <c>*.sln</c>-Dateien auf oberster Ebene des Arbeitsverzeichnisses, alphabetisch sortiert.</summary>
    /// <param name="arbeitsverzeichnis">Der zu durchsuchende Verzeichnispfad, oder <c>null</c>/leer.</param>
    /// <returns>Die gefundenen Solution-Pfade, alphabetisch sortiert; leere Liste bei fehlendem/leerem Pfad oder nicht existierendem Verzeichnis.</returns>
    public IReadOnlyList<string> FindeSolutions(string? arbeitsverzeichnis)
    {
        if (string.IsNullOrWhiteSpace(arbeitsverzeichnis))
            return [];

        return VisualStudioIdePlugin.FindSolutionFiles(arbeitsverzeichnis);
    }

    /// <summary>Öffnet die übergebene Solution-Datei mit dem beim Betriebssystem registrierten Standardhandler.</summary>
    /// <param name="solutionPfad">Der Pfad der zu öffnenden <c>*.sln</c>-Datei.</param>
    public void OeffneSolution(string solutionPfad)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionPfad);

        VisualStudioIdePlugin.OpenSolutionFile(prozessStarter, solutionPfad);
    }

    /// <summary>
    /// Löst das für <paramref name="repositoryPath"/> zuständige IDE-Plugin über den
    /// <see cref="PluginSelectionService"/> auf und ermittelt dessen verfügbare Einstiegspunkte über
    /// <see cref="IIdePlugin.FindEntryPointsAsync"/>. Existiert genau ein Einstiegspunkt, wird dieser
    /// direkt geöffnet. Existieren mehrere Einstiegspunkte und ist <paramref name="waehleEntryPointAsync"/>
    /// gesetzt, wird der Callback aufgerufen, um den zu öffnenden Einstiegspunkt auszuwählen (UX-Erhalt
    /// für Mehr-Einstiegspunkt-Repos); liefert der Callback <c>null</c>, wird nichts geöffnet. Existieren
    /// mehrere Einstiegspunkte ohne Callback, wird der erste geöffnet.
    /// </summary>
    /// <param name="repositoryPath">Pfad des zu öffnenden Repositories.</param>
    /// <param name="waehleEntryPointAsync">
    /// Optionaler Callback zur Auswahl eines Einstiegspunkts bei mehreren Treffern; erhält die
    /// gefundenen Einstiegspunkte und liefert den gewählten Einstiegspunkt, oder <c>null</c> bei Abbruch
    /// durch den Anwender (in diesem Fall wird nichts geöffnet).
    /// </param>
    /// <param name="ct">Cancellation Token.</param>
    public async Task OpenRepositoryInIdeAsync(
        string repositoryPath,
        Func<IReadOnlyList<IdeEntryPoint>, CancellationToken, Task<IdeEntryPoint?>>? waehleEntryPointAsync = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);

        if (pluginSelectionService is null)
            throw new InvalidOperationException("PluginSelectionService wurde nicht bereitgestellt.");

        var plugin = await pluginSelectionService.ResolveIdePluginAsync(repositoryPath, ct);

        var entryPoints = await plugin.FindEntryPointsAsync(repositoryPath, ct);

        if (entryPoints.Count == 0)
            throw new FileNotFoundException($"Keine Einstiegspunkte im Repository gefunden: {repositoryPath}");

        if (entryPoints.Count == 1)
        {
            await plugin.OpenEntryPointAsync(entryPoints[0], ct);
            return;
        }

        if (waehleEntryPointAsync is not null)
        {
            var gewaehlterEntryPoint = await waehleEntryPointAsync(entryPoints, ct);
            if (gewaehlterEntryPoint is null)
                return;

            await plugin.OpenEntryPointAsync(gewaehlterEntryPoint, ct);
            return;
        }

        await plugin.OpenEntryPointAsync(entryPoints[0], ct);
    }
}

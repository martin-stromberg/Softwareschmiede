using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.PluginImpl;

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
    /// <see cref="PluginSelectionService"/> auf und öffnet das Repository damit. Wird als
    /// <see cref="VisualStudioIdePlugin"/> aufgelöst und existieren im Arbeitsverzeichnis mehrere
    /// Solution-Dateien, wird <paramref name="waehleSolutionAsync"/> aufgerufen, um die konkrete
    /// Solution auszuwählen (UX-Erhalt für Mehr-Solution-Repos); wird kein Callback übergeben oder
    /// existiert nur eine Solution, öffnet das aufgelöste Plugin das Repository direkt.
    /// </summary>
    /// <param name="repositoryPath">Pfad des zu öffnenden Repositories.</param>
    /// <param name="waehleSolutionAsync">
    /// Optionaler Callback zur Auswahl einer Solution-Datei bei mehreren Treffern; erhält die
    /// gefundenen Solution-Pfade und liefert den gewählten Pfad, oder <c>null</c> bei Abbruch durch
    /// den Anwender (in diesem Fall wird nichts geöffnet).
    /// </param>
    /// <param name="ct">Cancellation Token.</param>
    public async Task OpenRepositoryInIdeAsync(
        string repositoryPath,
        Func<IReadOnlyList<string>, CancellationToken, Task<string?>>? waehleSolutionAsync = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);

        if (pluginSelectionService is null)
            throw new InvalidOperationException("PluginSelectionService wurde nicht bereitgestellt.");

        var plugin = await pluginSelectionService.ResolveIdePluginAsync(repositoryPath, ct);

        if (plugin is VisualStudioIdePlugin && waehleSolutionAsync is not null)
        {
            var solutionPfade = FindeSolutions(repositoryPath);
            if (solutionPfade.Count > 1)
            {
                var solutionPfad = await waehleSolutionAsync(solutionPfade, ct);
                if (solutionPfad is null)
                    return;

                OeffneSolution(solutionPfad);
                return;
            }
        }

        await plugin.OpenRepositoryAsync(repositoryPath, ct);
    }
}

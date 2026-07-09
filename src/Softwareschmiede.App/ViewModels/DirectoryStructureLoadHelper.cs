using Microsoft.Extensions.Logging;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.App.ViewModels;

/// <summary>Gemeinsame Lade-Logik für die Arbeitsverzeichnis-Auswahl, genutzt von <see cref="RepositoryAssignViewModel"/> und <see cref="ArbeitsverzeichnisBearbeitenViewModel"/>.</summary>
internal static class DirectoryStructureLoadHelper
{
    /// <summary>Ruft die Verzeichnisstruktur eines Repositories ab und liefert sie inklusive vorangestelltem Root-Eintrag <c>"."</c>. Fehler werden geloggt und führen zu einer Liste, die nur den Root-Eintrag enthält; eine Cancellation wird nicht abgefangen, sondern an den Aufrufer weitergereicht.</summary>
    /// <param name="directoryStructureService">Der Service zum Abruf der Verzeichnisstruktur.</param>
    /// <param name="gitPlugin">Das Git-Plugin des Repositories.</param>
    /// <param name="repositoryUrl">URL bzw. Pfad des Repositories.</param>
    /// <param name="logger">Logger für Fehlermeldungen.</param>
    /// <param name="ct">Cancellation Token.</param>
    /// <returns>Liste der Arbeitsverzeichnisse, beginnend mit <c>"."</c> (Repository-Root).</returns>
    public static async Task<List<string>> LoadWorkingDirectoriesAsync(
        DirectoryStructureBrowserService directoryStructureService,
        IGitPlugin gitPlugin,
        string repositoryUrl,
        ILogger logger,
        CancellationToken ct)
    {
        var result = new List<string> { "." };

        try
        {
            var directories = await directoryStructureService.GetDirectoriesAsync(gitPlugin, repositoryUrl, ct);
            ct.ThrowIfCancellationRequested();
            result.AddRange(directories);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Fehler beim Laden der Verzeichnisstruktur.");
        }

        return result;
    }

    /// <summary>
    /// Kapselt den in beiden ViewModels identischen Wrapper um <see cref="LoadWorkingDirectoriesAsync"/>:
    /// setzt den Lade-Status vor/nach dem Aufruf und schluckt einen erwarteten Abbruch (<paramref name="ct"/>
    /// wurde durch Repository-/Plugin-Wechsel abgebrochen). Die Anwendung des Ergebnisses (Befüllung der
    /// UI-Collection, Vorauswahl) bleibt bewusst beim jeweiligen Aufrufer, da sich dieser Teil zwischen den
    /// beiden ViewModels unterscheidet (unterschiedliches Verhalten bei Abbruch bzw. bei fehlendem Kontext).
    /// </summary>
    /// <param name="directoryStructureService">Der Service zum Abruf der Verzeichnisstruktur.</param>
    /// <param name="gitPlugin">Das Git-Plugin des Repositories.</param>
    /// <param name="repositoryUrl">URL bzw. Pfad des Repositories.</param>
    /// <param name="logger">Logger für Fehlermeldungen.</param>
    /// <param name="setIsLoading">Callback zum Setzen des Lade-Status-Flags des Aufrufers.</param>
    /// <param name="ct">Cancellation Token.</param>
    /// <returns>
    /// Die geladene Verzeichnisliste, oder <c>null</c>, wenn der Ladevorgang über <paramref name="ct"/>
    /// erwartungsgemäß abgebrochen wurde (der Aufrufer entscheidet dann selbst, wie mit diesem Fall
    /// umzugehen ist).
    /// </returns>
    public static async Task<List<string>?> LoadWithLoadingStateAsync(
        DirectoryStructureBrowserService directoryStructureService,
        IGitPlugin gitPlugin,
        string repositoryUrl,
        ILogger logger,
        Action<bool> setIsLoading,
        CancellationToken ct)
    {
        try
        {
            setIsLoading(true);
            return await LoadWorkingDirectoriesAsync(directoryStructureService, gitPlugin, repositoryUrl, logger, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return null;
        }
        finally
        {
            if (!ct.IsCancellationRequested)
                setIsLoading(false);
        }
    }
}

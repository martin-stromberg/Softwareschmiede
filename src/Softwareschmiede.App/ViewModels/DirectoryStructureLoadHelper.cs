using System.IO;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.App.ViewModels;

/// <summary>Gemeinsame Lade-Logik für die Arbeitsverzeichnis-Auswahl, genutzt von <see cref="RepositoryAssignViewModel"/> und <see cref="ArbeitsverzeichnisBearbeitenViewModel"/>.</summary>
internal static class DirectoryStructureLoadHelper
{
    /// <summary>Ruft die Verzeichnisstruktur eines Repositories ab und liefert das Ergebnis für Auswahl- oder manuellen Eingabemodus.</summary>
    /// <param name="directoryStructureService">Der Service zum Abruf der Verzeichnisstruktur.</param>
    /// <param name="gitPlugin">Das Git-Plugin des Repositories.</param>
    /// <param name="repositoryUrl">URL bzw. Pfad des Repositories.</param>
    /// <param name="logger">Logger für Fehlermeldungen.</param>
    /// <param name="ct">Cancellation Token.</param>
    /// <returns>Lade-Ergebnis für die Arbeitsverzeichnis-UI.</returns>
    public static async Task<WorkingDirectoryLoadResult> LoadWorkingDirectoriesAsync(
        DirectoryStructureBrowserService directoryStructureService,
        IGitPlugin gitPlugin,
        string repositoryUrl,
        ILogger logger,
        CancellationToken ct)
    {
        var result = await directoryStructureService.GetDirectoryLoadResultAsync(gitPlugin, repositoryUrl, ct);
        ct.ThrowIfCancellationRequested();
        if (result.Status != RepositoryStructureLoadStatus.Success)
        {
            logger.LogWarning(
                "Verzeichnisstruktur konnte nicht geladen werden ({Status}): {Message}",
                result.Status,
                result.Message);
            return WorkingDirectoryLoadResult.ManualInput(result.Message);
        }

        return WorkingDirectoryLoadResult.Selection([".", ..result.Entries.Select(entry => entry.Path)]);
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
    public static async Task<WorkingDirectoryLoadResult?> LoadWithLoadingStateAsync(
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

/// <summary>App-nahes Lade-Ergebnis für die Arbeitsverzeichnis-Auswahl.</summary>
/// <param name="WorkingDirectories">Arbeitsverzeichnis-Kandidaten für den Auswahlmodus.</param>
/// <param name="RequiresManualInput">Gibt an, ob statt Auswahl eine manuelle Eingabe benötigt wird.</param>
/// <param name="Message">Optionale Fehlermeldung.</param>
internal sealed record WorkingDirectoryLoadResult(
    IReadOnlyList<string> WorkingDirectories,
    bool RequiresManualInput,
    string? Message = null)
{
    /// <summary>Erzeugt ein Auswahl-Ergebnis.</summary>
    public static WorkingDirectoryLoadResult Selection(IEnumerable<string> workingDirectories)
        => new(workingDirectories.ToList(), RequiresManualInput: false);

    /// <summary>Erzeugt ein Ergebnis für manuelle Eingabe.</summary>
    public static WorkingDirectoryLoadResult ManualInput(string? message = null)
        => new([], RequiresManualInput: true, message);
}

/// <summary>Validiert und normalisiert manuell eingegebene Arbeitsverzeichnis-Pfade.</summary>
internal static class WorkingDirectoryInputValidator
{
    /// <summary>Validiert den Eingabetext und liefert den normalisierten relativen Pfad.</summary>
    public static bool TryNormalize(string? input, out string normalized, out string? error)
    {
        normalized = string.IsNullOrWhiteSpace(input) ? "." : input.Trim().Replace('\\', '/');
        error = null;

        if (normalized == ".")
        {
            return true;
        }

        if (Path.IsPathRooted(normalized))
        {
            error = "Das Arbeitsverzeichnis muss ein relativer Pfad sein.";
            return false;
        }

        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Any(part => part == ".."))
        {
            error = "Das Arbeitsverzeichnis darf keine '..'-Segmente enthalten.";
            return false;
        }

        if (normalized.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            error = "Das Arbeitsverzeichnis enthält ungültige Zeichen.";
            return false;
        }

        return true;
    }
}

using Softwareschmiede.Application.Services.Updates;

namespace Softwareschmiede.App.ViewModels;

/// <summary>
/// Anzeige-Texte der Update-Pfade und des daraus abgeleiteten UpdateChannels. Einzige Quelle
/// für die Labels — UI und Tests beziehen die Texte von hier, damit sie nicht an mehreren
/// Stellen gepflegt werden.
/// </summary>
public static class UpdatePathText
{
    /// <summary>Pfad-Label: ausschließlich stabile Produktivversionen.</summary>
    public const string StableVersions = UpdatePathChannelResolver.StableVersionsPath;

    /// <summary>Pfad-Label: ausschließlich Vorabversionen (Release-Kandidaten).</summary>
    public const string Vorabversionen = UpdatePathChannelResolver.PrereleaseVersionsPath;

    /// <summary>Kanal-Anzeigetext für den aus dem Pfad <see cref="StableVersions"/> resultierenden Kanal.</summary>
    public const string ChannelStable = "Produktiv (nur stabile Versionen)";

    /// <summary>Kanal-Anzeigetext für den aus dem Pfad <see cref="Vorabversionen"/> resultierenden Kanal.</summary>
    public const string ChannelReleaseCandidate = "Release Candidate (nur Vorabversionen)";
}

/// <summary>
/// Auswahloption für den Update-Pfad in den Einstellungen: der Steuerwert
/// (<see cref="Path"/>) zusammen mit dem sichtbaren Anzeige-Label.
/// </summary>
/// <param name="Path">Der persistierte Update-Pfad.</param>
/// <param name="Label">Der in der Auswahlbox angezeigte Text.</param>
public sealed record UpdatePathOption(string Path, string Label)
{
    /// <summary>Alle wählbaren Update-Pfade in Anzeigereihenfolge.</summary>
    public static IReadOnlyList<UpdatePathOption> All { get; } =
    [
        new(UpdatePathChannelResolver.StableVersionsPath, UpdatePathText.StableVersions),
        new(UpdatePathChannelResolver.PrereleaseVersionsPath, UpdatePathText.Vorabversionen)
    ];

    /// <summary>Liefert die Auswahloption zu einem gespeicherten Pfad; unbekannte oder leere Werte fallen auf die Produktiv-Option zurück.</summary>
    public static UpdatePathOption ForPath(string? path)
    {
        var normalizes = UpdatePathChannelResolver.NormalizePath(path);
        return All.First(o => o.Path == normalizes);
    }

    /// <inheritdoc/>
    public override string ToString() => Label;
}

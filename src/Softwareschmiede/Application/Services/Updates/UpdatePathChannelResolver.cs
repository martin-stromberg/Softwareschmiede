namespace Softwareschmiede.Application.Services.Updates;

/// <summary>
/// Bildet den konfigurierten Update-Pfad auf den exklusiven Update-Kanal ab und ordnet
/// installierte Versions-Tags demselben Kanalmodell zu. Reine Funktionen ohne Abhängigkeiten;
/// die Prerelease-Klassifikation der Release-Kandidaten bleibt beim GitHub-<c>prerelease</c>-Flag
/// bzw. <see cref="SemanticUpdateVersion.IsPrerelease"/>.
/// </summary>
public static class UpdatePathChannelResolver
{
    /// <summary>Fester Update-Pfad für ausschließlich stabile Produktivversionen.</summary>
    public const string StableVersionsPath = "Produktivversionen";

    /// <summary>Fester Update-Pfad für ausschließlich Vorabversionen (Release-Kandidaten).</summary>
    public const string PrereleaseVersionsPath = "Vorabversionen";

    private const string ReleaseCandidateMarker = "-rc";

    /// <summary>
    /// Leitet den exklusiven Update-Kanal aus dem Update-Pfad ab:
    /// <see cref="PrereleaseVersionsPath"/> → <see cref="UpdateChannel.ReleaseCandidate"/>,
    /// jeder andere Wert → <see cref="UpdateChannel.Stable"/>.
    /// </summary>
    /// <param name="updatePath">Der konfigurierte Update-Pfad.</param>
    /// <returns>Der abgeleitete exklusive Update-Kanal.</returns>
    public static UpdateChannel ResolveChannel(string? updatePath)
        => IsPath(updatePath, PrereleaseVersionsPath)
            ? UpdateChannel.ReleaseCandidate
            : UpdateChannel.Stable;

    /// <summary>
    /// Normalisiert einen gespeicherten Pfadwert auf einen der beiden bekannten Pfade;
    /// fehlende, leere oder unbekannte Werte fallen auf <see cref="StableVersionsPath"/> zurück.
    /// </summary>
    /// <param name="updatePath">Der gespeicherte Pfadwert.</param>
    /// <returns>Der normalisierte Update-Pfad.</returns>
    public static string NormalizePath(string? updatePath)
        => IsPath(updatePath, PrereleaseVersionsPath)
            ? PrereleaseVersionsPath
            : StableVersionsPath;

    /// <summary>
    /// Prüft, ob der Wert einer der beiden bekannten Update-Pfade ist
    /// (Groß-/Kleinschreibung und umgebende Leerzeichen werden toleriert).
    /// </summary>
    /// <param name="updatePath">Der zu prüfende Pfadwert.</param>
    /// <returns><c>true</c>, wenn der Wert ein bekannter Update-Pfad ist.</returns>
    public static bool IsValidPath(string? updatePath)
        => IsPath(updatePath, StableVersionsPath) || IsPath(updatePath, PrereleaseVersionsPath);

    /// <summary>
    /// Ermittelt den Kanal einer installierten Version anhand ihres Release-Tags: ein
    /// „-rc"-Infix (OrdinalIgnoreCase) kennzeichnet <see cref="UpdateChannel.ReleaseCandidate"/>.
    /// Fehlende oder leere Tag-Namen werden konservativ dem RC-Kanal zugeordnet.
    /// Diese Klassifikationsregel ist die einzige Quelle und wird auch von
    /// <c>DatabasePathResolver</c> für die Wahl des Datenbankpfads wiederverwendet.
    /// </summary>
    /// <param name="tagName">Der Release-Tag der installierten <c>version.json</c>.</param>
    /// <returns>Der Kanal der installierten Version.</returns>
    public static UpdateChannel ResolveChannelForTagName(string? tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
            return UpdateChannel.ReleaseCandidate;

        return tagName.Contains(ReleaseCandidateMarker, StringComparison.OrdinalIgnoreCase)
            ? UpdateChannel.ReleaseCandidate
            : UpdateChannel.Stable;
    }

    private static bool IsPath(string? updatePath, string expected)
        => string.Equals(updatePath?.Trim(), expected, StringComparison.OrdinalIgnoreCase);
}

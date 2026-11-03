namespace Softwareschmiede.Application.Services.Updates;

/// <summary>
/// Bildet den konfigurierten Update-Pfad auf den exklusiven Update-Kanal ab und ordnet
/// installierte Versions-Tags demselben Kanalmodell zu. Reine Funktionen ohne Abhängigkeiten;
/// die Prerelease-Klassifikation der Release-Kandidaten bleibt beim GitHub-<c>prerelease</c>-Flag
/// bzw. <see cref="SemanticUpdateVersion.IsPrerelease"/>.
/// </summary>
public static class UpdatePfadKanalResolver
{
    /// <summary>Fester Update-Pfad für ausschließlich stabile Produktivversionen.</summary>
    public const string ProduktivversionenPfad = "Produktivversionen";

    /// <summary>Fester Update-Pfad für ausschließlich Vorabversionen (Release-Kandidaten).</summary>
    public const string VorabversionenPfad = "Vorabversionen";

    private const string ReleaseCandidateMarker = "-rc";

    /// <summary>
    /// Leitet den exklusiven Update-Kanal aus dem Update-Pfad ab:
    /// <see cref="VorabversionenPfad"/> → <see cref="UpdateKanal.ReleaseCandidate"/>,
    /// jeder andere Wert → <see cref="UpdateKanal.Produktiv"/>.
    /// </summary>
    /// <param name="updatePfad">Der konfigurierte Update-Pfad.</param>
    /// <returns>Der abgeleitete exklusive Update-Kanal.</returns>
    public static UpdateKanal ErmittleKanal(string? updatePfad)
        => IstPfad(updatePfad, VorabversionenPfad)
            ? UpdateKanal.ReleaseCandidate
            : UpdateKanal.Produktiv;

    /// <summary>
    /// Normalisiert einen gespeicherten Pfadwert auf einen der beiden bekannten Pfade;
    /// fehlende, leere oder unbekannte Werte fallen auf <see cref="ProduktivversionenPfad"/> zurück.
    /// </summary>
    /// <param name="updatePfad">Der gespeicherte Pfadwert.</param>
    /// <returns>Der normalisierte Update-Pfad.</returns>
    public static string NormalisierePfad(string? updatePfad)
        => IstPfad(updatePfad, VorabversionenPfad)
            ? VorabversionenPfad
            : ProduktivversionenPfad;

    /// <summary>
    /// Prüft, ob der Wert einer der beiden bekannten Update-Pfade ist
    /// (Groß-/Kleinschreibung und umgebende Leerzeichen werden toleriert).
    /// </summary>
    /// <param name="updatePfad">Der zu prüfende Pfadwert.</param>
    /// <returns><c>true</c>, wenn der Wert ein bekannter Update-Pfad ist.</returns>
    public static bool IstGueltigerPfad(string? updatePfad)
        => IstPfad(updatePfad, ProduktivversionenPfad) || IstPfad(updatePfad, VorabversionenPfad);

    /// <summary>
    /// Ermittelt den Kanal einer installierten Version anhand ihres Release-Tags: ein
    /// „-rc"-Infix (OrdinalIgnoreCase) kennzeichnet <see cref="UpdateKanal.ReleaseCandidate"/>.
    /// Fehlende oder leere Tag-Namen werden konservativ dem RC-Kanal zugeordnet
    /// (gleiche Regel wie <c>DatenbankPfadResolver</c>).
    /// </summary>
    /// <param name="tagName">Der Release-Tag der installierten <c>version.json</c>.</param>
    /// <returns>Der Kanal der installierten Version.</returns>
    public static UpdateKanal ErmittleKanalFuerTagName(string? tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
            return UpdateKanal.ReleaseCandidate;

        return tagName.Contains(ReleaseCandidateMarker, StringComparison.OrdinalIgnoreCase)
            ? UpdateKanal.ReleaseCandidate
            : UpdateKanal.Produktiv;
    }

    private static bool IstPfad(string? updatePfad, string erwartet)
        => string.Equals(updatePfad?.Trim(), erwartet, StringComparison.OrdinalIgnoreCase);
}

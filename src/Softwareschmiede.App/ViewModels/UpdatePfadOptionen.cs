using Softwareschmiede.Application.Services.Updates;

namespace Softwareschmiede.App.ViewModels;

/// <summary>
/// Anzeige-Texte der Update-Pfade und des daraus abgeleiteten Update-Kanals. Einzige Quelle
/// für die Labels — UI und Tests beziehen die Texte von hier, damit sie nicht an mehreren
/// Stellen gepflegt werden.
/// </summary>
public static class UpdatePfadTexte
{
    /// <summary>Pfad-Label: ausschließlich stabile Produktivversionen.</summary>
    public const string Produktivversionen = UpdatePfadKanalResolver.ProduktivversionenPfad;

    /// <summary>Pfad-Label: ausschließlich Vorabversionen (Release-Kandidaten).</summary>
    public const string Vorabversionen = UpdatePfadKanalResolver.VorabversionenPfad;

    /// <summary>Kanal-Anzeigetext für den aus dem Pfad <see cref="Produktivversionen"/> resultierenden Kanal.</summary>
    public const string KanalProduktiv = "Produktiv (nur stabile Versionen)";

    /// <summary>Kanal-Anzeigetext für den aus dem Pfad <see cref="Vorabversionen"/> resultierenden Kanal.</summary>
    public const string KanalReleaseCandidate = "Release Candidate (nur Vorabversionen)";
}

/// <summary>
/// Auswahloption für den Update-Pfad in den Einstellungen: der Steuerwert
/// (<see cref="Pfad"/>) zusammen mit dem sichtbaren Anzeige-Label.
/// </summary>
/// <param name="Pfad">Der persistierte Update-Pfad.</param>
/// <param name="Label">Der in der Auswahlbox angezeigte Text.</param>
public sealed record UpdatePfadOption(string Pfad, string Label)
{
    /// <summary>Alle wählbaren Update-Pfade in Anzeigereihenfolge.</summary>
    public static IReadOnlyList<UpdatePfadOption> Alle { get; } =
    [
        new(UpdatePfadKanalResolver.ProduktivversionenPfad, UpdatePfadTexte.Produktivversionen),
        new(UpdatePfadKanalResolver.VorabversionenPfad, UpdatePfadTexte.Vorabversionen)
    ];

    /// <summary>Liefert die Auswahloption zu einem gespeicherten Pfad; unbekannte oder leere Werte fallen auf die Produktiv-Option zurück.</summary>
    public static UpdatePfadOption FuerPfad(string? pfad)
    {
        var normalisiert = UpdatePfadKanalResolver.NormalisierePfad(pfad);
        return Alle.First(o => o.Pfad == normalisiert);
    }

    /// <inheritdoc/>
    public override string ToString() => Label;
}

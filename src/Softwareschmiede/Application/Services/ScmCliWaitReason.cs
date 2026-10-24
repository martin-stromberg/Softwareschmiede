namespace Softwareschmiede.Application.Services;

/// <summary>Einzige Quelle der Warte- und Kanalzugriffs-Grundtexte der CLI-Erkennung. Gründe an
/// zwei Stellen (Orchestrator-Wartegrund, Adapter-Zugriffsgrund) und die Button-Kopplung über
/// <see cref="IsConfirmable"/> teilen sich dieselbe Textquelle, damit sie nicht auseinanderlaufen.</summary>
public static class ScmCliWaitReason
{
    /// <summary>Wartegrund bei unterstütztem Format, aber nicht nachweisbarer Eingabebereitschaft.</summary>
    public const string UnknownReady = "Eingabebereitschaft unbekannt. Für die angezeigte CLI-Sitzung bestätigen.";
    /// <summary>Suffix, das einen vom Anwender ausdrücklich bestätigbaren Grund markiert.</summary>
    public const string ConfirmableMarker = "Für die angezeigte CLI-Sitzung bestätigen.";

    /// <summary>Nur Gründe mit dem Bestätigungs-Suffix bieten die manuelle Bereitschaftsbestätigung an.</summary>
    public static bool IsConfirmable(string? reason)
        => reason?.EndsWith(ConfirmableMarker, StringComparison.Ordinal) == true;

    /// <summary>Beschreibt den Erkennungszustand als ursachenehrlichen Grundtext: CLI-Name und
    /// erkannte Version werden benannt, unbekannte Formate und fehlende Banner ehrlich gemeldet.</summary>
    public static string Describe(ScmCliRecognition recognition) => recognition.State switch
    {
        ScmCliRecognitionState.Unsupported when recognition.Version is not null
            => $"{recognition.CliName} v{recognition.Version} wird nicht unterstützt; unterstützt ist {recognition.SupportedVersionsText}. {ConfirmableMarker}",
        ScmCliRecognitionState.Unsupported
            => $"{recognition.CliName}-Version konnte nicht ermittelt werden; unterstützt ist {recognition.SupportedVersionsText}. {ConfirmableMarker}",
        ScmCliRecognitionState.Unknown when recognition.ChannelReason is { } channelReason
            => $"{recognition.CliName}: {channelReason} CLI regulär neu starten oder erneut prüfen.",
        ScmCliRecognitionState.Unknown when recognition.CliName is not null
            => $"{recognition.CliName}: in der Startausgabe wurde kein unterstütztes Versionsbanner erkannt; die CLI initialisiert möglicherweise noch oder nutzt ein unbekanntes Ausgabeformat. CLI regulär neu starten oder erneut prüfen.",
        ScmCliRecognitionState.Unknown
            => "Das Promptformat der gestarteten CLI ist unbekannt; Eingabebereitschaft und Antwortkanal können nicht geprüft werden.",
        _ => UnknownReady,
    };
}

namespace Softwareschmiede.Domain.Entities;

/// <summary>
/// Verbindliche Statuswerte und Freigabe-Einteilung von <see cref="ScmIncomingEvent.Status"/>:
/// alle produktiven Vergleiche und Zuweisungen des Status verwenden diese Konstanten;
/// <c>ScmIncomingEventStatusTests</c> hält die persistierten Literalwerte fest.
/// Die Prüfrichtung ist bewusst negativ: Nur die ausdrücklich freigegebenen Werte in
/// <see cref="NonBlocking"/> — die finalen Stati sowie die terminalen, ausschließlich manuell
/// klärbaren Klärungsstati — geben die Abschlussprüfung einer Aufgabe und die Zustellung
/// nachfolgender Ereignisse frei; jeder andere Wert, einschließlich unbekannter oder
/// zukünftiger Stati, blockiert konservativ weiter.
/// </summary>
public static class ScmIncomingEventStatus
{
    /// <summary>Neu registriert, noch nicht zugestellt (Default).</summary>
    public const string Aufgenommen = "Aufgenommen";

    /// <summary>Zustellung ruht persistiert bis <see cref="ScmIncomingEvent.NextAttemptUtc"/> (Verzögerungsdirektive).</summary>
    public const string Verzögert = "Verzögert";

    /// <summary>Wartet auf eine Bedingung oder Korrektur; <see cref="ScmIncomingEvent.Reason"/> erklärt den Grund.</summary>
    public const string Wartend = "Wartend";

    /// <summary>Versuchskanal angelegt, Prompt noch nicht geschrieben.</summary>
    public const string ZustellungVorbereitet = "ZustellungVorbereitet";

    /// <summary>Prompt erfolgreich in die CLI geschrieben; Annahme steht aus.</summary>
    public const string Zugestellt = "Zugestellt";

    /// <summary>Schreibversuch begonnen, Erfolg nicht bestätigt; kein automatischer Neuversand.</summary>
    public const string ZustellungUngewiss = "ZustellungUngewiss";

    /// <summary>Korrelierte Annahme eingetroffen; Abschluss steht aus.</summary>
    public const string InBearbeitung = "InBearbeitung";

    /// <summary>Terminaler Diagnose-Status ohne automatischen Neuversand; Klärung nur manuell.</summary>
    public const string VerarbeitungUngeklärt = "VerarbeitungUngeklärt";

    /// <summary>Final: korrelierte Annahme und bestätigter Abschluss.</summary>
    public const string Verarbeitet = "Verarbeitet";

    /// <summary>Final: vom Anwender abgebrochen; keine weiteren Versuche.</summary>
    public const string Abgebrochen = "Abgebrochen";

    /// <summary>
    /// Freigabeliste nicht blockierender Stati: die finalen Stati <see cref="Verarbeitet"/> und
    /// <see cref="Abgebrochen"/> sowie die terminalen, ausschließlich manuell klärbaren
    /// Klärungsstati <see cref="VerarbeitungUngeklärt"/> und <see cref="ZustellungUngewiss"/>.
    /// Ein Ereignis in einem dieser Stati blockiert weder die Abschlussprüfung seiner Aufgabe
    /// (<c>ScmTaskCompletionMonitoringService</c>) noch die Zustellung nachfolgender Ereignisse
    /// derselben Aufgabe (Vorgängerprüfung im <c>ScmEventOrchestrator</c>). Alle übrigen Werte —
    /// die transienten Stati sowie jeder unbekannte oder zukünftige Wert — blockieren.
    /// Bewusste Ausnahme der Transient-Faustregel: <see cref="Wartend"/> mit
    /// <see cref="ScmIncomingEvent.NextAttemptUtc"/> = <see cref="long.MaxValue"/>
    /// (Provider-Authentifizierungs-/Berechtigungs- und Konfigurations-/Startfehler) bewegt
    /// sich ohne Benutzereingriff ebenfalls nicht weiter, bleibt aber blockierend.
    /// </summary>
    public static IReadOnlyCollection<string> NonBlocking { get; } =
        [Verarbeitet, Abgebrochen, VerarbeitungUngeklärt, ZustellungUngewiss];
}

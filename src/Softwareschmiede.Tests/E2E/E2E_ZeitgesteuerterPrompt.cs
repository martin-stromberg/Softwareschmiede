using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Test: Zeitgesteuerter Prompt-Versand über das CLI-Ribbon der Aufgabendetailansicht.
///
/// Voraussetzungen:
/// - Windows-Desktop-Session (kein Headless-CI), Windows 10 Build 17763 oder neuer
/// - Softwareschmiede.App muss im Debug-Modus gebaut sein
///
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
[Trait("Category", "E2E")]
[OsInterface]
[Collection("E2E")]
public sealed class E2E_ZeitgesteuerterPrompt : WpfTestBase
{
    /// <summary>
    /// Szenario: Nutzer startet die CLI, trägt eine in der Zukunft liegende Zielzeit ein, wählt eine
    /// Promptvorlage und klickt "Zeitgesteuert senden". Die Statusanzeige "Prompt in Wartestellung"
    /// muss erscheinen, ohne dass ein Fehlerbanner sichtbar wird.
    /// </summary>
    [SkippableFact]
    public void ZeitgesteuerterPrompt_NachPlanen_ZeigtWartestellungStatus_E2E()
    {
        SkipWennConPtyNichtVerfuegbar();
        ConfirmLocalDirectoryGitInitInSourceDirectory();

        var mainWindow = SetupProjectMitNeuerAufgabe("ZeitgesteuertPrompt-Repo", "ZeitgesteuertPrompt-Projekt");
        StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator");

        WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);

        // Die App berechnet die Zielzeit stets aus dem heutigen Datum. Läge "jetzt + 5 Minuten" bereits am
        // nächsten Tag (kurz vor Mitternacht), wäre die daraus resultierende heutige Uhrzeit bereits vergangen,
        // wodurch die App sofort statt zeitgesteuert versendet und die erwartete Wartestellung-Anzeige ausbleibt.
        // Da dieser E2E-Test die reale Systemuhr nutzt (kein injizierbarer TimeProvider in der App-UI-Schicht),
        // wird die Zielzeit in diesem Fall auf den letztmöglichen Zeitpunkt des heutigen Tages begrenzt.
        // In der letzten Tagesminute (23:59:00-23:59:59) läge auch diese Begrenzung bereits in der
        // Vergangenheit, weshalb der Test in diesem terminalen 1-Minuten-Fenster übersprungen wird,
        // statt eine unerreichbare Zielzeit zu wählen.
        var jetzt = DateTime.Now;
        Skip.If(jetzt.Hour == 23 && jetzt.Minute >= 59, "Letzte Tagesminute: Mitternachts-Guard würde eine bereits vergangene Zielzeit liefern.");

        var zielzeitKandidat = jetzt.AddMinutes(5);
        var zielzeit = zielzeitKandidat.Date == jetzt.Date
            ? zielzeitKandidat
            : new DateTime(jetzt.Year, jetzt.Month, jetzt.Day, 23, 59, 0);

        var stundeBox = WaitForElement(mainWindow, cf => cf.ByName("ScheduledPromptStunde"), Short);
        stundeBox.Click();
        Keyboard.Type(zielzeit.Hour.ToString("00"));

        var minuteBox = WaitForElement(mainWindow, cf => cf.ByName("ScheduledPromptMinute"), Short);
        minuteBox.Click();
        Keyboard.Type(zielzeit.Minute.ToString("00"));

        var promptVorlagenBox = WaitForElement(mainWindow, cf => cf.ByName("PromptVorlagenAuswahl"), Short);
        SelectComboBoxItemByClick(promptVorlagenBox, "Weitermachen", Short);

        var sendenButton = WaitForElement(mainWindow, cf => cf.ByName("ZeitgesteuertSenden"), Short);
        sendenButton.AsButton().Click();

        // Erscheint erst, sobald ScheduledPromptStatus gesetzt ist (NullOrEmptyToVisibilityConverter).
        var statusElement = WaitForElement(mainWindow, cf => cf.ByName("ScheduledPromptStatus"), Medium);
        Assert.NotNull(statusElement);

        var fehlerBanner = mainWindow.FindFirstDescendant(cf => cf.ByName("FehlerMeldung"));
        Assert.Null(fehlerBanner);
    }
}

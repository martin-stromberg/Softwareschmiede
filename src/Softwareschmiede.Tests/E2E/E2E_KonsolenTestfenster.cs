using System.Text;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.WindowsAPI;
using Softwareschmiede.Infrastructure.Terminal;
using Softwareschmiede.Tests.E2E.Views;
using Softwareschmiede.Tests.E2E.Views.Dialogs;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Abdeckung des Konsolentestfensters (CLI-Replay-Diagnose): Öffnen über die Einstellungen,
/// Laden von synthetisch erzeugten .clireplay-Dateien über den nativen Öffnen-Dialog, Zeitraffer-,
/// Pause-/Fortsetzen-, Schrittmodus- (vorwärts/rückwärts) und Neustart-Steuerung sowie Nachweis
/// der Quell-Chunk-Liste (ohne
/// ConPTY-Abhängigkeit — die Aufzeichnungen werden direkt über <see cref="CliReplayAufzeichnungStore"/>
/// erzeugt).
/// </summary>
public partial class End2EndTest
{
    /// <summary>Pflichtabnahme E-02 (Mauspfad): Eine mehrzeilige Auswahl wird in einer frischen
    /// Replay-Ansicht per Drag angelegt und mit Strg+Umschalt+C kopiert.</summary>
    private Task ReplayText_MarkAndCopyViaMouse(Window mainWindow)
        => ReplayText_MitFrischerAufzeichnung(mainWindow, "E2E-Textauswahl-Maus", dialog =>
        {
            // UIA kann den Custom-Renderer nicht lesen. Daher wird das gleiche Zellrechteck
            // vor und nach dem echten Mausdrag aufgenommen; genügend abweichende Pixel belegen
            // das vom TerminalControl gezeichnete Auswahl-Overlay statt nur dessen Zustand.
            var vorMausauswahl = dialog.ErfasseReplayZellPixel(0, 0, 1, "zweite Zeile".Length - 1, 80, 24);
            dialog.MarkiereReplayZellen(0, 0, 1, "zweite Zeile".Length - 1, 80, 24);
            var nachMausauswahl = dialog.ErfasseReplayZellPixel(0, 0, 1, "zweite Zeile".Length - 1, 80, 24);
            Assert.True(KonsolenTestDialogView.ZaehleAbweichendeReplayPixel(vorMausauswahl, nachMausauswahl) > 100,
                "der Mausdrag muss im tatsächlichen Replay-Terminal eine deutlich sichtbare Auswahl-Hervorhebung zeichnen");
            dialog.KopiereReplayAuswahl();
            Assert.Equal($"erste Zeile{Environment.NewLine}zweite Zeile", GetClipboardText());
        });

    /// <summary>E-02-Ergänzung: Der echte Replay-Dialog nimmt den Tastaturfokus an und
    /// kopiert eine bereits sichtbare Auswahl über Strg+Umschalt+C. Die Erweiterung per
    /// Umschalt+Pfeil wird separat mit echten WPF-KeyEventArgs getestet, weil die Windows-
    /// Injektion im Testdesktop Modifier nicht zuverlässig an WPF weitergibt.</summary>
    private Task ReplayText_CopyShortcutWithFocusedSelection(Window mainWindow)
        => ReplayText_MitFrischerAufzeichnung(mainWindow, "E2E-Textauswahl-Tastatur", dialog =>
        {
            // Der Sentinel belegt, dass Ctrl+Shift+C den Clipboard-Inhalt wirklich ersetzt.
            SetClipboardText("E2E-Clipboard-vor-Tastaturkopie");
            dialog.MarkiereReplayZellen(0, 0, 0, "erste Zeile".Length - 1, 80, 24);
            dialog.KopiereReplayAuswahl();
            Assert.Equal("erste Zeile", GetClipboardText());
        });

    /// <summary>Startet für genau einen Auswahlpfad eine isolierte Aufzeichnung, einen eigenen
    /// Dialog und eine eigene Replay-Session. Damit wird keine Interaktion zwischen Maus- und
    /// Tastaturauswahl als Produktsemantik vorausgesetzt.</summary>
    private async Task ReplayText_MitFrischerAufzeichnung(Window mainWindow, string pluginName, Action<KonsolenTestDialogView> pruefung)
    {
        var pfad = Path.Combine(Path.GetTempPath(), $"softwareschmiede_selection_{Guid.NewGuid():N}.clireplay");
        var prozessstartsVorher = File.Exists(ResolveProzessStartLogPfad())
            ? await File.ReadAllTextAsync(ResolveProzessStartLogPfad()) : string.Empty;
        SettingsView? settings = null;
        KonsolenTestDialogView? dialog = null;
        try
        {
            var store = new CliReplayAufzeichnungStore();
            await using (var stream = File.Create(pfad))
            {
                await store.SpeichernAsync(stream, new CliOutputAufzeichnung
                {
                    AufgabeId = Guid.NewGuid(),
                    PluginName = pluginName,
                    StartUtc = DateTimeOffset.UtcNow,
                    EndeUtc = DateTimeOffset.UtcNow,
                    Cols = 80,
                    Rows = 24,
                    IstVollstaendig = true,
                    Chunks =
                    [
                        new CliOutputChunkRecord(TimeSpan.Zero,
                            // Echte CR/LF-Zeilen erzwingen zwei Terminalzeilen; ein
                            // literales "\\r\\n" wäre kein Replay-Auswahlfall.
                            Encoding.UTF8.GetBytes("erste Zeile\r\nzweite Zeile")),
                    ],
                });
            }

            settings = new SettingsView(mainWindow).ForceShow();
            dialog = settings.OpenKonsolenTestDialog();
            dialog.SetZeitrafferSchwelle("0");
            dialog.OeffneAufzeichnung(pfad);
            dialog.WarteAufStatus($"Aufzeichnung geladen ({pluginName}, 1 Chunks) — bereit.");
            dialog.StartWiedergabe();
            dialog.WarteAufStatus("Wiedergabe beendet.");
            pruefung(dialog);

            // Replay besitzt absichtlich keinen InputStream. Während des gesamten Szenarios
            // darf daher weder eine Live-CLI gestartet noch eine Live-Prozesseingabe ausgelöst
            // worden sein; der Prozessstarter-Trace ist der externe Negativnachweis.
            var prozessstartsNachher = File.Exists(ResolveProzessStartLogPfad())
                ? await File.ReadAllTextAsync(ResolveProzessStartLogPfad()) : string.Empty;
            Assert.Equal(prozessstartsVorher, prozessstartsNachher);
        }
        finally
        {
            TryCloseKonsolenTestfenster(dialog, settings);
            if (File.Exists(pfad))
                File.Delete(pfad);
        }
    }

    private static string GetClipboardText()
    {
        // Die Windows-Zwischenablage kann kurz nach Ctrl+Shift+C noch belegt sein.
        // Begrenzte Wiederholungen halten den E2E-Test robust, ohne Fehler zu verstecken.
        Exception? lastError = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            string? text = null;
            Exception? error = null;
            var thread = new Thread(() =>
            {
                try { text = System.Windows.Clipboard.GetText(); }
                catch (Exception ex) { error = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (error is null)
                return text ?? string.Empty;

            lastError = error;
            Thread.Sleep(100);
        }

        throw new InvalidOperationException("Die Zwischenablage konnte nach 5 Versuchen nicht gelesen werden.", lastError);
    }

    /// <summary>
    /// Szenario: Synthetische .clireplay-Dateien werden auf der Platte erzeugt, das
    /// Konsolentestfenster über Einstellungen → Allgemein geöffnet. Abgedeckt werden der Abbruch
    /// des nativen Öffnen-Dialogs (kein Zustandswechsel, kein Fehler), eine defekte Datei
    /// (sichtbare FehlerMeldung), die Pflicht-Phase Pausieren (keine neuen Chunks) → Fortsetzen
    /// an einer Aufzeichnung mit langer Inter-Chunk-Pause, ein echtes erneutes Abspielen nach
    /// Ende, ein Neustart aus dem Pausiert-Zustand sowie das vollständige Abspielen mit
    /// Zeitraffer-Schwelle 0 (maximale Geschwindigkeit) inkl. Quell-Chunk-Liste mit sichtbar
    /// gemachten ANSI-Steuersequenzen und Endstatus "Wiedergabe beendet.".
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster der Anwendung.</param>
    protected async Task KonsolenTestfenster_OeffnetLaedtAufzeichnungUndSpieltAb_E2E(Window mainWindow)
    {
        var pfad = Path.Combine(Path.GetTempPath(), $"softwareschmiede_e2e_{Guid.NewGuid():N}.clireplay");
        var pausePfad = Path.Combine(Path.GetTempPath(), $"softwareschmiede_e2e_{Guid.NewGuid():N}.clireplay");
        var schrittPfad = Path.Combine(Path.GetTempPath(), $"softwareschmiede_e2e_{Guid.NewGuid():N}.clireplay");
        var defektPfad = Path.Combine(Path.GetTempPath(), $"softwareschmiede_e2e_{Guid.NewGuid():N}.clireplay");
        var breitPfad = Path.Combine(Path.GetTempPath(), $"softwareschmiede_e2e_{Guid.NewGuid():N}.clireplay");
        var schmalPfad = Path.Combine(Path.GetTempPath(), $"softwareschmiede_e2e_{Guid.NewGuid():N}.clireplay");
        SettingsView? settings = null;
        KonsolenTestDialogView? dialog = null;
        try
        {
            var store = new CliReplayAufzeichnungStore();

            // Synthetische Aufzeichnung mit zwei getrennten Roh-Chunks (Chunk-Grenzen bleiben
            // sichtbar); Chunk 1 enthält SGR-Farbsequenzen — die Quell-Ansicht muss die
            // Steuersequenzen sichtbar machen (ESC als ␛).
            await using (var stream = File.Create(pfad))
            {
                await store.SpeichernAsync(stream, new CliOutputAufzeichnung
                {
                    AufgabeId = Guid.NewGuid(),
                    PluginName = "E2E-Diagnose",
                    StartUtc = DateTimeOffset.UtcNow,
                    EndeUtc = DateTimeOffset.UtcNow.AddMilliseconds(50),
                    Cols = 80,
                    Rows = 24,
                    IstVollstaendig = true,
                    Chunks =
                    [
                        new CliOutputChunkRecord(TimeSpan.Zero, Encoding.UTF8.GetBytes("\x1b[31mE2E-Replay-Chunk-1\x1b[0m")),
                        new CliOutputChunkRecord(TimeSpan.FromMilliseconds(50), Encoding.UTF8.GetBytes(" -> Chunk-2")),
                    ],
                });
            }

            // Zweite Aufzeichnung mit langer Inter-Chunk-Pause (3 s): darin lässt sich die
            // Pausieren/Fortsetzen-Phase deterministisch innerhalb der realen Pause auslösen.
            await using (var stream = File.Create(pausePfad))
            {
                await store.SpeichernAsync(stream, new CliOutputAufzeichnung
                {
                    AufgabeId = Guid.NewGuid(),
                    PluginName = "E2E-Pause",
                    StartUtc = DateTimeOffset.UtcNow,
                    EndeUtc = DateTimeOffset.UtcNow.AddSeconds(3),
                    Cols = 80,
                    Rows = 24,
                    IstVollstaendig = true,
                    Chunks =
                    [
                        new CliOutputChunkRecord(TimeSpan.Zero, Encoding.UTF8.GetBytes("pause-chunk-1")),
                        new CliOutputChunkRecord(TimeSpan.FromSeconds(3), Encoding.UTF8.GetBytes("pause-chunk-2")),
                    ],
                });
            }

            // Dritte Aufzeichnung für den Schrittmodus: drei inhaltlich unterscheidbare Chunks
            // mit minimalen Offsets (Schritte sind zeitstempel-unabhängig; das Fortsetzen nach
            // einem Rückwärtsschritt läuft unter Zeitraffer-Schwelle 0 ohne realen Delay ab).
            await using (var stream = File.Create(schrittPfad))
            {
                await store.SpeichernAsync(stream, new CliOutputAufzeichnung
                {
                    AufgabeId = Guid.NewGuid(),
                    PluginName = "E2E-Schritt",
                    StartUtc = DateTimeOffset.UtcNow,
                    EndeUtc = DateTimeOffset.UtcNow.AddMilliseconds(100),
                    Cols = 80,
                    Rows = 24,
                    IstVollstaendig = true,
                    Chunks =
                    [
                        new CliOutputChunkRecord(TimeSpan.Zero, Encoding.UTF8.GetBytes("schritt-chunk-1")),
                        new CliOutputChunkRecord(TimeSpan.FromMilliseconds(50), Encoding.UTF8.GetBytes(" -> schritt-chunk-2")),
                        new CliOutputChunkRecord(TimeSpan.FromMilliseconds(100), Encoding.UTF8.GetBytes(" -> schritt-chunk-3")),
                    ],
                });
            }

            // Aufzeichnung mit fixierter Geometrie 220×50 — deutlich breiter als der sichtbare
            // Bereich des Dialogs: der Replay-Buffer behält die Aufzeichnungsbreite, der
            // überschüssige Inhalt muss horizontal scrollbar werden.
            await using (var stream = File.Create(breitPfad))
            {
                await store.SpeichernAsync(stream, new CliOutputAufzeichnung
                {
                    AufgabeId = Guid.NewGuid(),
                    PluginName = "E2E-Breit",
                    StartUtc = DateTimeOffset.UtcNow,
                    EndeUtc = DateTimeOffset.UtcNow.AddMilliseconds(10),
                    Cols = 220,
                    Rows = 50,
                    IstVollstaendig = true,
                    Chunks =
                    [
                        new CliOutputChunkRecord(TimeSpan.Zero, Encoding.UTF8.GetBytes("e2e-breit")),
                    ],
                });
            }

            // Schmale Aufzeichnung (60×20): passt vollständig in den sichtbaren Bereich —
            // es darf keine horizontale Scrollbar entstehen.
            await using (var stream = File.Create(schmalPfad))
            {
                await store.SpeichernAsync(stream, new CliOutputAufzeichnung
                {
                    AufgabeId = Guid.NewGuid(),
                    PluginName = "E2E-Schmal",
                    StartUtc = DateTimeOffset.UtcNow,
                    EndeUtc = DateTimeOffset.UtcNow.AddMilliseconds(10),
                    Cols = 60,
                    Rows = 20,
                    IstVollstaendig = true,
                    Chunks =
                    [
                        new CliOutputChunkRecord(TimeSpan.Zero, Encoding.UTF8.GetBytes("e2e-schmal")),
                    ],
                });
            }

            // Defekte Datei: falsches Magic → Formatfehler muss im Dialog sichtbar werden.
            await File.WriteAllBytesAsync(defektPfad, Encoding.ASCII.GetBytes("KEINCLIREPLAY-FORMAT"));

            settings = new SettingsView(mainWindow).ForceShow();
            dialog = settings.OpenKonsolenTestDialog();

            // Abbruch-Phase: Öffnen-Dialog per ESC abbrechen — kein Fehler, kein Zustandswechsel.
            dialog.OeffneAufzeichnungAbbrechen();
            Assert.False(dialog.IstFehlerSichtbar(),
                "Der Abbruch des Öffnen-Dialogs darf keine Fehlermeldung auslösen.");
            Assert.Equal(0, dialog.GetQuellEintraegeCount());

            // Formatfehler-Phase: defekte Datei muss eine sichtbare Fehlermeldung erzeugen.
            dialog.OeffneAufzeichnung(defektPfad);
            dialog.WarteAufFehlerSichtbar();
            Assert.Contains("geladen", dialog.GetFehlerMeldung(), StringComparison.Ordinal);

            // Schrittmodus-Phase 1: Einzelschritte ohne gestartete Wiedergabe — Positionsanzeige
            // und Quell-Selektion folgen der Schrittposition; an Position 0 ist "Schritt zurück"
            // deaktiviert. (Der gerenderte Terminalinhalt ist über UI-Automation nicht lesbar —
            // kein TextPattern am custom gerenderten TerminalControl; der deterministische
            // Buffer-Neuaufbau ist per Unit-Test in TerminalReplaySessionTests nachgewiesen.)
            dialog.SetZeitrafferSchwelle("0");
            dialog.OeffneAufzeichnung(schrittPfad);
            dialog.WarteAufStatus("Aufzeichnung geladen (E2E-Schritt, 3 Chunks) — bereit.");
            Assert.Equal("Chunk 0/3", dialog.GetPositionsText());
            Assert.Equal(-1, dialog.GetSelektierterQuellEintragIndex());
            Assert.False(dialog.IstSchaltflaecheAktiviert("SchrittZurueck"),
                "an Position 0 muss 'Schritt zurück' deaktiviert sein");

            dialog.SchrittVor();
            dialog.SchrittVor();
            dialog.WarteAufPosition("Chunk 2/3");
            dialog.WarteAufStatus("Einzelschritt — Chunk 2/3 angewendet.");
            // Die Quell-Selektion muss den zuletzt angewendeten Chunk markieren.
            Assert.Equal(1, dialog.GetSelektierterQuellEintragIndex());
            Assert.True(dialog.IstSchaltflaecheAktiviert("WiedergabeNeustarten"),
                "aus dem Schrittmodus muss 'Neu starten' als direkter Rückweg zu Position 0 erreichbar sein");

            dialog.SchrittZurueck();
            dialog.WarteAufPosition("Chunk 1/3");
            dialog.WarteAufStatus("Schritt zurück — Chunk 2/3 zurückgenommen.");
            Assert.Equal(0, dialog.GetSelektierterQuellEintragIndex());
            dialog.SchrittZurueck();
            dialog.WarteAufPosition("Chunk 0/3");
            // An Position 0 darf kein Quell-Eintrag selektiert sein.
            Assert.Equal(-1, dialog.GetSelektierterQuellEintragIndex());
            Assert.False(dialog.IstSchaltflaecheAktiviert("SchrittZurueck"),
                "an Position 0 muss 'Schritt zurück' deaktiviert sein");
            Assert.False(dialog.IstSchaltflaecheAktiviert("WiedergabeNeustarten"),
                "an Position 0 ohne laufende Wiedergabe muss 'Neu starten' deaktiviert sein");

            // Schrittmodus-Phase 2: bis ans Ende schreiten feuert die Ende-Semantik; danach
            // zurückschreiten und "Abspielen" setzt an der Schrittposition fort (Re-Arm).
            dialog.SchrittVor();
            dialog.SchrittVor();
            dialog.SchrittVor();
            dialog.WarteAufStatus("Wiedergabe beendet.");
            Assert.Equal("Chunk 3/3", dialog.GetPositionsText());
            Assert.False(dialog.IstSchaltflaecheAktiviert("SchrittVor"),
                "am Ende der Aufzeichnung muss 'Schritt vor' deaktiviert sein");

            dialog.SchrittZurueck();
            dialog.WarteAufPosition("Chunk 2/3");
            Assert.Equal(1, dialog.GetSelektierterQuellEintragIndex());

            dialog.StartWiedergabe();
            dialog.WarteAufStatus("Wiedergabe beendet.");
            Assert.Equal("Chunk 3/3", dialog.GetPositionsText());

            // Pause-Phase: großzügige Zeitraffer-Schwelle, damit die reale 3-s-Pause des zweiten
            // Chunks nicht verkürzt wird und die Pausierung deterministisch darin landet.
            dialog.SetZeitrafferSchwelle("10");
            dialog.OeffneAufzeichnung(pausePfad);
            dialog.WarteAufStatus("Aufzeichnung geladen (E2E-Pause, 2 Chunks) — bereit.");
            Assert.False(dialog.IstFehlerSichtbar(),
                "Nach erfolgreichem Laden darf kein Fehlerbanner mehr sichtbar sein.");

            dialog.StartWiedergabe();
            dialog.WarteAufPosition("Chunk 1/2");
            dialog.PausierenToggle();
            dialog.WarteAufStatus("Pausiert.");

            // Während der Pausierung darf kein weiterer Chunk angewendet werden.
            Thread.Sleep(1200);
            Assert.Equal("Chunk 1/2", dialog.GetPositionsText());

            dialog.PausierenToggle();
            dialog.WarteAufStatus("Wiedergabe beendet.");
            Assert.Equal("Chunk 2/2", dialog.GetPositionsText());

            // Erneutes Abspielen nach Ende: ein zweiter Start muss die Aufzeichnung tatsächlich
            // nochmals durchlaufen. Die 3-s-Pause des zweiten Chunks hält den Status
            // "Wiedergabe läuft." deterministisch beobachtbar — ohne echten Neustart bliebe
            // Status/Position bei "beendet"/"Chunk 2/2".
            dialog.StartWiedergabe();
            dialog.WarteAufStatus("Wiedergabe läuft.");

            // Neustart mitten im Lauf aus dem Pausiert-Zustand: der Statuswechsel
            // "Pausiert." → "Wiedergabe läuft." belegt deterministisch, dass die laufende
            // Wiedergabe verworfen und dieselbe Aufzeichnung sofort wieder ab Position 0
            // abgespielt wird.
            dialog.PausierenToggle();
            dialog.WarteAufStatus("Pausiert.");
            dialog.NeustartWiedergabe();
            dialog.WarteAufStatus("Wiedergabe läuft.");

            dialog.WarteAufStatus("Wiedergabe beendet.");
            Assert.Equal("Chunk 2/2", dialog.GetPositionsText());

            // Schrittmodus-Phase 3 (pausiert → Schritt zurück → fortsetzen): die Schleife setzt
            // an der durch den Schritt veränderten Position fort, wartet die aufgezeichnete
            // 3-s-Pause des zurückgenommenen Chunks erneut ab und wendet ihn erneut an.
            dialog.OeffneAufzeichnung(pausePfad);
            dialog.WarteAufStatus("Aufzeichnung geladen (E2E-Pause, 2 Chunks) — bereit.");
            dialog.StartWiedergabe();
            dialog.WarteAufPosition("Chunk 1/2");
            dialog.PausierenToggle();
            dialog.WarteAufStatus("Pausiert.");

            dialog.SchrittZurueck();
            dialog.WarteAufPosition("Chunk 0/2");
            Assert.Equal(-1, dialog.GetSelektierterQuellEintragIndex());

            dialog.PausierenToggle();
            dialog.WarteAufStatus("Wiedergabe läuft.");
            dialog.WarteAufPosition("Chunk 1/2");
            // Während der erneut abgewarteten 3-s-Pause (unpausiert laufende Wiedergabe)
            // müssen beide Schritt-Buttons deaktiviert sein.
            Assert.False(dialog.IstSchaltflaecheAktiviert("SchrittVor"),
                "während unpausierter Wiedergabe muss 'Schritt vor' deaktiviert sein");
            Assert.False(dialog.IstSchaltflaecheAktiviert("SchrittZurueck"),
                "während unpausierter Wiedergabe muss 'Schritt zurück' deaktiviert sein");

            dialog.WarteAufStatus("Wiedergabe beendet.");
            Assert.Equal("Chunk 2/2", dialog.GetPositionsText());

            // Maximale Geschwindigkeit: Inter-Chunk-Pausen der Aufzeichnung ignorieren.
            dialog.SetZeitrafferSchwelle("0");
            dialog.OeffneAufzeichnung(pfad);
            dialog.WarteAufStatus("Aufzeichnung geladen (E2E-Diagnose, 2 Chunks) — bereit.");

            Assert.False(dialog.IstFehlerSichtbar(),
                "Nach erfolgreichem Laden darf kein Fehlerbanner mehr sichtbar sein.");
            Assert.Equal(2, dialog.GetQuellEintraegeCount());
            // Die Quell-Ansicht macht ANSI-Steuersequenzen sichtbar (ESC als ␛).
            Assert.Contains("␛[31m", dialog.GetQuellEintragText(0), StringComparison.Ordinal);
            Assert.Contains("E2E-Replay-Chunk-1", dialog.GetQuellEintragText(0), StringComparison.Ordinal);
            Assert.Contains("␛[0m", dialog.GetQuellEintragText(0), StringComparison.Ordinal);

            dialog.StartWiedergabe();
            dialog.WarteAufStatus("Wiedergabe beendet.");
            Assert.Equal("Chunk 2/2", dialog.GetPositionsText());

            // Geometrie-Phase: die Wiedergabe rendert in der aufgezeichneten Geometrie statt
            // der Fenstergröße. Funktionaler Nachweis über den ReplayTerminalScrollViewer —
            // der gerenderte Terminalinhalt ist per UI-Automation nicht lesbar; ein
            // HorizontallyScrollable == true impliziert ExtentWidth > ViewportWidth und damit
            // einen nicht auf die Fensterbreite verkleinerten Buffer.
            dialog.OeffneAufzeichnung(breitPfad);
            dialog.WarteAufStatus("Aufzeichnung geladen (E2E-Breit, 1 Chunks) — bereit.");
            Assert.Equal("Aufzeichnung: 220×50", dialog.GetGeometrieText());
            dialog.WarteAufHorizontalScrollFaellig();
            Assert.True(dialog.IstHorizontalScrollbar(),
                "eine 220×50-Aufzeichnung übersteigt die Dialogbreite — der Inhalt muss horizontal scrollbar sein");
            Assert.True(dialog.GetHorizontalViewSize() < 100,
                "der sichtbare Ausschnitt muss kleiner als der Gesamtinhalt sein");

            dialog.SetzeHorizontalScrollProzent(50);
            var horizontalGescrollt = false;
            var scrollDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (DateTime.UtcNow < scrollDeadline)
            {
                if (dialog.GetHorizontalScrollPercent() > 0)
                {
                    horizontalGescrollt = true;
                    break;
                }

                Thread.Sleep(200);
            }
            Assert.True(horizontalGescrollt,
                "SetScrollPercent muss den horizontalen Offset des ReplayTerminal-ScrollViewers verschieben");

            // Tastatur-Scrolling: das Replay-Terminal besitzt keinen Eingabekanal — die
            // Ende-Taste darf vom VT100-Encoder nicht verschluckt werden, sondern muss zum
            // umschließenden ScrollViewer bubbeln (horizontal ans rechte Ende scrollen).
            var prozentVorTaste = dialog.GetHorizontalScrollPercent();
            dialog.DrueckeTasteImReplayTerminal(VirtualKeyShort.END);
            var tastaturGescrollt = false;
            var tastaturDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (DateTime.UtcNow < tastaturDeadline)
            {
                if (dialog.GetHorizontalScrollPercent() > prozentVorTaste)
                {
                    tastaturGescrollt = true;
                    break;
                }

                Thread.Sleep(200);
            }
            Assert.True(tastaturGescrollt,
                "die Ende-Taste im fokussierten Replay-Terminal muss den ScrollViewer horizontal weiter nach rechts scrollen");

            // Schmale Aufzeichnung (60×20): passt in den Viewport — kein horizontaler Scrollbalken.
            // Das Polling auf „nicht scrollbar" überbrückt die asynchrone Extent-Aktualisierung
            // nach dem Session-Wechsel (die zuvor breite Extent darf nicht transient greifen).
            dialog.OeffneAufzeichnung(schmalPfad);
            dialog.WarteAufStatus("Aufzeichnung geladen (E2E-Schmal, 1 Chunks) — bereit.");
            Assert.Equal("Aufzeichnung: 60×20", dialog.GetGeometrieText());
            var schmalNichtScrollbar = false;
            var schmalDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (DateTime.UtcNow < schmalDeadline)
            {
                if (!dialog.IstHorizontalScrollbar())
                {
                    schmalNichtScrollbar = true;
                    break;
                }

                Thread.Sleep(200);
            }
            Assert.True(schmalNichtScrollbar,
                "eine 60×20-Aufzeichnung passt in den Viewport — es darf keine horizontale Scrollbar geben");

            dialog.Schliessen();
            Assert.False(dialog.IsVisible);

            // Nach dem Schließen ist wieder die Einstellungsansicht aktiv.
            Assert.IsType<SettingsView>(mainWindow.CurrentView());
        }
        finally
        {
            // TryClose-Muster (analog TryCloseTaskDetail in E2E_CliRawExport.cs): das
            // nicht-modale Konsolentestfenster muss auch bei einem Assert-Fehler geschlossen
            // werden — sonst erkennt CurrentView() der Folgeszenarien in RunGeneralTests den
            // offenen Dialog statt der Hauptansicht.
            TryCloseKonsolenTestfenster(dialog, settings);

            if (File.Exists(pfad))
                File.Delete(pfad);
            if (File.Exists(pausePfad))
                File.Delete(pausePfad);
            if (File.Exists(schrittPfad))
                File.Delete(schrittPfad);
            if (File.Exists(defektPfad))
                File.Delete(defektPfad);
            if (File.Exists(breitPfad))
                File.Delete(breitPfad);
            if (File.Exists(schmalPfad))
                File.Delete(schmalPfad);
        }
    }

    /// <summary>Best-Effort-Schließen des Konsolentestfensters und der Einstellungsansicht —
    /// Fehler beim Schließen (z. B. bereits geschlossen) werden bewusst ignoriert.</summary>
    private static void TryCloseKonsolenTestfenster(KonsolenTestDialogView? dialog, SettingsView? settings)
    {
        try
        {
            if (dialog is not null && dialog.IsVisible)
                dialog.Schliessen();
        }
        catch
        {
        }

        try
        {
            settings?.ForceClose(recurseToDashboard: false);
        }
        catch
        {
        }
    }
}

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
/// ConPTY-Abhängigkeit — die Aufzeichnungen werden direkt über <see cref="CliReplayRecordingStore"/>
/// erzeugt).
/// </summary>
public partial class End2EndTest
{
    /// <summary>Pflichtabnahme E-02 (Mauspfad): Eine mehrzeilige Auswahl wird in einer frischen
    /// Replay-Ansicht per Drag angelegt und mit Strg+Umschalt+C kopiert.</summary>
    private Task ReplayText_MarkAndCopyViaMouse(Window mainWindow)
        => ReplayText_WithFreshRecording(mainWindow, "E2E-Textauswahl-Maus", dialog =>
        {
            // Die Kopier-Geste läuft in der gestarteten App — das Lock deckt die komplette
            // Region ab, damit keine parallele Test-Collection die Zwischenablage belegt.
            using var _ = ClipboardTestLock.Halten();
            // UIA kann den Custom-Renderer nicht lesen. Daher wird das gleiche Zellrechteck
            // vor und nach dem echten Mausdrag aufgenommen; genügend abweichende Pixel belegen
            // das vom TerminalControl gezeichnete Auswahl-Overlay statt nur dessen Zustand.
            var beforeMouseselection = dialog.ErfasseReplayZellPixel(0, 0, 1, "zweite Zeile".Length - 1, 80, 24);
            dialog.MarkReplayCells(0, 0, 1, "zweite Zeile".Length - 1, 80, 24);
            var afterMouseselection = dialog.ErfasseReplayZellPixel(0, 0, 1, "zweite Zeile".Length - 1, 80, 24);
            Assert.True(ConsoleTestDialogView.CountDeviatingReplayPixel(beforeMouseselection, afterMouseselection) > 100,
                "der Mausdrag muss im tatsächlichen Replay-Terminal eine deutlich sichtbare Auswahl-Hervorhebung zeichnen");
            dialog.CopyReplaySelection();
            Assert.Equal($"erste Zeile{Environment.NewLine}zweite Zeile", GetClipboardText());
        });

    /// <summary>E-02-Ergänzung: Der echte Replay-Dialog nimmt den Tastaturfokus an und
    /// kopiert eine bereits sichtbare Auswahl über Strg+Umschalt+C. Die Erweiterung per
    /// Umschalt+Pfeil wird separat mit echten WPF-KeyEventArgs getestet, weil die Windows-
    /// Injektion im Testdesktop Modifier nicht zuverlässig an WPF weitergibt.</summary>
    private Task ReplayText_CopyShortcutWithFocusedSelection(Window mainWindow)
        => ReplayText_WithFreshRecording(mainWindow, "E2E-Textauswahl-Tastatur", dialog =>
        {
            // Die Kopier-Geste läuft in der gestarteten App — das Lock deckt die komplette
            // Region ab, damit keine parallele Test-Collection die Zwischenablage belegt.
            using var _ = ClipboardTestLock.Halten();
            // Der Sentinel belegt, dass Ctrl+Shift+C den Clipboard-Inhalt wirklich ersetzt.
            SetClipboardText("E2E-Clipboard-vor-Tastaturkopie");
            dialog.MarkReplayCells(0, 0, 0, "erste Zeile".Length - 1, 80, 24);
            dialog.CopyReplaySelection();
            Assert.Equal("erste Zeile", GetClipboardText());
        });

    /// <summary>Startet für genau einen Auswahlpfad eine isolierte Aufzeichnung, einen eigenen
    /// Dialog und eine eigene Replay-Session. Damit wird keine Interaktion zwischen Maus- und
    /// Tastaturauswahl als Produktsemantik vorausgesetzt.</summary>
    private async Task ReplayText_WithFreshRecording(Window mainWindow, string pluginName, Action<ConsoleTestDialogView> check)
    {
        var path = Path.Combine(Path.GetTempPath(), $"softwareschmiede_selection_{Guid.NewGuid():N}.clireplay");
        var prozessstartsBefore = File.Exists(ResolveProcessStartLogPath())
            ? await File.ReadAllTextAsync(ResolveProcessStartLogPath()) : string.Empty;
        SettingsView? settings = null;
        ConsoleTestDialogView? dialog = null;
        try
        {
            var store = new CliReplayRecordingStore();
            await using (var stream = File.Create(path))
            {
                await store.SaveAsync(stream, new CliOutputRecording
                {
                    WorkItemId = Guid.NewGuid(),
                    PluginName = pluginName,
                    StartUtc = DateTimeOffset.UtcNow,
                    EndUtc = DateTimeOffset.UtcNow,
                    Cols = 80,
                    Rows = 24,
                    IsComplete = true,
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
            dialog = settings.OpenConsoleTestDialog();
            dialog.SetTimelapseThreshold("0");
            dialog.OpenRecording(path);
            dialog.WaitOnStatus($"Aufzeichnung geladen ({pluginName}, 1 Chunks) — bereit.");
            dialog.StartReplay();
            dialog.WaitOnStatus("Wiedergabe beendet.");
            check(dialog);

            // Replay besitzt absichtlich keinen InputStream. Während des gesamten Szenarios
            // darf daher weder eine Live-CLI gestartet noch eine Live-Prozesseingabe ausgelöst
            // worden sein; der Prozessstarter-Trace ist der externe Negativnachweis.
            var prozessstartsNachher = File.Exists(ResolveProcessStartLogPath())
                ? await File.ReadAllTextAsync(ResolveProcessStartLogPath()) : string.Empty;
            Assert.Equal(prozessstartsBefore, prozessstartsNachher);
        }
        finally
        {
            TryCloseConsoleTestwindow(dialog, settings);
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static string GetClipboardText()
    {
        // Die Windows-Zwischenablage kann kurz nach Ctrl+Shift+C noch belegt sein.
        // Begrenzte Wiederholungen halten den E2E-Test robust, ohne Fehler zu verstecken.
        string? text = null;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { text = ClipboardTestHelfer.GetTextWithRetry(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null)
            throw error;
        return text ?? string.Empty;
    }

    /// <summary>
    /// Szenario: Synthetische .clireplay-Dateien werden auf der Platte erzeugt, das
    /// Konsolentestfenster über Einstellungen → Allgemein geöffnet. Abgedeckt werden der Abbruch
    /// des nativen Öffnen-Dialogs (kein Zustandswechsel, kein Fehler), eine defekte Datei
    /// (sichtbare ErrorMessage), die Pflicht-Phase Pausieren (keine neuen Chunks) → Fortsetzen
    /// an einer Aufzeichnung mit langer Inter-Chunk-Pause, ein echtes erneutes Abspielen nach
    /// Ende, ein Neustart aus dem Pausiert-Zustand sowie das vollständige Abspielen mit
    /// Zeitraffer-Schwelle 0 (maximale Geschwindigkeit) inkl. Quell-Chunk-Liste mit sichtbar
    /// gemachten ANSI-Steuersequenzen und Endstatus "Wiedergabe beendet.".
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster der Anwendung.</param>
    protected async Task ConsoleTestwindow_OpensLoadsRecordingAndPlaysFrom_E2E(Window mainWindow)
    {
        var path = Path.Combine(Path.GetTempPath(), $"softwareschmiede_e2e_{Guid.NewGuid():N}.clireplay");
        var pausePath = Path.Combine(Path.GetTempPath(), $"softwareschmiede_e2e_{Guid.NewGuid():N}.clireplay");
        var stepPath = Path.Combine(Path.GetTempPath(), $"softwareschmiede_e2e_{Guid.NewGuid():N}.clireplay");
        var defectPath = Path.Combine(Path.GetTempPath(), $"softwareschmiede_e2e_{Guid.NewGuid():N}.clireplay");
        var widePath = Path.Combine(Path.GetTempPath(), $"softwareschmiede_e2e_{Guid.NewGuid():N}.clireplay");
        var narrowPath = Path.Combine(Path.GetTempPath(), $"softwareschmiede_e2e_{Guid.NewGuid():N}.clireplay");
        SettingsView? settings = null;
        ConsoleTestDialogView? dialog = null;
        try
        {
            var store = new CliReplayRecordingStore();

            // Synthetische Aufzeichnung mit zwei getrennten Roh-Chunks (Chunk-Grenzen bleiben
            // sichtbar); Chunk 1 enthält SGR-Farbsequenzen — die Quell-Ansicht muss die
            // Steuersequenzen sichtbar machen (ESC als ␛).
            await using (var stream = File.Create(path))
            {
                await store.SaveAsync(stream, new CliOutputRecording
                {
                    WorkItemId = Guid.NewGuid(),
                    PluginName = "E2E-Diagnose",
                    StartUtc = DateTimeOffset.UtcNow,
                    EndUtc = DateTimeOffset.UtcNow.AddMilliseconds(50),
                    Cols = 80,
                    Rows = 24,
                    IsComplete = true,
                    Chunks =
                    [
                        new CliOutputChunkRecord(TimeSpan.Zero, Encoding.UTF8.GetBytes("\x1b[31mE2E-Replay-Chunk-1\x1b[0m")),
                        new CliOutputChunkRecord(TimeSpan.FromMilliseconds(50), Encoding.UTF8.GetBytes(" -> Chunk-2")),
                    ],
                });
            }

            // Zweite Aufzeichnung mit langer Inter-Chunk-Pause (3 s): darin lässt sich die
            // Pausieren/Fortsetzen-Phase deterministisch innerhalb der realen Pause auslösen.
            await using (var stream = File.Create(pausePath))
            {
                await store.SaveAsync(stream, new CliOutputRecording
                {
                    WorkItemId = Guid.NewGuid(),
                    PluginName = "E2E-Pause",
                    StartUtc = DateTimeOffset.UtcNow,
                    EndUtc = DateTimeOffset.UtcNow.AddSeconds(3),
                    Cols = 80,
                    Rows = 24,
                    IsComplete = true,
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
            await using (var stream = File.Create(stepPath))
            {
                await store.SaveAsync(stream, new CliOutputRecording
                {
                    WorkItemId = Guid.NewGuid(),
                    PluginName = "E2E-Schritt",
                    StartUtc = DateTimeOffset.UtcNow,
                    EndUtc = DateTimeOffset.UtcNow.AddMilliseconds(100),
                    Cols = 80,
                    Rows = 24,
                    IsComplete = true,
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
            await using (var stream = File.Create(widePath))
            {
                await store.SaveAsync(stream, new CliOutputRecording
                {
                    WorkItemId = Guid.NewGuid(),
                    PluginName = "E2E-Breit",
                    StartUtc = DateTimeOffset.UtcNow,
                    EndUtc = DateTimeOffset.UtcNow.AddMilliseconds(10),
                    Cols = 220,
                    Rows = 50,
                    IsComplete = true,
                    Chunks =
                    [
                        new CliOutputChunkRecord(TimeSpan.Zero, Encoding.UTF8.GetBytes("e2e-breit")),
                    ],
                });
            }

            // Schmale Aufzeichnung (60×20): passt vollständig in den sichtbaren Bereich —
            // es darf keine horizontale Scrollbar entstehen.
            await using (var stream = File.Create(narrowPath))
            {
                await store.SaveAsync(stream, new CliOutputRecording
                {
                    WorkItemId = Guid.NewGuid(),
                    PluginName = "E2E-Schmal",
                    StartUtc = DateTimeOffset.UtcNow,
                    EndUtc = DateTimeOffset.UtcNow.AddMilliseconds(10),
                    Cols = 60,
                    Rows = 20,
                    IsComplete = true,
                    Chunks =
                    [
                        new CliOutputChunkRecord(TimeSpan.Zero, Encoding.UTF8.GetBytes("e2e-schmal")),
                    ],
                });
            }

            // Defekte Datei: falsches Magic → Formatfehler muss im Dialog sichtbar werden.
            await File.WriteAllBytesAsync(defectPath, Encoding.ASCII.GetBytes("KEINCLIREPLAY-FORMAT"));

            settings = new SettingsView(mainWindow).ForceShow();
            dialog = settings.OpenConsoleTestDialog();

            // Abbruch-Phase: Öffnen-Dialog per ESC abbrechen — kein Fehler, kein Zustandswechsel.
            dialog.OpenRecordingCancel();
            Assert.False(dialog.IsErrorVisible(),
                "Der Abbruch des Öffnen-Dialogs darf keine Fehlermeldung auslösen.");
            Assert.Equal(0, dialog.GetSourceEntriesCount());

            // Formatfehler-Phase: defekte Datei muss eine sichtbare Fehlermeldung erzeugen.
            dialog.OpenRecording(defectPath);
            dialog.WaitOnErrorVisible();
            Assert.Contains("geladen", dialog.GetErrorMessage(), StringComparison.Ordinal);

            // Schrittmodus-Phase 1: Einzelschritte ohne gestartete Wiedergabe — Positionsanzeige
            // und Quell-Selektion folgen der Schrittposition; an Position 0 ist "Schritt zurück"
            // deaktiviert. (Der gerenderte Terminalinhalt ist über UI-Automation nicht lesbar —
            // kein TextPattern am custom gerenderten TerminalControl; der deterministische
            // Buffer-Neuaufbau ist per Unit-Test in TerminalReplaySessionTests nachgewiesen.)
            dialog.SetTimelapseThreshold("0");
            dialog.OpenRecording(stepPath);
            dialog.WaitOnStatus("Aufzeichnung geladen (E2E-Schritt, 3 Chunks) — bereit.");
            Assert.Equal("Chunk 0/3", dialog.GetPositionText());
            Assert.Equal(-1, dialog.GetSelectsSourceEntryIndex());
            Assert.False(dialog.IsButtonEnabled("StepBack"),
                "an Position 0 muss 'Schritt zurück' deaktiviert sein");

            dialog.StepForward();
            dialog.StepForward();
            dialog.WaitOnPosition("Chunk 2/3");
            dialog.WaitOnStatus("Einzelschritt — Chunk 2/3 angewendet.");
            // Die Quell-Selektion muss den zuletzt angewendeten Chunk markieren.
            Assert.Equal(1, dialog.GetSelectsSourceEntryIndex());
            Assert.True(dialog.IsButtonEnabled("RestartPlayback"),
                "aus dem Schrittmodus muss 'Neu starten' als direkter Rückweg zu Position 0 erreichbar sein");

            dialog.StepBackward();
            dialog.WaitOnPosition("Chunk 1/3");
            dialog.WaitOnStatus("Schritt zurück — Chunk 2/3 zurückgenommen.");
            Assert.Equal(0, dialog.GetSelectsSourceEntryIndex());
            dialog.StepBackward();
            dialog.WaitOnPosition("Chunk 0/3");
            // An Position 0 darf kein Quell-Eintrag selektiert sein.
            Assert.Equal(-1, dialog.GetSelectsSourceEntryIndex());
            Assert.False(dialog.IsButtonEnabled("StepBack"),
                "an Position 0 muss 'Schritt zurück' deaktiviert sein");
            Assert.False(dialog.IsButtonEnabled("RestartPlayback"),
                "an Position 0 ohne laufende Wiedergabe muss 'Neu starten' deaktiviert sein");

            // Schrittmodus-Phase 2: bis ans Ende schreiten feuert die Ende-Semantik; danach
            // zurückschreiten und "Abspielen" setzt an der Schrittposition fort (Re-Arm).
            dialog.StepForward();
            dialog.StepForward();
            dialog.StepForward();
            dialog.WaitOnStatus("Wiedergabe beendet.");
            Assert.Equal("Chunk 3/3", dialog.GetPositionText());
            Assert.False(dialog.IsButtonEnabled("StepForward"),
                "am Ende der Aufzeichnung muss 'Schritt vor' deaktiviert sein");

            dialog.StepBackward();
            dialog.WaitOnPosition("Chunk 2/3");
            Assert.Equal(1, dialog.GetSelectsSourceEntryIndex());

            dialog.StartReplay();
            dialog.WaitOnStatus("Wiedergabe beendet.");
            Assert.Equal("Chunk 3/3", dialog.GetPositionText());

            // Pause-Phase: großzügige Zeitraffer-Schwelle, damit die reale 3-s-Pause des zweiten
            // Chunks nicht verkürzt wird und die Pausierung deterministisch darin landet.
            dialog.SetTimelapseThreshold("10");
            dialog.OpenRecording(pausePath);
            dialog.WaitOnStatus("Aufzeichnung geladen (E2E-Pause, 2 Chunks) — bereit.");
            Assert.False(dialog.IsErrorVisible(),
                "Nach erfolgreichem Laden darf kein Fehlerbanner mehr sichtbar sein.");

            dialog.StartReplay();
            dialog.WaitOnPosition("Chunk 1/2");
            dialog.PauseToggle();
            dialog.WaitOnStatus("Pausiert.");

            // Während der Pausierung darf kein weiterer Chunk angewendet werden.
            Thread.Sleep(1200);
            Assert.Equal("Chunk 1/2", dialog.GetPositionText());

            dialog.PauseToggle();
            dialog.WaitOnStatus("Wiedergabe beendet.");
            Assert.Equal("Chunk 2/2", dialog.GetPositionText());

            // Erneutes Abspielen nach Ende: ein zweiter Start muss die Aufzeichnung tatsächlich
            // nochmals durchlaufen. Die 3-s-Pause des zweiten Chunks hält den Status
            // "Wiedergabe läuft." deterministisch beobachtbar — ohne echten Neustart bliebe
            // Status/Position bei "beendet"/"Chunk 2/2".
            dialog.StartReplay();
            dialog.WaitOnStatus("Wiedergabe läuft.");

            // Neustart mitten im Lauf aus dem Pausiert-Zustand: der Statuswechsel
            // "Pausiert." → "Wiedergabe läuft." belegt deterministisch, dass die laufende
            // Wiedergabe verworfen und dieselbe Aufzeichnung sofort wieder ab Position 0
            // abgespielt wird.
            dialog.PauseToggle();
            dialog.WaitOnStatus("Pausiert.");
            dialog.RestartReplay();
            dialog.WaitOnStatus("Wiedergabe läuft.");

            dialog.WaitOnStatus("Wiedergabe beendet.");
            Assert.Equal("Chunk 2/2", dialog.GetPositionText());

            // Schrittmodus-Phase 3 (pausiert → Schritt zurück → fortsetzen): die Schleife setzt
            // an der durch den Schritt veränderten Position fort, wartet die aufgezeichnete
            // 3-s-Pause des zurückgenommenen Chunks erneut ab und wendet ihn erneut an.
            dialog.OpenRecording(pausePath);
            dialog.WaitOnStatus("Aufzeichnung geladen (E2E-Pause, 2 Chunks) — bereit.");
            dialog.StartReplay();
            dialog.WaitOnPosition("Chunk 1/2");
            dialog.PauseToggle();
            dialog.WaitOnStatus("Pausiert.");

            dialog.StepBackward();
            dialog.WaitOnPosition("Chunk 0/2");
            Assert.Equal(-1, dialog.GetSelectsSourceEntryIndex());

            dialog.PauseToggle();
            dialog.WaitOnStatus("Wiedergabe läuft.");
            dialog.WaitOnPosition("Chunk 1/2");
            // Während der erneut abgewarteten 3-s-Pause (unpausiert laufende Wiedergabe)
            // müssen beide Schritt-Buttons deaktiviert sein.
            Assert.False(dialog.IsButtonEnabled("StepForward"),
                "während unpausierter Wiedergabe muss 'Schritt vor' deaktiviert sein");
            Assert.False(dialog.IsButtonEnabled("StepBack"),
                "während unpausierter Wiedergabe muss 'Schritt zurück' deaktiviert sein");

            dialog.WaitOnStatus("Wiedergabe beendet.");
            Assert.Equal("Chunk 2/2", dialog.GetPositionText());

            // Maximale Geschwindigkeit: Inter-Chunk-Pausen der Aufzeichnung ignorieren.
            dialog.SetTimelapseThreshold("0");
            dialog.OpenRecording(path);
            dialog.WaitOnStatus("Aufzeichnung geladen (E2E-Diagnose, 2 Chunks) — bereit.");

            Assert.False(dialog.IsErrorVisible(),
                "Nach erfolgreichem Laden darf kein Fehlerbanner mehr sichtbar sein.");
            Assert.Equal(2, dialog.GetSourceEntriesCount());
            // Die Quell-Ansicht macht ANSI-Steuersequenzen sichtbar (ESC als ␛).
            Assert.Contains("␛[31m", dialog.GetSourceEntryText(0), StringComparison.Ordinal);
            Assert.Contains("E2E-Replay-Chunk-1", dialog.GetSourceEntryText(0), StringComparison.Ordinal);
            Assert.Contains("␛[0m", dialog.GetSourceEntryText(0), StringComparison.Ordinal);

            dialog.StartReplay();
            dialog.WaitOnStatus("Wiedergabe beendet.");
            Assert.Equal("Chunk 2/2", dialog.GetPositionText());

            // Geometrie-Phase: die Wiedergabe rendert in der aufgezeichneten Geometrie statt
            // der Fenstergröße. Funktionaler Nachweis über den ReplayTerminalScrollViewer —
            // der gerenderte Terminalinhalt ist per UI-Automation nicht lesbar; ein
            // HorizontallyScrollable == true impliziert ExtentWidth > ViewportWidth und damit
            // einen nicht auf die Fensterbreite verkleinerten Buffer.
            dialog.OpenRecording(widePath);
            dialog.WaitOnStatus("Aufzeichnung geladen (E2E-Breit, 1 Chunks) — bereit.");
            Assert.Equal("Aufzeichnung: 220×50", dialog.GetGeometryText());
            dialog.WaitOnHorizontalScrollDue();
            Assert.True(dialog.IsHorizontalScrollbar(),
                "eine 220×50-Aufzeichnung übersteigt die Dialogbreite — der Inhalt muss horizontal scrollbar sein");
            Assert.True(dialog.GetHorizontalViewSize() < 100,
                "der sichtbare Ausschnitt muss kleiner als der Gesamtinhalt sein");

            dialog.SetHorizontalScrollPercent(50);
            var horizontalScrolled = false;
            var scrollDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (DateTime.UtcNow < scrollDeadline)
            {
                if (dialog.GetHorizontalScrollPercent() > 0)
                {
                    horizontalScrolled = true;
                    break;
                }

                Thread.Sleep(200);
            }
            Assert.True(horizontalScrolled,
                "SetScrollPercent muss den horizontalen Offset des ReplayTerminal-ScrollViewers verschieben");

            // Tastatur-Scrolling: das Replay-Terminal besitzt keinen Eingabekanal — die
            // Ende-Taste darf vom VT100-Encoder nicht verschluckt werden, sondern muss zum
            // umschließenden ScrollViewer bubbeln (horizontal ans rechte Ende scrollen).
            var percentBeforeKey = dialog.GetHorizontalScrollPercent();
            dialog.PressKeyInReplayTerminal(VirtualKeyShort.END);
            var keyboardScrolled = false;
            var keyboardDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (DateTime.UtcNow < keyboardDeadline)
            {
                if (dialog.GetHorizontalScrollPercent() > percentBeforeKey)
                {
                    keyboardScrolled = true;
                    break;
                }

                Thread.Sleep(200);
            }
            Assert.True(keyboardScrolled,
                "die Ende-Taste im fokussierten Replay-Terminal muss den ScrollViewer horizontal weiter nach rechts scrollen");

            // Schmale Aufzeichnung (60×20): passt in den Viewport — kein horizontaler Scrollbalken.
            // Das Polling auf „nicht scrollbar" überbrückt die asynchrone Extent-Aktualisierung
            // nach dem Session-Wechsel (die zuvor breite Extent darf nicht transient greifen).
            dialog.OpenRecording(narrowPath);
            dialog.WaitOnStatus("Aufzeichnung geladen (E2E-Schmal, 1 Chunks) — bereit.");
            Assert.Equal("Aufzeichnung: 60×20", dialog.GetGeometryText());
            var narrowNotScrollbar = false;
            var narrowDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (DateTime.UtcNow < narrowDeadline)
            {
                if (!dialog.IsHorizontalScrollbar())
                {
                    narrowNotScrollbar = true;
                    break;
                }

                Thread.Sleep(200);
            }
            Assert.True(narrowNotScrollbar,
                "eine 60×20-Aufzeichnung passt in den Viewport — es darf keine horizontale Scrollbar geben");

            dialog.Close();
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
            TryCloseConsoleTestwindow(dialog, settings);

            if (File.Exists(path))
                File.Delete(path);
            if (File.Exists(pausePath))
                File.Delete(pausePath);
            if (File.Exists(stepPath))
                File.Delete(stepPath);
            if (File.Exists(defectPath))
                File.Delete(defectPath);
            if (File.Exists(widePath))
                File.Delete(widePath);
            if (File.Exists(narrowPath))
                File.Delete(narrowPath);
        }
    }

    /// <summary>Best-Effort-Schließen des Konsolentestfensters und der Einstellungsansicht —
    /// Fehler beim Schließen (z. B. bereits geschlossen) werden bewusst ignoriert.</summary>
    private static void TryCloseConsoleTestwindow(ConsoleTestDialogView? dialog, SettingsView? settings)
    {
        try
        {
            if (dialog is not null && dialog.IsVisible)
                dialog.Close();
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

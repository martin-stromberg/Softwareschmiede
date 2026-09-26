using System.Text;
using FlaUI.Core.AutomationElements;
using Softwareschmiede.Infrastructure.Terminal;
using Softwareschmiede.Tests.E2E.Views;
using Softwareschmiede.Tests.E2E.Views.Dialogs;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Abdeckung des Konsolentestfensters (CLI-Replay-Diagnose): Öffnen über die Einstellungen,
/// Laden von synthetisch erzeugten .clireplay-Dateien über den nativen Öffnen-Dialog, Zeitraffer-,
/// Pause-/Fortsetzen- und Neustart-Steuerung sowie Nachweis der Quell-Chunk-Liste (ohne
/// ConPTY-Abhängigkeit — die Aufzeichnungen werden direkt über <see cref="CliReplayAufzeichnungStore"/>
/// erzeugt).
/// </summary>
public partial class End2EndTest
{
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
        var defektPfad = Path.Combine(Path.GetTempPath(), $"softwareschmiede_e2e_{Guid.NewGuid():N}.clireplay");
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
            if (File.Exists(defektPfad))
                File.Delete(defektPfad);
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

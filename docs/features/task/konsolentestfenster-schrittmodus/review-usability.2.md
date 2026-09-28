# Usability-Review

## Ergebnis

**Status:** Keine Befunde

## Geprüfte Interaktionen

Liste der aus der Anforderung geprüften Benutzerinteraktionen:

- Aufzeichnung öffnen (`.clireplay` über nativen Datei-Dialog mit Filter `*.clireplay`) → unauffällig — keine interne Kennung nötig, Auswahl per Dateiname.
- Zeitgesteuerte Wiedergabe starten / pausieren / fortsetzen (`Abspielen`, `Pausieren/Fortsetzen`) → unauffällig — bestehende Bedienelemente unverändert erreichbar.
- Einzelschritt vorwärts ohne gestartete Wiedergabe (ab Position 0 direkt nach dem Laden) → unauffällig — `Schritt vor` ist bei geladener Aufzeichnung sofort aktiv (`AktuellerChunkIndex < QuellEintraege.Count`), ToolTip „Nächsten aufgezeichneten Chunk anwenden" erklärt die Aktion.
- Einzelschritt vorwärts/rückwärts im pausierten Zustand → unauffällig — beide Buttons nur aktiv, wenn die Wiedergabe nicht unpausiert läuft (`!IstWiedergabeAktiv || IstPausiert`); Deaktivierung während des Laufs ist über den Statustext „Wiedergabe läuft." nachvollziehbar.
- Einzelschritt rückwärts (Zustand vor dem zuletzt angewendeten Chunk wiederherstellen) → unauffällig — deterministischer Buffer-Neuaufbau; an Position 0 deaktiviert; Statustext „Schritt zurück — Chunk n/y zurückgenommen." benennt den zurückgenommenen Chunk in derselben 1-basierten Nummerierung wie die `#`-Spalte.
- Positions- und Statusanzeige verfolgen (`Chunk x/y`, `AktuellerQuellEintrag`-Selektion + `ScrollIntoView`) → unauffällig — „Chunk n/y" selektiert Zeile `#n`; erkennbar, welcher Chunk zuletzt angewendet wurde (Selektion); an Position 0 korrekt keine Selektion.
- Übergang pausiert → Einzelschritte → fortsetzen → unauffällig — die Wiedergabe-Schleife liest die Position aus `_abgespielteChunks.Count` und setzt an der durch Schritte veränderten Position fort (Positions-Re-Check in `WiedergabeLoopAsync`).
- Ende erreichen per Einzelschritt → unauffällig — `Schritt vor` deaktiviert, Status „Wiedergabe beendet.", `Schritt zurück` bleibt aktiv; danach „Abspielen" setzt an der Schrittposition fort bzw. startet am Ende frisch ab 0.
- Rückweg zu Position 0 aus reinem Schrittmodus (Befund aus Runde 1) → unauffällig — gelöst über erweitertes `WiedergabeNeustartenCommand`-CanExecute (`IstWiedergabeAktiv || AktuellerChunkIndex > 0`); zusätzlich ist Position 0 jederzeit deterministisch über wiederholtes „Schritt zurück" erreichbar. Nebenbeobachtung (kein Befund): „Neu starten" startet die Wiedergabe sofort statt nur auf Position 0 zu halten — deckt sich aber mit Beschriftung und ToolTip („…sofort wieder ab Position 0 abspielen").
- Werkzeugleiste bei begrenzter Fensterbreite (Befund aus Runde 1) → unauffällig — `WrapPanel` statt `StackPanel`: alle Elemente (inkl. `PositionsText`, `Schließen`) bleiben durch Umbruch erreichbar statt abgeschnitten zu werden. Nebenbeobachtung (kein Befund): beim Umbruch können Label/TextBox der Zeitraffer-Schwelle auf getrennte Zeilen fallen — kosmetisch, keine Funktionseinschränkung.
- Beschriftung und Zählweise (Befund aus Runde 1) → unauffällig — `#`-Spalte jetzt 1-basiert und konsistent zu `PositionsText` und den neuen Statustexten; beide Schritt-Buttons tragen `AutomationProperties.Name` (`SchrittVor`/`SchrittZurueck`) und erklärende ToolTips; Reihenfolge „Schritt zurück" vor „Schritt vor" entspricht der üblichen Zurück/Vor-Konvention.

## Geprüfte Dateien

Liste aller geprüften UI-Dateien:
- `src/Softwareschmiede.App/Views/KonsolenTestDialog.xaml`
- `src/Softwareschmiede.App/Views/KonsolenTestDialog.xaml.cs` (unverändert ggü. Basis; Selektions-/Scroll-Mechanik geprüft)
- `src/Softwareschmiede.App/ViewModels/KonsolenTestViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/CliChunkAnzeigeEintrag.cs`
- `src/Softwareschmiede/Infrastructure/Terminal/TerminalReplaySession.cs` (nicht UI, geprüft zur Beurteilung der sichtbaren Schritt-/Ende-Semantik)
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/KonsolenTestDialogView.cs` + `src/Softwareschmiede.Tests/E2E/E2E_KonsolenTestfenster.cs` (Test-Sicht; bestätigt das beabsichtigte Bedienverhalten)

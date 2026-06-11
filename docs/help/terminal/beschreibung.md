← [Zurück zur Übersicht](index.md)

# CLI-Fenster-Einbettung — Beschreibung

## Zweck

Die CLI-Fenster-Einbettung ermöglicht die direkte, interaktive Bedienung von Claude CLI oder GitHub Copilot CLI innerhalb der Softwareschmiede. Das native Terminalfenster des CLI-Tools wird physisch in die WPF-Aufgabendetailansicht eingebettet — der Anwender arbeitet mit dem echten CLI, ohne ein separates Fenster öffnen zu müssen.

## Funktionsweise

Der `ProcessWindowHost` (von `HwndHost` abgeleitet) erstellt ein natives Win32-Hostfenster innerhalb der WPF-Ansicht. Sobald der CLI-Prozess gestartet ist und sein Hauptfenster-Handle verfügbar ist, wird via `SetParent` das CLI-Fenster als Kind-Fenster eingebettet. Titelleiste und Rahmen des eingebetteten Fensters werden durch `SetWindowLong` entfernt.

### Prozess-Lifecycle

1. `KiAusfuehrungsService.StartCliAsync` startet den CLI-Prozess über `ProcessStartInfo` des KI-Plugins.
2. Das `CliProzessGestartet`-Event des `TaskDetailViewModel` gibt das `Process`-Objekt weiter.
3. `TaskDetailView.xaml.cs` setzt `ProcessWindowHost.EmbeddedHandle` auf `Process.MainWindowHandle`.
4. `ProcessWindowHost.EmbedWindow` ruft `SetParent` auf und passt die Fenstergröße an.

### Größenanpassung

Das eingebettete Fenster füllt immer den gesamten verfügbaren Platz aus. Bei Größenänderungen des WPF-Containers reagiert `OnRenderSizeChanged` mit einem `SetWindowPos`-Aufruf.

## Beispiele

- Claude CLI direkt in der Aufgabenansicht bedienen, ohne das Fenster zu wechseln.
- GitHub Copilot CLI für explorative Aufgaben starten und das Ergebnis direkt im Kontext der Aufgabe sehen.

## Einschränkungen

- Die Einbettung funktioniert nur auf Windows; auf anderen Plattformen scheitert `SetParent`.
- Falls `Process.MainWindowHandle` zum Einbettungszeitpunkt noch `IntPtr.Zero` ist (CLI noch nicht vollständig gestartet), bleibt der Host leer. Das Handle kann erneut gesetzt werden, sobald es verfügbar ist.
- Das eingebettete Fenster reagiert nicht auf WPF-Themen (Dark Mode): es behält sein natives Erscheinungsbild.
- Multi-Monitor-Layouts können in seltenen Fällen die Handle-Ermittlung beeinflussen.

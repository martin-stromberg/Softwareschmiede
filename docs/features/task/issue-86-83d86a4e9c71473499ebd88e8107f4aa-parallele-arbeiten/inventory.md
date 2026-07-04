# Bestandsaufnahme: Parallele CLI-Ausführungen — Analyse der bestehenden Terminal-Architektur

Diese Bestandsaufnahme analysiert die vorhandene Implementierung der Terminal- und CLI-Prozessausführung bezogen auf die Anforderung, parallele CLI-Prozesse stabil laufen zu lassen, auch wenn die Aufgabenseite nicht angezeigt wird.

## Zusammenfassung

- **TerminalControl** ist derzeit das zentrale UI-Control für die Terminal-Anzeige. Es verwaltet seinen eigenen `ReadLoopAsync`-Lesevorgang und bricht diesen ab, wenn das Control aus dem visuellen Baum entfernt wird (Unloaded-Event, Zeilen 51–56).
  
- **PseudoConsoleSession** ist eine Schnittstelle zwischen Prozess, Streams und Lifecycle-Management. Sie verwaltet die Input-/Output-Pipes und den Runtime-Status (ob läuft/wartet), wird aber nicht als Lifecycle-Owner der ReadLoop betrachtet.

- **TerminalBuffer** ist ein Thread-sicherer Zustandsbuffer für die Terminal-Ausgabe (Cursor, Zellen, Farben). Er wird von TerminalControl verwaltet und bei Session-Wechsel wiederverwendet.

- **TaskDetailViewModel** kapselt die Geschäftslogik für die Aufgabenanzeige. Es empfängt `PseudoConsoleSessionGestartet`-Events aus dem KiAusfuehrungsService und leitet die Session an TaskDetailView weiter.

- **TaskDetailView** bindet die Session an TerminalControl und registriert sich per DataContextChanged, da WPF die View-Instanz nicht neu erstellt, wenn der DataContext zu einer anderen TaskDetailViewModel-Instanz desselben Typs wechselt.

- **KiAusfuehrungsService** ist der zentrale Singleton-Service für CLI-Prozess-Verwaltung. Er speichert laufende Prozesse in `_handles` (ConcurrentDictionary) und gibt die `PseudoConsoleSession` via `GetPseudoConsoleSession()` an den ViewModel zurück.

- **Probleme mit dem aktuellen Ansatz:**
  - Die ReadLoop ist an den Control-Lifecycle gebunden; wenn der Anwender zu einer anderen Aufgabe navigiert, wird die ReadLoop abgebrochen.
  - Die Pipe-Buffer-Verwaltung stoppt; wenn mehrere Prozesse parallel laufen, können die Output-Pipes für nicht angezeigten Aufgaben blockiert werden.
  - Der Prozess läuft zwar weiter (im OS), produziert aber effektiv keine verwertbare Ausgabe, da niemand sie liest.

## Details

- [Datenmodell](inventory/models.md) — TerminalBuffer, CliRuntimeStatusChangedEventArgs, CliProcessHandle
- [Logik](inventory/logic.md) — TerminalControl, PseudoConsoleSession, TaskDetailViewModel, KiAusfuehrungsService
- [Enums](inventory/enums.md) — CliRuntimeStatus, CliProcessStatus
- [Tests](inventory/tests.md) — Bestehende TerminalControlTests und Hilfsmethoden

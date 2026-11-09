# Release Notes

## Important Notes Before Update

- There are no special notices.

## What's New

- Fixed: the task tile in the task list (sidebar menu and dashboard) could show "✓ Bereit" while the CLI execution was still running — the tile status now follows the live state of the CLI session and consistently shows "▶ Läuft" / "⏸ Wartet" in sync with the task footer.
- Missing run data for a running CLI process is now restored automatically (self-healing), so the status stays correct after restarts and reused sessions.
- New error message when stopping a task fails: if the CLI process keeps running, the app reports "Der CLI-Prozess konnte nicht beendet werden und läuft weiter. Bitte erneut versuchen." instead of wrongly showing the task as ready.

## Wichtige Hinweise vor dem Update

- Es gibt keine besonderen Hinweise.

## Neuerungen

- Behoben: Die Aufgabenkachel in der Aufgabenliste (Menü-Seitenleiste und Dashboard) konnte „✓ Bereit" anzeigen, obwohl die CLI-Ausführung noch lief — der Kachel-Status folgt jetzt dem Live-Zustand der CLI-Session und zeigt konsistent zur Aufgaben-Fußzeile „▶ Läuft" bzw. „⏸ Wartet".
- Fehlende Laufdaten zu einem laufenden CLI-Prozess werden jetzt selbstheilend ergänzt, sodass der Status auch nach Neustarts und wiederverwendeten Sitzungen korrekt bleibt.
- Neue Fehlermeldung beim Stoppen: Läuft der CLI-Prozess nach dem Stoppen weiter, meldet die App „Der CLI-Prozess konnte nicht beendet werden und läuft weiter. Bitte erneut versuchen." statt die Aufgabe fälschlich als bereit anzuzeigen.

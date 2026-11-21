# Release Notes

## Important Notes Before Update

- There are no special notices.

## What's New

- Fixed: the SCM execution limit now frees an occupied slot as soon as a task's CLI session is verifiably waiting at the input prompt and the task has no open event — the next queued event then starts event-driven instead of only at the next poll; a finished, prompt-waiting task no longer blocks its slot until process exit or a manual pause, which previously let the queue starve at per-plugin limit 1.

## Wichtige Hinweise vor dem Update

- Es gibt keine besonderen Hinweise.

## Neuerungen

- Fehlerkorrektur: Die SCM-Ausführungsbegrenzung gibt einen belegten Platz jetzt frei, sobald die CLI-Sitzung einer Aufgabe nachweislich am Eingabeprompt wartet und kein offenes Ereignis der Aufgabe existiert — das nächste wartende Ereignis startet anschließend ereignisgesteuert statt erst beim nächsten Poll; eine fertige, am Prompt wartende Aufgabe blockiert ihren Platz damit nicht mehr bis zum Prozessende oder einer manuellen Pause, wodurch die Queue bisher bei Plugin-Limit 1 verhungern konnte.

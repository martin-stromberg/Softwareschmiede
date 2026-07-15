# Plan

1. GitHub-Actions-Testschritt auf stabile Kategorien filtern.
2. `Category=E2E` ausschliessen, damit WPF/FlaUI-UI-Automation nicht mehr PR-Gate ist.
3. `Category=ConPTY` ausschliessen, da echte ConPTY-Umgebungstests ebenfalls von der Runner-Session abhaengen.
4. Kommentar im Workflow aktualisieren, damit klar bleibt, dass E2E/ConPTY lokal waehrend der Entwicklung laufen.
5. Lokalen Lauf mit demselben Filter ausfuehren.

## Offene Punkte

Keine.

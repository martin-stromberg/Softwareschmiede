# Testplan – Kontextsteuerung bei Folgeanweisungen

## Ziel

Absicherung der Modi **Kontext mitgeben / Kontext ignorieren / Kontext neu beginnen** inklusive UI-Guardrails, Service-Fehlerpfade und Laufzeitintegration.

## Geplante Testergänzungen

1. `src/Softwareschmiede.Tests/Components/Pages/Aufgaben/AufgabeDetailFolgePromptTests.cs`
   - Kontextmodus-Wechsel setzt Neu-beginnen-Bestätigung zurück.
   - Ungültige Kontextmodus-Werte verändern den Zustand nicht.
   - Folgeanweisung mit bestätigtem „Kontext neu beginnen“ startet mit korrektem Modus.

2. `src/Softwareschmiede.Tests/Application/Services/EntwicklungsprozessServiceTests.cs`
   - `KontextMitgeben` ohne vorhandene Kontextdatei verwendet nur User-Prompt.
   - Soft-Limit-Komprimierung ohne Pflichtabschnitte bricht mit Fehler ab und schreibt Preflight-Eintrag.
   - Fallback auf `.bak` bei Lesefehler der Haupt-Kontextdatei.
   - Plugin-Fehler bei Folgeanweisung schreibt Kontextverlauf mit Fehlerstatus.

3. `src/Softwareschmiede.Tests/Application/Services/KiAusfuehrungsServiceTests.cs`
   - `StartKiLauf` ruft `onStarted`/`onCompleted` im Erfolgsfall korrekt auf.
   - Doppeltstart derselben Aufgabe wird blockiert.
   - Hintergrundfehler markieren Lauf als Fehler und schreiben Fehlerzeile in den Puffer.

## Abnahmekriterien

- Alle neuen Tests laufen mit den bestehenden `dotnet test`-Kommandos.
- Modusspezifische Semantik ist in UI- und Service-Schicht nachweisbar.
- Fehler- und Randpfade erzeugen erwartete Status-/Kontextartefakte.

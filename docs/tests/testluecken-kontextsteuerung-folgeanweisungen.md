# Testlücken – Kontextsteuerung bei Folgeanweisungen

## Analysebasis

- Fokus: Kontextmodi **mitgeben / ignorieren / neu beginnen** entlang UI, Service und Integrationsfluss.
- Bestehende Tests decken zentrale Happy-Paths ab, lassen aber Rand- und Fehlerpfade offen.

## Priorisierte Testlücken

| Priorität | Datei | Szenario (nicht getestet) | Begründung |
|---|---|---|---|
| P1 | `src/Softwareschmiede.Tests/Application/Services/KiAusfuehrungsServiceTests.cs` | `StartKiLauf` startet Session, liefert Output, ruft `onStarted`/`onCompleted` korrekt auf | Zentraler Integrationsknoten für Folgeanweisungs-Ausführung |
| P1 | `src/Softwareschmiede.Tests/Application/Services/KiAusfuehrungsServiceTests.cs` | Doppeltstart wird blockiert (`IsRunning == true`) | Schutz vor konkurrierenden Folgeanweisungen |
| P1 | `src/Softwareschmiede.Tests/Application/Services/KiAusfuehrungsServiceTests.cs` | Fehler-/Cancel-Pfade schreiben Status/Fehler und beenden Session sauber | Robustheit des Hintergrundlaufs |
| P1 | `src/Softwareschmiede.Tests/Components/Pages/Aufgaben/AufgabeDetailFolgePromptTests.cs` | `FolgeKontextmodusGeaendert` setzt Bestätigung zurück, wenn Modus von „neu beginnen“ wegwechselt | Sicherheitsrelevanter UI-Guard |
| P1 | `src/Softwareschmiede.Tests/Components/Pages/Aufgaben/AufgabeDetailFolgePromptTests.cs` | Ungültiger Auswahlwert für Kontextmodus verändert Zustand nicht | UI-Fehlerpfad |
| P2 | `src/Softwareschmiede.Tests/Application/Services/EntwicklungsprozessServiceTests.cs` | `KontextMitgeben` bei fehlender Kontextdatei verwendet nur User-Prompt | Kritische Randbedingung |
| P2 | `src/Softwareschmiede.Tests/Application/Services/EntwicklungsprozessServiceTests.cs` | Komprimierung ohne Pflichtabschnitte bricht mit Fehler ab und schreibt Preflight-Kontexteintrag | Wichtiger Fehlerpfad vor KI-Start |
| P2 | `src/Softwareschmiede.Tests/Application/Services/EntwicklungsprozessServiceTests.cs` | Fallback auf `.bak` bei Lesefehlern wird genutzt | Persistenz-Resilienz |

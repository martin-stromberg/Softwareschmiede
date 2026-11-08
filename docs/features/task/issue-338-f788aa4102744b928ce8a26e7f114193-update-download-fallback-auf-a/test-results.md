# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

## Fehlgeschlagene Tests

Keine.

## Zusammenfassung

- Gesamt: 2201
- Bestanden: 2200
- Fehlgeschlagen: 0
- Übersprungen: 1

Übersprungen: `ArbeitsverzeichnisOeffnenServiceTests.Oeffne_AufNichtWindows_WirftPlatformNotSupportedException`
(plattformbedingter Skip — Test läuft nur auf Nicht-Windows-Systemen).

Hinweise zum Lauf (zweiter Durchlauf):

- Build vor dem Testlauf: `dotnet build Softwareschmiede.slnx` — erfolgreich (0 Warnungen, 0 Fehler).
- Ausgeführte Lane: `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface" --logger "console;verbosity=normal" --collect:"XPlat Code Coverage"` mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`.
- Die `Category=OsInterface`-Lane (FlaUI-E2E, ConPTY) wurde **nicht** ausgeführt — sie benötigt einen interaktiven Desktop, der in dieser Sandbox nicht zuverlässig verfügbar ist.
- Gesamtdauer des Testlaufs: ca. 3,2 Minuten.

## Testabdeckung

**Abdeckung:** 27,6 % (gesamt, über alle vom Testlauf geladenen Assemblys; niedrig, weil die OsInterface-/E2E-Lane nicht lief und große Teile des WPF-App-Codes nur durch E2E-Tests abgedeckt werden)

Branch-relevante geänderte Dateien (Zeilenabdeckung, Union über alle Klassen-Records der Datei):

| Datei | Abdeckung |
|-------|-----------|
| `src/Softwareschmiede/Infrastructure/Services/Updates/UpdatePackageService.cs` | 95,0 % (134/141 Zeilen) |
| `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` | 94,9 % (37/39 Zeilen) |
| `src/Softwareschmiede/Infrastructure/Services/Updates/GitHubReleaseClient.cs` | 93,2 % (150/161 Zeilen) |
| `src/Softwareschmiede.App/Services/Testing/UpdateFixtureHttpMessageHandler.cs` | 76,6 % (111/145 Zeilen) |

## Fehlende Tests

Keine — für alle vier geänderten Produktivdateien existieren zugehörige Tests und die Abdeckung liegt über 0 %:

- `UpdateModels.cs` — indirekt über `UpdateServiceTests*` u. a. abgedeckt (94,9 %)
- `GitHubReleaseClient.cs` — `GitHubReleaseClientTests.cs`, `GitHubReleaseClientTests_Filters.cs`, `GitHubReleaseClientTests_Pagination.cs` (93,2 %)
- `UpdatePackageService.cs` — `UpdatePackageServiceTests.cs` (95,0 %)
- `UpdateFixtureHttpMessageHandler.cs` — `UpdateFixtureHttpMessageHandlerTests.cs` (76,6 %)

Anmerkung: Es gibt zahlreiche weitere Dateien mit 0 % Abdeckung im Report (v. a. `Softwareschmiede.App`-UI-/Plugin-Code). Diese sind nicht Teil dieser Änderung — sie werden überwiegend durch die hier nicht ausgeführte `OsInterface`-/E2E-Lane abgedeckt.

Quelle: `Coverage-Daten` (`src/Softwareschmiede.Tests/TestResults/b0caa0fb-ac67-4c6f-8e62-224a8cbdd762/coverage.cobertura.xml`)

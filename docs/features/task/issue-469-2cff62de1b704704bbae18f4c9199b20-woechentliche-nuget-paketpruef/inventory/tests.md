# Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** 2026-11-23, ~18:50–19:10 Uhr +0100 (CET)
- **Branch und Commit-ID:** `task/issue-469-2cff62de1b704704bbae18f4c9199b20-woechentliche-nuget-paketpruef`
  @ `cef41f6e619e94f5de7dffb9c70a68b2d3c146fe` (Basis: `staging`)
- **Uncommittete Änderungen im getesteten Stand:** keine Änderungen an getrackten Dateien;
  einziges untracked Verzeichnis ist der Ordner `docs/features/task/issue-469-…/`
  dieser Inventory-Artefakte (ohne Einfluss auf Build/Tests)
- **Testumgebung und Runtime-/SDK-Versionen:** Windows, .NET SDK 11.0.100
  (zusätzlich installiert: 10.0.404), `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` für alle
  `dotnet test`-Läufe gesetzt (Sandbox-Vorgabe laut CLAUDE.md)
- **Ermittelte Testsuiten und Quellen der Testbefehle:** CLAUDE.md sowie
  `.github/workflows/pr-staging-ci.yml` / `staging-ci.yml` / `release.yml` — stabile Lane
  `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"`,
  separate Lane `Category=OsInterface` (E2E/ConPTY/OS-abhängig), dazu die zweite Suite
  `Softwareschmiede.IntegrationTests`. Voraussetzung laut CLAUDE.md: voller Build vor Tests.

## Build-Nachweis

| Befehl | Arbeitsverzeichnis | Ergebnis |
|--------|--------------------|----------|
| `dotnet build Softwareschmiede.slnx` | Repo-Root | Erfolg, 0 Warnungen, 0 Fehler (~11,6 s) — kein MSB3026/MSB3027-Dateilock |

## Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|---------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| 1 | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"` | Repo-Root | 0 | 2800 | 0 | 3 | [dotnet-test-stable-lane.log](test-results/dotnet-test-stable-lane.log) |
| 2 | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj` (ohne Filter, alle Kategorien) | Repo-Root | 0 | 436 | 0 | 0 | [dotnet-test-integrationtests.log](test-results/dotnet-test-integrationtests.log) |

Lauf 1 Dauer: ~3 m 50 s. Lauf 2 Dauer: ~1 m 5 s.

Die 3 Übersprungenen in Lauf 1 sind OS-bedingt ausgelegte Tests, keine Fehler:

- `Softwareschmiede.Tests.Infrastructure.Services.SystemShutdownServiceTests.ResolveShutdownCommand_Linux_RemainsUnchanged`
- `Softwareschmiede.Tests.Infrastructure.Services.SystemShutdownServiceTests.ResolveShutdownCommand_MacOS_RemainsUnchanged`
- `Softwareschmiede.Tests.Application.Services.WorkingDirectoryOpenServiceTests.Open_OnNotWindows_ThrowsPlatformNotSupportedException`

## Nachgewiesene bestehende Testfehler

Keine — beide Läufe endeten mit 0 Fehlgeschlagenen. Für spätere Arbeiten gilt damit:
Jeder Testfehler nach Paketupdates in den gelaufenen Lanes ist ein **neuer** Befund,
kein „preexisting failure". Für die nicht ausgeführte `OsInterface`-Lane kann keine
Vorher-Aussage getroffen werden (siehe unten).

## Testlücken und Ausführungsprobleme

- **Lane `Category=OsInterface` nicht ausgeführt** (`Softwareschmiede.Tests` und
  `Softwareschmiede.IntegrationTests`): enthält die FlaUI-E2E-Tests, die reale
  App-Fenster starten, sowie ConPTY-Tests, die in dieser Agent-Sandbox nachweislich
  nicht isoliert laufen (dokumentiert in CLAUDE.md). Der ConPTY-Anteil wurde in den
  gelaufenen Läufen ohnehin durch den Filter ausgeschlossen; für die gesamte
  OsInterface-Lane liegt kein Vorher-Nachweis vor. Vor einem Versions-PR, der
  FlaUI-/E2E-relevante Pakete berührt, sollte diese Lane mindestens einmal in einer
  geeigneten Umgebung (oder in der PR-CI) verifiziert werden.
- Keine Build-, Setup- oder Infrastrukturfehler aufgetreten.
- Beim `dotnet test`-Lauf wurde — wie von CLAUDE.md gefordert — vollständig neu gebaut
  (kein `--no-build`).

## Testklassen für den Bereich „Paketprüfung"

Es existieren **keine** Tests, die NuGet-Referenzen, Paketversionen oder
`THIRD_PARTY_LICENSES.md` prüfen. Einzige Fundstelle mit csproj-Bezug:
`src/Softwareschmiede.IntegrationTests/Scripts/StartPs1IntegrationTests.cs:335`
(verwendet `src/Softwareschmiede/Softwareschmiede.csproj` als Pfad im getesteten
Start-Skript, keine Versionsaussage).

## Hilfsmethoden (update-relevante Test-Infrastruktur)

- `src/Softwareschmiede.Tests/Helpers/TestTimeProvider.cs` — nutzt
  `Microsoft.Extensions.TimeProvider.Testing` (Fakeable `TimeProvider`); wird per
  `Compile Include` auch in `Softwareschmiede.IntegrationTests` gelinkt.
- `src/Softwareschmiede.Tests/E2E/WpfTestBase.cs` u. a. E2E-Basisklassen —
  FlaUI-basiert, betroffen bei FlaUI-Updates (derzeit kein Update anstehend).
- `src/Softwareschmiede.Tests/Infrastructure/Testing/{TestCategories,FlakyAttribute,OsInterfaceAttribute}.cs`
  — Testkategorie-Konventionen, per Link auch in IntegrationTests eingebunden.

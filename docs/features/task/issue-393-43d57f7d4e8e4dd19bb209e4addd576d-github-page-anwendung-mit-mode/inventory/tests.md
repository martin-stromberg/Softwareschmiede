# Bestandsaufnahme: Tests und Test-Ausgangszustand

Die Anforderung betrifft ausschließlich Repository-Infrastruktur (statische Seite, Workflow, Dokumentation) — es existiert keine Testinfrastruktur für statisches HTML im Projekt, und die Anforderung sieht ausdrücklich keine automatisierten Tests für die Seite vor. Dennoch wurde vor jeder Änderung der projektübliche Test-Ausgangszustand erhoben, damit spätere Vergleiche einen dokumentierten Nachweis haben.

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** 2026-11-17, ca. 05:36–05:45 Uhr +0100 (CET)
- **Branch und Commit-ID:** `task/issue-393-43d57f7d4e8e4dd19bb209e4addd576d-github-page-anwendung-mit-mode` @ `73a36c409fd8b9384548c86bcb5c332c4e35368d` („chore: coverlet.collector 10.1.1 – NuGet-Prüfung 2026-KW47 (#402)", 2026-11-16 17:22:25 +0100)
- **Uncommittete Änderungen im getesteten Stand:** nur die neue, noch nicht getrackte Feature-Dokumentation unter `docs/features/task/issue-393-43d57f7d4e8e4dd19bb209e4addd576d-github-page-anwendung-mit-mode/` (`requirement.md` und die hier entstehenden Inventory-Dateien) — keine Produktivcode- oder Teständerungen.
- **Testumgebung und Runtime-/SDK-Versionen:** Windows, Git Bash; .NET SDK `10.0.403`, .NET Runtime `10.0.15` (aus dem xUnit-Adapter-Log); `dotnet build Softwareschmiede.slnx -c Debug` vorab erfolgreich (0 Warnungen, 0 Fehler, 11 s inkrementell).
- **Ermittelte Testsuiten und Quellen der Testbefehle:** Befehle aus `CLAUDE.md` („Testing") und den Workflow-Release-Gates in `release.yml` (Zeilen 103/108/114/120): stabile reguläre Lane `dotnet test <Projekt> --filter "Category!=OsInterface"` je Testprojekt (`Softwareschmiede.Tests`, `Softwareschmiede.IntegrationTests`); OS-Interface-Lane `Category=OsInterface` als separate, umgebungssensitive Suite (FlaUI-E2E, ConPTY, Clipboard, echte Prozessstarts). `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` wurde gesetzt (Sandbox-Vorgabe aus `CLAUDE.md`); innerhalb der regulären Lane tragen ConPTY-/E2E-Tests zusätzlich die Kategorie `OsInterface` und sind damit ohnehin ausgefiltert.
- **Build vor den Tests:** `dotnet build Softwareschmiede.slnx -c Debug` — erfolgreich (Nachweis: Konsolenausgabe; alle 14 Projekte gebaut, darunter `Softwareschmiede.App` und beide Testprojekte).

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| 1 | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface" --logger "trx;LogFileName=inventory-baseline-regular-tests.trx" --logger "console;verbosity=normal"` (an `tee` gepiped) | Repo-Wurzel | `dotnet test` meldete „Fehler beim Testlauf" (1 Fehlschlag); Pipeline-Exit-Code durch `tee` maskiert (0) | 2475 | 1 | 3 | [run1-regular-tests.log](test-results/run1-regular-tests.log), [inventory-baseline-regular-tests.trx](test-results/inventory-baseline-regular-tests.trx) |
| 2 | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --filter "Category!=OsInterface" --logger "trx;LogFileName=inventory-baseline-regular-integration.trx" --logger "console;verbosity=normal"` (an `tee` gepiped) | Repo-Wurzel | `dotnet test` meldete „Der Testlauf war erfolgreich."; Pipeline-Exit-Code 0 | 285 | 0 | 0 | [run2-regular-integration.log](test-results/run2-regular-integration.log), [inventory-baseline-regular-integration.trx](test-results/inventory-baseline-regular-integration.trx) |

Gesamt in der regulären Lane: 2764 Tests, davon 2760 bestanden, 1 fehlgeschlagen, 3 übersprungen. Laufzeiten: ca. 3,4 min (Lauf 1) bzw. ca. 39 s (Lauf 2).

### Nachgewiesene bestehende Testfehler

| Test-ID inkl. Testfall | Suite / Dateipfad | Fehlerbild / Fehlermeldung | Lauf und Nachweis |
|------------------------|-------------------|----------------------------|-------------------|
| `Softwareschmiede.Tests.App.Controls.TerminalControlTests.Selection_NewOutputOutsideSelection_KeepsSelectionAndCopyText` | `Softwareschmiede.Tests`, `src/Softwareschmiede.Tests/App/Controls/TerminalControlTests.KeyInput.cs:173` (über `src/Softwareschmiede.Tests/Helpers/WpfUnitTestHelpers.cs:31`, `RunOnSta`) | `System.Runtime.InteropServices.COMException : OpenClipboard fehlgeschlagen (0x800401D0 (CLIPBRD_E_CANT_OPEN))` — der Test greift auf die Windows-Zwischenablage zu; in dieser Sandbox ist der Clipboard-Zugriff nicht verfügbar/gesperrt. Laufzeit des Tests 15 s (Retry-Verhalten). | Lauf 1, [run1-regular-tests.log](test-results/run1-regular-tests.log) Zeilen 230–244, TRX `outcome="Failed"` (einziger `UnitTestResult`-Fehlschlag; weitere `outcome="Error"/"Warning"`-Einträge im TRX sind Run-Level-`RunInfo`-Meldungen wie der FluentAssertions-Lizenz-Hinweis, keine Testfehlschläge) |

Damit ist genau **ein** bereits vor der Umsetzung vorhandener Testfehler dokumentiert (Umgebungs-/Infrastrukturproblem der Sandbox, kein Anwendungsfehler; betrifft keine der von der Anforderung berührten Artefakte).

### Übersprungene Tests in der regulären Lane

| Test | Grund |
|------|-------|
| `Softwareschmiede.Tests.Infrastructure.Services.SystemShutdownServiceTests.ResolveShutdownCommand_Linux_BleibtUnveraendert` | Plattform-bedingt übersprungen (Nicht-Windows-Pfad, SkippableFact auf Windows-Host) |
| `Softwareschmiede.Tests.Infrastructure.Services.SystemShutdownServiceTests.ResolveShutdownCommand_MacOS_BleibtUnveraendert` | Plattform-bedingt übersprungen (Nicht-Windows-Pfad) |
| `Softwareschmiede.Tests.Application.Services.ArbeitsverzeichnisOeffnenServiceTests.Oeffne_AufNichtWindows_WirftPlatformNotSupportedException` | Plattform-bedingt übersprungen (Nicht-Windows-Pfad) |

### Testlücken und Ausführungsprobleme

- **`Category=OsInterface`-Lanes nicht ausgeführt** (beide Testprojekte): umgebungssensitive Suite (FlaUI-E2E-Fenstertests, ConPTY-Prozessstarts, Clipboard, echte Prozessstarts), die in dieser Agent-Sandbox bekanntermaßen nicht zuverlässig läuft (vgl. `CLAUDE.md`: ConPTY-Tests benötigen `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` bzw. time-outs ohne interaktive Desktop-Session; auch der o. g. Clipboard-Fehlschlag zeigt die eingeschränkte Umgebung). Da die Anforderung ausschließlich Repository-Infrastruktur ohne jede Anwendungscode-Änderung umfasst, wurde auf den Lauf verzichtet — der Zustand dieser Tests ist damit **unbekannt** und darf später nicht als „preexisting" bezeichnet werden. In der CI laufen diese Lanes ohnehin nur best-effort (`continue-on-error: true`, `release.yml` Zeilen 111–121).
- **Kein Fehlschlag-Nachweis außer dem einen Clipboard-Test:** Alle anderen Tests der regulären Lane sind nachweislich bestanden; für nicht ausgeführte OsInterface-Tests liegt weder ein Erfolgs- noch ein Fehlschlag-Nachweis vor.
- **Keine Infrastruktur-/Build-Fehler:** Build und beide regulären Testläufe liefen vollständig durch; keine abgebrochenen Suiten.

## Testklassen

Für den fachlichen Bereich der Anforderung (statische GitHub-Pages-Seite) existieren **keine** Testklassen — es gibt keine Testinfrastruktur für statisches HTML/Workflow-Artefakte. Die Anforderung sieht ebenfalls keine automatisierten Tests vor (Prüfung über Review und manuelle Sichtprüfung der deployed Page; optionaler, nicht geforderter Artefakt-Guard im Workflow analog zu „Verify publish output" in `.github/actions/build-and-package/action.yml`).

Die beiden bestehenden Testprojekte im Überblick:

### `Softwareschmiede.Tests`
- `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj` — `net10.0-windows10.0.17763.0`, `UseWPF`; xUnit 2.9.3 + xunit.runner.visualstudio 4.0.1, FluentAssertions, Moq, Xunit.SkippableFact, FlaUI.Core/UIA3 (E2E), coverlet.collector.
- Struktur: `App/` (ViewModels, Controls — u. a. `TerminalControlTests`), `Application/`, `Domain/`, `Infrastructure/`, `E2E/`, `ServiceIntegration/`, `Helpers/` (u. a. `WpfUnitTestHelpers.cs` mit `RunOnSta`), `PluginSymbolTests.cs`.
- In der regulären Lane nachgewiesen: 2479 Tests (2475 bestanden, 1 fehlgeschlagen, 3 übersprungen).

### `Softwareschmiede.IntegrationTests`
- `src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj` — `net10.0`; xUnit, EF-Core-SQLite-Integrationstests (`Infrastructure/`, `Services/`, `Scripts/`).
- In der regulären Lane nachgewiesen: 285 Tests (alle bestanden).

## Hilfsmethoden

### `Softwareschmiede.Tests.Helpers`
- `WpfUnitTestHelpers.RunOnSta` (`src/Softwareschmiede.Tests/Helpers/WpfUnitTestHelpers.cs:31`) — führt WPF-Abhängige Testlogik auf einem STA-Thread aus; der dokumentierte Clipboard-Fehlschlag entsteht innerhalb dieses Wrappers bei `OpenClipboard`.

Weitere projektweite Hilfsmittel (für diese Anforderung nicht relevant): `ScmDispatchFixture` (in die IntegrationTests gelinkt), FlaUI-View-Pattern unter `E2E/` (dokumentiert in `docs/help/e2e-test-infrastruktur/`).

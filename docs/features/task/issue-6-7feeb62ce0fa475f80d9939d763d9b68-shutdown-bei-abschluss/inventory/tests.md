# Tests — Shutdown bei Abschluss

## Test-Ausgangszustand

**Es wurde kein Build- oder Testlauf ausgeführt.** Begründung: Diese Bestandsaufnahme ist ausdrücklich als reine Lese-/Dokumentationsaufgabe definiert („kein Quellcode, kein Build, keine Tests"); der Ausgangszustand ist damit bewusst **unbekannt** und muss vor Beginn der Umsetzung noch erhoben werden. Es existieren entsprechend keine Artefakte unter `inventory/test-results/`.

Ermittelte Umgebung/Regeln für den späteren Baseline-Lauf:

- SDK: .NET `10.0.403` (einzige installierte SDK), Ziel `net10.0-windows10.0.17763.0` (App), `net10.0` (Libraries/Tests); .NET-Desktop-Workload erforderlich.
- Stabile reguläre Lane (CLAUDE.md / `pr-staging-ci.yml`):
  `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"`
- OS-sensitive Lane separat: `--filter "Category=OsInterface"` (E2E/FlaUI, ConPTY, Clipboard, echte Prozessstarts — `[OsInterface]`-Trait).
- Integrationstests: `dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --filter "Category!=OsInterface"`.
- Verbindliche Regeln (CLAUDE.md): immer vollständiger Build vor Tests, **nie `--no-build`**, `dotnet test` niemals im Hintergrund, in dieser Sandbox `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` inline setzen.
- Frameworks: xUnit 2.9.3, FluentAssertions 8.11.0, Moq 4.21.0, `Microsoft.Extensions.TimeProvider.Testing` (FakeTimeProvider), coverlet; FlaUI (UIA3) für E2E.

## Bestehende relevante Tests

### `AutoShutdownOrchestratorTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/AutoShutdownOrchestratorTests.cs`

Testlokale Fakes implementieren `IRunningAutomationStatusSource` (Event manuell auslösbar) und `ISystemShutdownService` (zählt `RequestShutdownAsync`-Aufrufe). Abgedeckte Fälle:

1. Aktiviert + Übergang 1→0 → genau ein `RequestShutdownAsync`.
2. Deaktiviert + Übergang 1→0 → kein Aufruf.
3. Wiederholter 0-Übergang → idempotent (Guard).
4. Positiver Count zwischendurch → Guard-Reset, späterer 0-Übergang löst erneut aus.
5. Final-Recheck: `GetRunningCount()` >0 zum Ausführzeitpunkt → kein Shutdown.
6. Exception im Shutdown-Service → gefangen, kein Propagieren.
7. `Dispose` → Abmeldung von `RunningCountChanged`, keine Reaktion mehr.

Nicht abgedeckt (existiert nicht): Countdown/Toleranz, Abbruch bei Statuswechsel, Force, Persistenz.

### `MainWindowViewModelTests`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests.cs`

Deckt u. a. ab: Refresh der `AktiveAufgabenListe` über `RunningCountChanged`/`LaufdatenChanged`/Timer, `LiveLaufStatus`-Mapping, Event-Abonnement/-Abmeldung, Gruppierung (`AufgabenListenModus`), Plugin-Mapping, Dispatcher-Marshalling über injiziertes `Action<Action>`. Konstruktor-Injection via `MainWindowOptionaleDienste` — Muster für neue VM-Abhängigkeiten in Tests.

### `KiAusfuehrungsServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/KiAusfuehrungsServiceTests.cs` (+ weitere `KiAusfuehrungsServiceTests_*.cs`-Teildateien)

Deckt Prozesslebenszyklus, `IsRunning`/`GetRunningCount`/`GetLaufendeAufgabenIds`, `GetLiveLaufStatus`, Prozessende/Session-Cleanup, `CliProcessStatusChanged` ab. Hilfsmittel: `TestKiAusfuehrungsServiceFactory`, `TestTerminalSessionFactory` — deterministische In-Memory-Sessions (`Process.GetCurrentProcess()`, `MemoryStream`), keine echten Prozessstarts.

### `AppEinstellungServiceTests_*`
Dateien: `src/Softwareschmiede.Tests/Application/Services/AppEinstellungServiceTests*.cs`

Muster-Tests für Key-Value-Get/Set, Defaults und Clamp-Verhalten — Vorlage für spätere `autoshutdown.*`-Schlüssel-Tests.

### `PromptZeitVersandServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/PromptZeitVersandServiceTests.cs`

Timer-Service-Tests mit `FakeTimeProvider` — Referenzmuster für deterministische Countdown-Tests.

### E2E-Infrastruktur
- `src/Softwareschmiede.Tests/E2E/WpfTestBase.cs`: `LaunchApp(umgebungsVariablen)` setzt prozessbezogene Env-Vars (`SOFTWARESCHMIEDE_TEST_DB_PATH`, `SOFTWARESCHMIEDE_UPDATE_TEST_CONFIG`); `SOFTWARESCHMIEDE_SKIP_WPF_E2E_TESTS=1` → `Xunit.SkipException`; `RestartAppPreservingDatabase` für Persistenz-/Neustart-Szenarien.
- `UpdateE2EFixture.cs`/`UpdateE2EProtokoll.cs`/`UpdateE2EEreignisse`: Szenario-JSON + JSONL-Ereignisprotokoll (u. a. `ShutdownRequested`) — Referenz für einen aufzeichnenden `ISystemShutdownService`-E2E-Fake.
- `MenuView`/E2E-Views: Helper für Seitenleisten-Elemente (aktive Aufgaben, Status-Glyphs, Versions-/Update-Controls) — keine Auto-Shutdown-Controls vorhanden.
- Trait-Kategorien: `[Trait("Category", "E2E")]` und `[OsInterface]`.

## Testlücken (für die Anforderung relevant)

- Keine Countdown-/Abbruch-Tests für den Orchestrator (Funktionalität fehlt).
- Keine Tests für fachliche „läuft"-Zählung (nur rohe Prozessanzahl testbar).
- Kein `ISystemShutdownService`-Fake/Recording-Service → OS-Shutdown in E2E aktuell nicht abgesichert.
- Keine ViewModel-/UI-Tests für Auto-Shutdown-Controls (existieren nicht).
- Keine Persistenz-Tests für `autoshutdown.*` (Schlüssel existieren nicht).

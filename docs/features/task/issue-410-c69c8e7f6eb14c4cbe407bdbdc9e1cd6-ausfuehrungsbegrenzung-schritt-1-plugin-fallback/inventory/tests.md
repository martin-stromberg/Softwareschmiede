# Tests – Ausgangszustand und relevante Testklassen

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (UTC): 2026-11-17, ~20:00–20:05 UTC
- Branch und Commit-ID: `task/issue-410-c69c8e7f6eb14c4cbe407bdbdc9e1cd6-ausfuehrungsbegrenzung-schritt-1-plugin-fallback` @ `369053a24935bfb376353fcaf502425e2d550cf5`
- Uncommittete Änderungen im getesteten Stand: ausschließlich Lifecycle-Artefakte unter `docs/features/…/` (keine Code-Änderungen)
- Testumgebung und Runtime-/SDK-Versionen: Windows-Sandbox, .NET SDK 10.0.403, Ziel `net10.0-windows10.0.17763.0`
- Ermittelte Testsuiten und Quellen der Testbefehle: stabile reguläre Lane laut `CLAUDE.md` (`dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"`); voller Solution-Build (`dotnet build Softwareschmiede.slnx`, 0 Warnungen, 0 Fehler) unmittelbar vor dem Lauf
- Hinweis zur Ausführung: Der `dotnet test`-Aufruf lief synchron in einer Shell; das Harness hat die Ausgabewarteschleife nach ~12 s Leerlauf in den Hintergrund delegiert, der Prozess lief jedoch ungestört zu Ende (in dieser Umgebung existiert kein `.claude`-Hook-Setup/Build-Lock — der in `CLAUDE.md` beschriebene Korruptionspfad tritt hier nicht auf). `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` war inline gesetzt.

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Baseline | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"` | Repo-Wurzel | 0 | 2580 | 0 | 3 | [baseline-regulaer.txt](test-results/baseline-regulaer.txt) |

### Nachgewiesene bestehende Testfehler

Keine — der Baseline-Lauf meldet 0 Fehlschläge.

### Testlücken und Ausführungsprobleme

- 3 übersprungene Tests (plattformbezogene `Skip`-Bedingungen, kein Fehlerbild): `ArbeitsverzeichnisOeffnenServiceTests.Oeffne_AufNichtWindows_WirftPlatformNotSupportedException`, `SystemShutdownServiceTests.ResolveShutdownCommand_Linux_BleibtUnveraendert`, `SystemShutdownServiceTests.ResolveShutdownCommand_MacOS_BleibtUnveraendert`.
- `Category=OsInterface`-Tests (E2E/FlaUI, ConPTY, Clipboard, reale Prozessstarts) sind in diesem Lauf nicht enthalten — separate Lane, nicht Teil der stabilen regulären Lane.

## Testklassen

### `PluginSelectionServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/PluginSelectionServiceTests.cs`

- `ResolveDevelopmentAutomationPluginAsync_ShouldUseExplicitSelection_WhenProvided` — explizite Auswahl gewinnt
- `GetAvailableKiPluginPrefixesAsync_ShouldReturnOnlyActivePlugins` — nur aktivierte Plugins
- `ResolveDevelopmentAutomationPluginAsync_ShouldUseStoredDefault_WhenNoExplicitSelection` — gespeicherter Default
- `ResolveDevelopmentAutomationPluginAsync_ShouldPreferCopilotProviderInFallback` — Copilot-Sortier-Fallback
- `ResolveDevelopmentAutomationPluginAsync_ShouldUseDefaultResolver_WhenAvailableListIsEmpty` — Default-Resolver bei leerer Liste
- `TestResolvePluginWithProjectScope_UsesProjectDefault` — Projekt-Default gewinnt
- `TestResolvePluginWithProjectScope_UsesConfiguredSettingsDefault` — `ki.plugin.default` als letzte Stufe
- `TestResolvePluginWithProjectScope_TaskPluginBeatsSettingsDefault` — Aufgaben-Prefix vor Einstellungs-Default
- `TestResolvePluginWithProjectScope_ProjectDefaultBeatsSettingsDefault` — Projekt-Default vor Einstellungs-Default
- `TestResolvePluginWithProjectScope_StoredGlobalDefaultBeatsSettingsDefault` — `plugins.default.*` vor `ki.plugin.default`
- `TestResolvePluginWithProjectScope_UnknownSettingsDefaultReturnsNull` — unbekannter Einstellungs-Default → `null`
- `TestResolvePluginWithProjectScope_ReturnsNullIfNoDefault` — keine Zuordnung → `null`

### `ScmEventOrchestratorTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/ScmEventOrchestratorTests.cs` — Dispatch-Tests über `ScmDispatchFixture` (SQLite + gemockte SCM-/KI-Plugins + deterministische Terminalgrenze)

- `MissingWriteAccess_ExplainsTokenSettingsBeforeCliStart` — Wartegrund vor CLI-Start
- `Dispatch_ReactedStoredObservation_Waits` — Revalidierungs-Wartezustand
- `NoDefault_ActivatesTaskBeforeWaitingForSelection` — „Keine Standard-CLI"-Wartegrund ohne konfiguriertes Plugin
- `SettingsDefaultCli_StartsTaskAndDelivers` — `ki.plugin.default` → Start + Zustellung, Prefix-Persistierung
- `UnresolvableSettingsDefaultCli_KeepsWaitingState` — nicht installierter Einstellungs-Default → Wartend
- `BlockedTask_IsNeverActivated` — Beendet/Archiviert/Pausiert-Guards
- `ConfirmReady_LiftsNextAttemptLock`, `TrustDialog_*`, `UnknownCliFormat_*`, `Confirmation_*`, `ConfirmReady_UnsupportedVersion_*` — Sitzungs-/Bestätigungspfade

## Hilfsmethoden

### `ScmDispatchFixture`
Datei: `src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs`

- Konstruktor `ScmDispatchFixture(root?, time?, ki?)` — kompletter DI-Container mit SQLite-DB, `Mock<IGitPlugin>` (`Git`), `Mock<IKiPlugin>` (`Ki`), `Mock<IPluginManager>`; `KiProfile` steuert Prefix/Banner/Prompt der simulierten CLI (`ScmKiProfile.Simulator` = Prefix `Softwareschmiede.KiSimulator`, `ScmKiProfile.Devin`)
- `AddAsync(status, configured, cloned, referenceKind, contentKind, body, eventRepository, authorId, authorLogin)` — legt Projekt/GitRepository/Aufgabe + SCM-Ereignis an; `configured: true` setzt `Aufgabe.KiPluginPrefix = KiProfile.Prefix`
- `DispatchAsync(id)` / `EventAsync(id)` — Dispatch auslösen / persistierten Ereigniszustand lesen
- `Sessions`, `StartSpecs`, `Prompts`, `Responses` — Nachweise über gestartete CLI-Sitzungen, gesendete Prompts und SCM-Antworten
- `SetPrompt(task, prompt)` / `CompleteProbeAsync` — Terminal-Puffer-Steuerung / Dateitool-Kanalprobe

Wichtig für Fallback-Tests: Der Fixture-`IPluginManager` liefert genau ein KI-Plugin (`Ki.Object`, Z. 105: `GetDevelopmentAutomationPlugins().Returns([Ki.Object])`). Für „globales Default ist ein anderes Plugin" müsste die Fixture erweitert werden (zweites KI-Plugin registrierbar machen) oder der Test registriert den Projekt-Default auf einen nicht installierten Prefix.

### `TestDbContextFactory`
Datei: `src/Softwareschmiede.Tests/Helpers/TestDbContextFactory.cs` — EF-InMemory-DB für Service-Unit-Tests.

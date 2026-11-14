# Interfaces

Datei aller drei Contracts: `src/Softwareschmiede/Application/Services/Updates/UpdateInterfaces.cs`. Laut Anforderung bleiben die Signaturen unverändert; die Fehlschlag-Semantik läuft über die bestehende `InvalidOperationException`-Konvention.

## `IUpdateScriptService`
Zeilen 53–66. Implementiert von `UpdateScriptService`; im Update-E2E-Modus über dieselbe Klasse mit Testbasis registriert. Test-Doubles: `UpdatePackageServiceTests.FakeScriptService`, `Mock.Of<IUpdateScriptService>()` in den `UpdateServiceTests`.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `CreateScriptAsync` | `string targetDirectory`, `string extractedDirectory`, `string executableName`, `string logPath`, `CancellationToken ct = default` | `Task<string>` (Skriptpfad) | Schreibt das Update-Skript für die angegebenen Pfade. |
| `StartScriptAsync` | `UpdatePreparationResult preparation`, `CancellationToken ct = default` | `Task` | Startet ein vorbereitetes Update-Skript — enthält die für diese Anforderung relevante Interpreter-Auflösung. |

## `IUpdateProcessLauncher`
Zeilen 68–73. Implementiert von `UpdateProcessLauncher` (Produktion) und `RecordingUpdateProcessLauncher` (Update-E2E-Modus). Test-Doubles: `UpdateScriptServiceTests.CapturingLauncher`, `UpdateScriptServiceTests.FailingLauncher`.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `Start` | `string fileName`, `IEnumerable<string> arguments`, `string workingDirectory`, `bool runElevated` | `bool` (`true` = Prozess gestartet) | Startet den externen Updater-Prozess; `fileName` ist der von `UpdateScriptService` aufgelöste Interpreter — die Anforderung stellt sicher, dass hier nur noch vollqualifizierte feste Pfade ankommen. |

## `IApplicationShutdownService`
Zeilen 75–80. Relevante Nebenbedingung: `UpdateService.StartPreparedUpdateAsync` ruft `Shutdown()` nur nach erfolgreichem `StartScriptAsync` auf — ein Auflösungsfehler darf also vor dem Shutdown geworfen werden.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `Shutdown` | — | `void` | Beendet die laufende Anwendung geordnet nach gestartetem Updater. |

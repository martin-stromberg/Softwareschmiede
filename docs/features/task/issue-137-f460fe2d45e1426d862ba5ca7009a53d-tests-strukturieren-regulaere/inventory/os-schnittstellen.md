# OS-Schnittstellen und flaky Testfelder

## ConPTY und echte Prozesse

Produktivcode:

- `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`
- `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsole*.cs`
- `src/Softwareschmiede/Infrastructure/Terminal/Win32PseudoConsoleProcessLauncher.cs`
- `src/Softwareschmiede/Infrastructure/Terminal/SimulatedPseudoConsoleProcessLauncher.cs`
- `src/Softwareschmiede/Infrastructure/Terminal/IPseudoConsoleProcessLauncher.cs`

`KiAusfuehrungsService.StartWithPseudoConsoleAsync` ruft den injizierten `IPseudoConsoleProcessLauncher` auf. Ohne explizite Injektion wird `Win32PseudoConsoleProcessLauncher` verwendet und damit echte Windows-ConPTY-/Prozessstart-Logik.

Relevante Tests:

- `src/Softwareschmiede.Tests/Infrastructure/Terminal/PseudoConsoleSessionTests*.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Terminal/SimulatedPseudoConsoleProcessLauncherTests.cs`
- `src/Softwareschmiede.Tests/ServiceIntegration/CliEmbeddingServiceIntegrationTests.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_ConPty*.cs`
- `src/Softwareschmiede.Tests/App/Controls/TerminalControlTests.cs`

Besonders auffaellig: `TerminalControlTests.CreateSession` ruft `PseudoConsole.Create(1, 1)` auf. Damit verwenden Control-Tests echte PseudoConsole-Ressourcen, obwohl viele Szenarien nur Streams und BufferChanged-Verhalten pruefen.

## Clipboard

Relevante Dateien:

- `src/Softwareschmiede.App/Controls/TerminalControl.cs`
- `src/Softwareschmiede.Tests/App/Controls/TerminalControlTests.ClipboardPaste.cs`

Die Clipboard-Paste-Tests verwenden `System.Windows.Clipboard.SetText`, `Clipboard.Clear` und indirekt `Clipboard.GetText`. Der Testcode enthaelt bereits Retry-Helper mit Kommentar zu `CLIPBRD_E_CANT_OPEN`. Das bestaetigt, dass die Tests eine systemweite OS-Ressource beruehren und unter Parallelitaet bzw. fremder Clipboard-Nutzung schwanken koennen.

## Dateisystem-Locks und Cleanup

Fundstellen mit potentiell empfindlichen Cleanup-Pfaden:

- `src/Softwareschmiede.Tests/E2E/E2E_WorkingDirectory.cs`
- `src/Softwareschmiede.IntegrationTests/Infrastructure/WorkdirMigrationTests.cs`
- `src/Softwareschmiede.IntegrationTests/Services/*`
- `src/Softwareschmiede.Tests/ServiceIntegration/*`

Ein vorhandenes Testartefakt dokumentiert bereits eine `UnauthorizedAccessException` bei `Directory.Delete(repositoryPath, recursive: true)` in einem E2E-Dateiexplorer-Kontext. Das passt zum Anforderungsrisiko "Dateisystem-Operationen mit potentiellen Locks".

## E2E-Umgebungszustand

`src/Softwareschmiede.Tests/E2E/WpfTestBase.cs` und die E2E-Klassen starten bzw. bedienen die WPF-Anwendung. Diese Tests haengen vom gebauten Kompilat, Desktop-/FlaUI-Umgebung, laufenden Prozessen und teilweise von ConPTY-Verfuegbarkeit ab.

`ConPtyEnvironmentProbe` steuert ConPTY-E2E-Skips ueber:

```text
SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1
```

Die Datei erklaert explizit, dass automatische ConPTY-Erkennung verworfen wurde, weil sie in einer funktionierenden Umgebung falsch negativ sein konnte.

## Kandidaten fuer `Category=OsInterface`

- alle E2E-Tests, sofern sie echte WPF-/FlaUI-/Desktop-Interaktion starten
- alle ConPTY-E2E-Tests
- `CliEmbeddingServiceIntegrationTests` mit `Category=ConPTY`
- Clipboard-Paste-Tests, solange sie echte Windows-Clipboard-API verwenden
- Tests, die echte `PseudoConsole.Create` aufrufen
- Tests mit echten Prozessstarts oder externen CLI-Prozessen
- Tests mit bekannten Cleanup-/Lock-Risiken, sofern sie nicht vollstaendig in isolierten Temp-Verzeichnissen deterministisch laufen

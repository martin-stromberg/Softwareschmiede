# Interfaces

Nur die für diese Anforderung relevanten Contracts (Trait-Mechanik und Prozess-/Session-Nahtstellen, über die Test-Doubles OS-Kontakt simulieren).

## `ITraitAttribute` (xUnit v3, `Xunit.v3`)

Implementiert von `OsInterfaceAttribute`, `OsInterfaceFactAttribute`, `OsInterfaceTheoryAttribute` (`src/Softwareschmiede.Tests/Infrastructure/Testing/`).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetTraits` | — | `IReadOnlyCollection<KeyValuePair<string, string>>` | Liefert Trait-Paare (`Category`/`OsInterface`) für den `--filter`-Mechanismus |

## `IPseudoConsoleProcessLauncher` (`Softwareschmiede.Infrastructure.Terminal`)

Nahtstelle, über die reguläre Tests Launcher-Doubles statt echtem Win32-ConPTY injizieren (`TestKiAusfuehrungsServiceFactory.Create(…, launcher)`).

| Methode/Property | Parameter | Rückgabewert | Zweck |
|------------------|-----------|--------------|-------|
| `IsPseudoTerminal` | — | `bool` | Kennzeichnet echtes PTY-Backend (`false` bei Pipe/Fake) |
| `Start` | `Guid aufgabeId`, `TerminalSessionStartSpec spec`, `ITerminalOutputSink? outputSink` | `TerminalSessionStartResult` | Startet Prozess + `PseudoConsoleSession`; Doubles: `SimulatedPseudoConsoleProcessLauncher` (echtes `cmd.exe`, Pipe), `FixedOutputPseudoConsoleProcessLauncher`, `OutputByTaskPseudoConsoleProcessLauncher`, `DelayedOutputPseudoConsoleProcessLauncher`, `AlreadyExitedProcessLauncher`, `FailingReadSessionLauncher`, `DelayedFailingReadSessionLauncher`, `RecordingLauncher` |

## `ITerminalSessionFactory` (`Softwareschmiede.Infrastructure.Terminal`)

Produktions-Nahtstelle, die `ScmDispatchFixture` per Moq ersetzt.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `StartAsync` | `Guid taskId`, `TerminalSessionStartSpec spec`, `ITerminalOutputSink?`, `Func<CancellationToken, Task<bool>>?`, `CancellationToken` | `Task<TerminalSessionStartResult>` | Session-Start; Fixture-Mock liefert Mock-`ITerminalSession` + echten `cmd.exe ping`-Prozess (`ScmDispatchFixture.cs` Z. 132–149) |

## `ITerminalSession` (`Softwareschmiede.Infrastructure.Terminal`)

Von Tests gemockt (`Mock<ITerminalSession>` in `ScmEventCliExchangeAccessTests.Session`, `ScmDispatchFixture`).

| Member | Typ | Zweck im Testkontext |
|--------|-----|----------------------|
| `Buffer` | `TerminalBuffer` | Bildschirminhalt für Banner-/Prompt-Erkennung |
| `Process` | `Process` | Prozessbezug für `ScmRestrictedProcessAccess.Capture` bzw. `HasExited`-Prüfungen |
| `WritePromptAsync` | `Task`-Methode | Sendet Kanalprobe/`WritePrompt`; Mock liefert Prompt-Text an den Test zurück |
| `BufferChanged`, `Exited`, `Failed` | Events | Puffer-/Lebenszyklus-Signale; via `session.Raise(…)` getrieben |

## `ICredentialStore` (`Softwareschmiede.Domain.Interfaces`)

In `ScmEventCliExchangeAccessTests.Spec` streng gemockt: `GetCredential("Softwareschmiede.<Provider>.CommandLineParameters")` liefert die Test-Startargumente, alle anderen `null`.

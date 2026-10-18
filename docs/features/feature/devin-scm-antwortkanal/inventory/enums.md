# Bestandsaufnahme: Enums und Records

## `ScmExchangeAccessStatus`
Datei: `src/Softwareschmiede/Application/Services/ScmCliAccessEvaluator.cs`

| Wert | Bedeutung |
|------|-----------|
| `Supported` | Der ausgewertete Startvertrag erlaubt den Arbeitsbereichskanal. |
| `Denied` | Eine bekannte Regel verweigert Dateizugriff. |
| `Unknown` | Effektive Rechte nicht sicher bestimmbar. |

## `ScmPromptReadyStatus`
Datei: `src/Softwareschmiede/Application/Services/ScmPromptSessionAdapter.cs`

| Wert | Bedeutung |
|------|-----------|
| `Unknown` | Kein verifizierter Eingabeprompt. |
| `Ready` | Bestätigter KI-Eingabeprompt für den aktuellen Lauf. |
| `Busy` | Turn oder Benutzereingabe läuft. |
| `TrustPrompt` | CLI wartet auf Vertrauensentscheidung fürs Arbeitsverzeichnis. |
| `Exited` | Gebundener Lauf beendet/fehlgeschlagen. |

## `ScmExchangeAccess` (record)
`Status`, `Reason`, `WorkingDirectory`, `CliRunId`, `ConfigurationFingerprint`,
`RestrictedWriteSids` (init-only, Standard `[]`).

## `ScmCliConfigurationFile` (record)
`Path`, `Content` (`null` = beim Start nicht vorhanden).

## `ScmExchangeAccessProbe` (record)
`CliRunId`, `Nonce`, `ConfigurationFingerprint`, `DirectoryPath`, `RestrictedWriteSids`;
abgeleitete Pfade `ChallengePath`/`ReadChallengePath`/`ProofPath`.

## `TerminalSessionStartSpec` (record)
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/TerminalSessionStartSpec.cs`

| Eigenschaft | Typ | Zweck |
|-------------|-----|-------|
| `FileName` | `string` | Executable-Name/-Pfad. |
| `Arguments` | `string` | Startargumente (optionalParameters + CommandLineParameters). |
| `WorkingDirectory` | `string` | CLI-Arbeitsverzeichnis. |
| `EnvironmentVariables` | `IReadOnlyDictionary<string, string?>` | Zusätzliche Prozessvariablen. |
| `Capabilities` | `TerminalProviderCapabilities` | PTY-Fähigkeiten. |
| `PluginName` / `OptionalParameters` | `string` / `string?` | Diagnose. |

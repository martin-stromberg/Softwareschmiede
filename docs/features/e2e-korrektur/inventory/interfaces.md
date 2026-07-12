## `IKiPlugin`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IKiPlugin.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `StartCliAsync` | `string localRepoPath, string? parameters = null, CancellationToken ct = default` | `Task<ProcessStartInfo>` | Liefert die zu startende Kommandozeile (FileName/Arguments) — unabhängig vom ConPTY-Mechanismus |
| `GetProcessWindowTitle` | `Guid aufgabeId` | `string` | Erwarteter Fenstertitel (optional) |
| `SupportsSessionContinuation` | — | `bool` | Session-Fortsetzung ja/nein |
| `CheckHealthAsync` | `CancellationToken ct = default` | `Task<bool>` | Plugin-Verfügbarkeit |

Erbt von `IPlugin` (Basis-Plugin-Contract, hier nicht relevant).

## `IRunningAutomationStatusSource`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IRunningAutomationStatusSource.cs` (referenziert von `KiAusfuehrungsService`, das dieses Interface implementiert)

Wird in `App.xaml.cs` registriert als `services.AddSingleton<IRunningAutomationStatusSource>(sp => sp.GetRequiredService<KiAusfuehrungsService>())`. Für die geplante Änderung nicht direkt betroffen, aber relevant als Beispiel dafür, dass `KiAusfuehrungsService` bereits mehrfach hinter Interfaces registriert wird — Präzedenzfall für eine weitere Interface-Extraktion (`IPseudoConsoleProcessLauncher` o. ä.).

Kein bestehendes Interface kapselt aktuell den ConPTY-Prozessstart selbst — `StartPseudoConsoleProcess` ist eine private Methode direkt in `KiAusfuehrungsService`, `PseudoConsoleProcessStarter` ist eine `internal static class` ohne Interface. Ein Austauschpunkt existiert an dieser Stelle noch nicht.

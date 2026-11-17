# Test-Ergebnisse (Abschluss)

## Ausgeführte Lanes

Alle Läufe auf dem Feature-Branch nach dem finalen Build
(`dotnet build Softwareschmiede.slnx -c Debug`, 0 Warnungen, 0 Fehler).
`SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` war gesetzt (Sandbox-Regel: ConPTY-
Isolation funktioniert nur in interaktiven Sessions; betroffene Tests melden
ehrlich „Skipped“).

### Stabile reguläre Lane

```
dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"
```

**Ergebnis:** 2639 erfolgreich, 0 Fehler, 3 übersprungen, 2642 gesamt (3 m 13 s)

Baseline: 2591/2594. Delta +48 = neue Tests (12 Limit-Service, 10 Priorisierung,
11 SettingsViewModel [Theorien expandiert]) plus bestehender
`KiPluginSelectedCommand`-Anpassung. Die 3 Skips sind plattformbedingt
(vorhandene Skip-Gründe, unverändert).

### Integrationstests

```
dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj
```

**Ergebnis:** 322 erfolgreich, 0 Fehler, 0 übersprungen, 322 gesamt (41 s)

Baseline: 302. Delta +20 = `ScmEventDispatchTests_Limits` (10 Fakten/Theorien,
expandiert): globales Limit, Slot-Nachfüllen, Plugin-Limit, restriktivstes
Limit, Reconcile-Bypass, Beendet/Archiviert-Blockade, priorisierte Reihenfolge,
Continuation-vor-Sequence, UI-Pfad (`OpenFromQueue`), Host-Action-Bypass.

### OS-Interface-Lane (E2E/ConPTY/Prozess-Start)

```
dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category=OsInterface"
```

**Ergebnis:** 66 erfolgreich, 0 Fehler, 5 übersprungen, 71 gesamt (16 m 45 s)

Die 5 Skips sind die ConPTY-/PTY-abhängigen konsolidierten Szenarien
(`ConPty_LifecycleUndReplay_E2E`, `ConPty_WeitereLebenszyklusSzenarien_E2E`,
`ConPty_SessionReattach_KeineDoppelausgabe_E2E`,
`TerminalText_LiveMarkCopyAndKeepSelection_E2E`,
`InitialisierungsskriptAusfuehrung`) — bekannte Sandbox-Einschränkung, nicht
codebedingt (CLAUDE.md). Enthält den neuen E2E
`Einstellungen_AusfuehrungsLimitsGlobalUndProPlugin_E2E` innerhalb
`General_BasisUndEinstellungen_E2E` (bestanden; fokussierter Vorlauf des
Szenarios einzeln ebenfalls bestanden, 1 m 9 s).

### Format

```
dotnet format Softwareschmiede.slnx --verify-no-changes
```

**Ergebnis:** sauber, keine Änderungen nötig.

## Neue Testartefakte

- `src/Softwareschmiede.Tests/Application/Services/ScmEventExecutionLimitServiceTests.cs` — 12 Tests.
- `src/Softwareschmiede.Tests/Application/Services/ScmDispatchPrioritaetTests.cs` — 10 Tests.
- `src/Softwareschmiede.Tests/App/ViewModels/SettingsViewModelTests_AusfuehrungsLimits.cs` — 11 Tests.
- `src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests_Limits.cs` — 10 Testfälle.
- `src/Softwareschmiede.Tests/E2E/E2E_AusfuehrungsLimits.cs` — 1 konsolidiertes FlaUI-Szenario.
- Angepasst: `SettingsViewModelTests.KiPluginSelectedCommand_LaeadtSettingsGroups_FuerAusgewaehltesPlugin` (zweite Gruppe „Ausführungsbegrenzung").

## Fehlerhistorie während der Umsetzung

1. `LadenAsync_GlobalesLimitGespeichert_FeldGefuellt` — Mock-Setup unvollständig
   (Plugin-Listen leer gelassen), `LadenAsync` brach früh ab → behoben durch
   vollständige Mock-Setups.
2. `KiPluginSelectedCommand_LaeadtSettingsGroups_FuerAusgewaehltesPlugin` —
   erwartete 1 Gruppe, KI-Plugins erhalten jetzt zusätzlich
   „Ausführungsbegrenzung“ → Assertion fachlich auf 2 Gruppen erweitert.
3. E2E `Assert.Contains` auf `FehlerMeldung` — UIA `Name` liefert den
   AutomationName, nicht den Text → `SaveSettingsExpectError` liest jetzt
   `GetHelpTextOrName` (HelpText-Binding). Danach grün.

# Test-Ergebnisse

**Hinweis:** Retrospektiv aus den tatsächlich ausgeführten Läufen dokumentiert (Ausführung vor diesem Dokument, PR #327).

## Build

| Projekt | Befehl | Ergebnis | Warnungen |
|---|---|---|---|
| Softwareschmiede.slnx (gesamt) | `dotnet build` | erfolgreich | 0 Warnungen, 0 Fehler |
| Softwareschmiede.slnx | `dotnet format --verify-no-changes` (pre-push Hook) | sauber | — |

## Testläufe

`SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` war bei allen Läufen gesetzt.

| Projekt | Befehl | Ergebnis |
|---|---|---|
| Softwareschmiede.Tests | `dotnet test --filter "FullyQualifiedName~ScmCliPromptProfileTests|FullyQualifiedName~ScmEventOrchestratorTests"` | **44 bestanden, 0 fehlgeschlagen** |
| Softwareschmiede.IntegrationTests | `dotnet test --filter "FullyQualifiedName~ScmEventDispatchTests_Devin|FullyQualifiedName~ScmEventCliExchangeAccessTests"` | **106 bestanden, 0 fehlgeschlagen** |

## PR-Checks (CI-Nachweis, PR #327)

| Check | Ergebnis |
|---|---|
| build & test | pass (8m44s) — voller regulärer + Integrations-Testlauf in CI |
| os-interface tests | pass (2m43s) |
| static checks | pass |
| CodeQL | pass |
| Analyze (actions) / Analyze (python) | pass |
| detect-backmerge | pass |
| back-merge-skip | skipping |

Die CI-Spur `build & test` deckt die vollständige reguläre Test-Suite ab (in dieser Sandbox wurden zusätzlich die betroffenen Klassen fokussiert ausgeführt); `os-interface tests` deckt die E2E-Spur ab. Beide sind in der CI grün gelaufen.

## Coverage

Nicht gemessen — kein Coverage-Instrument konfiguriert.

## Fehlende Tests (Fallback-Prüfung)

| Geänderte Datei | Testabdeckung |
|---|---|
| `ScmCliPromptProfile.cs` (Bereich + Anzeigetext) | `ScmCliPromptProfileTests` (Grenzen), `ScmEventOrchestratorTests`, Integrationstests `ScmEventCliExchangeAccessTests`/`ScmEventDispatchTests_Devin` |
| Test-/Fixture-Dateien | selbst Tests bzw. Test-Infrastruktur |
| `docs/help/plugins/github-ereignisse/*` | Dokumentation, kein Code |

## Hinweise

- Der Lifecycle wurde bei dieser Task nicht eingehalten: Diese Ergebnisse stammen aus den Läufen unmittelbar vor dem Merge; die PR-Checks (inkl. Voll-Testspuren) sind der ergänzende unabhängige Nachweis.

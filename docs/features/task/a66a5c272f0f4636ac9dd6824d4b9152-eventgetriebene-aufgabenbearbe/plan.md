# Plan: Devin-CLI-Versionsbereich auf v3000.x erweitern

**Hinweis:** Retrospektiv erstellt; dokumentiert den tatsächlich umgesetzten Stand (PR #327).

## Ziel

Die SCM-Ereigniszustellung toleriert Devin CLI `v3000.*.*` (gleiches Major, beliebige Minor-/Patch-Version). Andere Major-Versionen bleiben gesperrt. Codex (`v0.114.x`) und Claude Code (`v2.1.x`) bleiben unverändert.

## Schritte

1. **Versionskatalog** — `src/Softwareschmiede/Application/Services/ScmCliPromptProfile.cs`: Devin-`SupportedRanges` von `[3000.11.0, 3000.12.0)` auf `[3000.0.0, 3001.0.0)` erweitern; `SupportedVersionsText` `"v3000.11.x"` → `"v3000.x"`. Regex und Banner-Anker bleiben unverändert.
2. **Unit-Tests** — `ScmCliPromptProfileTests`: Grenzfälle umdrehen (`v3000.12.0`/`v3000.12.7` → supported, `v3001.0.0` neu als obere Grenze, `v2999.9.9` bleibt unsupported); Wartegrund-Samples auf `v3001.0.0`/`v3000.x` umstellen. `ScmEventOrchestratorTests.ConfirmReady_UnsupportedVersion_*`: Banner `v3000.12.7` → `v3001.0.0`.
3. **Test-Fixture** — `ScmDispatchFixture`: Kommentar auf `v3000.x` anpassen (Default-Banner `v3000.11.92` bleibt gültig).
4. **Integrationstests** — `ScmEventDispatchTests_Devin`: Patch-Version-Fall zu `DevinTask_WithNewerMinorVersion_DeliversEvent` mit Banner `v3000.12.7` (Nachweis: neuere Minor liefert); `DevinTask_WithUnsupportedVersion_WaitsWithHonestReason` auf `v3001.0.0`. `ScmEventCliExchangeAccessTests`: `v3000.12.7` in den Supported-Fällen (`ActualStartSpec_*`, `TokenlessAnchorReprint_*`), `v3001.0.0` als neuer Unsupported-Fall.
5. **Hilfedoku** — `docs/help/plugins/github-ereignisse/*`: `v3000.11.x` → `v3000.x`; die bisher pauschale Erläuterung „jeweils gleiches Major.Minor, beliebiger Patch" für Devin auf „gleiches Major, beliebige Minor- und Patch-Version" korrigieren.
6. **Verifikation** — Voller Build, gefilterte Testläufe der betroffenen Klassen (Unit + Integration) mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`; Format-Check über pre-push Hook.

## Nicht-Ziele

- Keine Änderung am Zugriffs-/Berechtigungsvertrag (`ScmCliAccessEvaluator`) — Versionserweiterung ist keine Freigabe; die Dateitool-Kanalprobe bleibt die Sicherheitsgrenze.
- Keine neuen E2E-/FlaUI-Tests: Die Versionserkennung ist über Adapter-/Orchestrator-Tests und den simulierten CLI-Kanal der Integrationstests vollständig abgedeckt; ein echtes Devin-Banner-Update lässt sich ohne reale CLI ohnehin nicht nachstellen.
- `DevinPlugin.cs`/`DevinPluginTests`-Kommentare mit „empirisch gegen v3000.11.x" bleiben als historische Messwerte stehen.

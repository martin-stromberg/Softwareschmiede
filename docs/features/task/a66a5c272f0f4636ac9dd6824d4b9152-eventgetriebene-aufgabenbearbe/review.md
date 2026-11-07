# Umsetzungs-Review (retrospektiv)

## Ergebnis

**Status:** Vollständig umgesetzt

Gegenstand: Nachträgliches Review des gemergten Stands (PR #327, Merge-Commit `ee90bb4`, Basiskommit `85bc20d`) gegen [requirement.md](requirement.md) und [plan.md](plan.md). Prüfbasis: Quellcode-Inspektion des Diffs plus die dokumentierten Läufe in [test-results.md](test-results.md); keine erneute Ausführung.

## Umgesetzte Planelemente

- [x] `ScmCliPromptProfile.cs:56` — Devin-Bereich `[3000.0.0, 3001.0.0)`, `SupportedVersionsText` `"v3000.x"`. Halboffener Bereich trifft `v3000.*.*` exakt; `v2999.x`/`v3001.x` bleiben außen.
- [x] `ScmCliPromptProfileTests` — Grenzen neu: `v3000.12.0`/`v3000.12.7` supported, `v3001.0.0` unsupported; Wartegrund-Samples auf `v3001.0.0`/`v3000.x`; Testzusammenfassung beschreibt die Major-Logik für Devin korrekt.
- [x] `ScmEventOrchestratorTests.ConfirmReady_UnsupportedVersion_DeliversAfterChannelProbe` — Banner `v3001.0.0`; Bestätigungspfad bleibt für out-of-range-Versionen belegt.
- [x] `ScmDispatchFixture`-Kommentar `v3000.x` (Default-Banner weiter `v3000.11.92`, im Bereich).
- [x] `ScmEventDispatchTests_Devin` — `DevinTask_WithNewerMinorVersion_DeliversEvent` weist Zustellung mit `v3000.12.7` nach (genau der Issue-Fall); `DevinTask_WithUnsupportedVersion_WaitsWithHonestReason` prüft `v3001.0.0` mit ehrlichem Grund.
- [x] `ScmEventCliExchangeAccessTests` — `v3000.12.7` supported in `ActualStartSpec_*` und `TokenlessAnchorReprint_*`; `v3001.0.0` als neuer Unsupported-Fall in beiden Tabellen.
- [x] Hilfedoku (6 Dateien) — `v3000.x`; Devin-Erläuterung „gleiches Major, beliebige Minor-/Patch-Version" getrennt von Codex/Claude formuliert.

## Geprüfte Risiken / Befunde

- **Anzeige-/Wirksamkeits-Synchronisation:** `SupportedVersionsText` und `SupportedRanges` wurden gemeinsam geändert — kein Fall, in dem der Wartegrund einen abweichenden Bereich behauptet.
- **Regex-Abdeckung:** Der Versions-Regex (`\d+\.\d+\.\d+`) parst `v3000.12.x`/`v3001.x` gleichermaßen; kein Musterabgleich nötig.
- **Keine Nebenwirkung auf andere CLIs:** Codex/Claude/Simulator-Einträge unverändert; `ScmCliVersionRange.Contains` ist numerisch, keine String-Falle bei `v3000.100.0`.
- **Sicherheitsgrenze:** Versionserweiterung ist keine Freigabe — `ScmCliAccessEvaluator` und die Dateitool-Kanalprobe bleiben unberührt; `--permission-mode`-/Konfigurationsprüfung unverändert.
- **Verbleibende Inkonsistenz (bewusst, kein Befund):** Historische empirische Kommentare in `DevinPlugin.cs`/`DevinPluginTests.cs` nennen weiter `v3000.11.x` — sie beschreiben Messbedingungen, nicht den unterstützten Bereich.

## Hinweise

- Diese Review ist retrospektiv: Der Lifecycle (inventory → requirement → plan → plan-check → Umsetzung → review) wurde bei dieser Task nicht eingehalten; die Artefakte wurden nach dem Merge nachgezogen. Der fachliche Nachweis (Grenzen, Zustellpfad, Wartegrund, Doku) ist gleichwohl vollständig und durch Unit-, Integrations- und CI-Läufe belegt.

# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Dokumentiertes Prüfergebnis: konkrete Update-Empfehlungen je Paket inkl. Risikobewertung (Updates, Security, Schwachstellen, Veraltung/Pflegestatus, Nachfolgepakete, Breaking Changes) | Prüfungstabelle im PR-Body (Programmablauf „PR-Erstellung", Schritt 6 der Umsetzungsreihenfolge); Befundlage aus `inventory/audit.md` vollständig übernommen (2 direkte Updates, transitive Updates, keine Schwachstellen, keine Deprecated-Pakete, Pflegestatus-Auffälligkeiten als aufgeschobene Empfehlungen); Changelog-/Breaking-Change-Analyse für `Serilog.Extensions.Hosting` in Designentscheidungen dokumentiert | Verifikations-Schritte: Restore mit Transitiv-Prüfung, Build, Testläufe, Schwachstellen-Scan | Abgedeckt |
| PR gegen `staging` mit allen niedrig-/mittelriskanten Empfehlungen als Versionsänderungen in `*.csproj`, inkl. `dotnet restore`/`dotnet build`-Verifikation und Testlauf | Schritt 1: `Serilog.Extensions.Hosting` → 11.0.0 (`Softwareschmiede.App.csproj` Z. 57, Risiko mittel mit Begründung) und `Microsoft.Extensions.TimeProvider.Testing` → 11.0.1 (`Softwareschmiede.Tests.csproj` Z. 33, Risiko niedrig); ein gemeinsamer PR (Annahme 7 zu „einem Pull Request") | Schritt 2: voller Build mit `-p:TreatWarningsAsErrors=true` (CI-Gate-Analogon); Schritt 3: stabile Lane `Category!=OsInterface` (Soll 2800/0/3) + `Softwareschmiede.IntegrationTests` (Soll 436/0/0), beide mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` inline und Vordergrund-Lauf; `OsInterface`-Lane über PR-CI | Abgedeckt |
| Dokumentation risikoreicher/aufgeschobener Empfehlungen im PR-Body oder als Folge-Issue | Abschnitt „Aufgeschobene Empfehlungen" listet alle nicht umgesetzten Befunde mit Begründung (kein stabiles `SQLitePCLRaw`-Update, transitive Major-/Dev-Time-Updates ohne direkten Hebel, `Serilog.Sinks.File` nur Prerelease); Annahme 4 legt fest: rein informative Punkte → PR-Body, kein Folge-Issue nötig | PR-Body-Inhalt in Schritt 6 konkret benannt | Abgedeckt |
| Umgang mit fehlenden Objekten (keine Updates, keine niedrig-/mittelriskanten Empfehlungen, nur transitiv verwundbare Pakete, Nachfolgepaket ohne Veraltung) | Scope-Klärung füllt alle Fälle; Annahmen 1–4 lösen die zugehörigen offenen Fragen fachlich begründet auf; Akzeptanzszenarien 2 und 3 decken Fehler-/Verschlechterungsfälle ab | Szenario 3 definiert Vorgehen bei neu eingebrachter Schwachstelle (Revert + Dokumentation) | Abgedeckt |
| Scope-Ausschlüsse der Anforderung (keine risikoreichen Umsetzungen, kein npm/Actions/SDK, kein TFM-/Workflow-Umbau, keine Refactorings über Update-Brüche hinaus, kein CPM) | Alle 5 Ausschlüsse als Zeilen 1–5 der Scope-Ausschluss-Tabelle mit Begründung und Status `bestätigt`; zusätzlich 2 fachlich begründete Ergänzungen (kein transitives Pinning, keine Prereleases), ebenfalls mit Status | — | Abgedeckt |
| `THIRD_PARTY_LICENSES.md`-Mitpflicht inkl. Bestandskorrektur der bereits veralteten Einträge | Eigener Programmablauf „`THIRD_PARTY_LICENSES.md` aktualisieren" mit konkreten Zeilen-/Versionsfestlegungen (ausgelieferte Abhängigkeiten + Test-/Build-Sektion, `Xunit.SkippableFact` entfernen, fehlende Transitiva ergänzen, SPDX, Stand-Datum); Annahme 5 begründet Ausführung im selben PR | Erhebung via `dotnet list package --include-transitive` nach finalem Paketgraph (Schritt 5) | Abgedeckt |
| Konsistenz mit CI-Gates (security-scan, TreatWarningsAsErrors, Format, Test-Lanes) und Repo-Konventionen (`Closes #469`, kein Beenden von `Softwareschmiede.App.exe`, kein Backgrounding von `dotnet test`, `--watch --fail-fast`) | Schritt 4: `--vulnerable --include-transitive` identisch zum CI-Gate; Schritt 2: `TreatWarningsAsErrors`; Schritt 6: `Closes #469`; Seiteneffekte/Risiken benennen Selbsthosting-Lock, OsInterface-Lücke, Security-Gate; Schritt 7: fail-fast-Überwachung mit Diagnoseweg | — | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

Entfällt — Status ist `Plan vollständig`.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Gesamte Anforderung (Wartungslauf ohne UI-Änderung) | Keine E2E-Tests geplant | Nicht erforderlich mit Begründung: Plan begründet im Abschnitt „E2E-Tests" nachvollziehbar, dass kein über UI oder Nutzeraktion erreichbarer Ablauf berührt wird; Funktionsnachweis über vollen Build inkl. `TreatWarningsAsErrors`, bestehende stabile Lanes und PR-CI (die die `OsInterface`-Lane zusätzlich ausführt) |

## Fehlende oder unvollständige Planbestandteile

Entfällt — Status ist `Plan vollständig`.

## Hinweise

- **Restrisiko `Serilog.Extensions.Hosting` 10.0.0 → 11.0.0:** Die Einstufung als „mittel" statt „hoch" beruht auf der dokumentierten Changelog-Analyse (reines .NET-11-TFM-Alignment ohne API-Break). Akzeptanzszenario 2 deckt den Fall ab, dass diese Analyse sich an Build/Tests als falsch erweist (minimaler Fix oder Revert + Dokumentation) — kein Planungsmanko, aber der kritischste Punkt der Umsetzung.
- **`OsInterface`-Lane ohne lokalen Vorher-Nachweis:** In `inventory/tests.md` dokumentiert; der Plan verweist korrekt auf die PR-CI als Verifikationsort. Beim Check-Monitoring (Schritt 7) darauf achten, dass ein `OsInterface`-Fehlschlag nicht automatisch auf das Update zurückgeführt wird — zuerst Vorher-Stand prüfen.
- **Format-Gate:** `dotnet format --verify-no-changes` ist als lokaler Schritt nicht explizit im Plan, für reine `*.csproj`-/Markdown-Änderungen aber praktisch irrelevant und über Schritt 7 (PR-Checks) abgedeckt.
- Alle 8 offenen Fragen der Anforderung sind im Abschnitt „Getroffene Annahmen" fachlich begründet aufgelöst; die tasks.md ist konsistent zum Plan (12 Aufgaben decken die 7 Umsetzungsschritte plus Lizenzdatei-Teilschritte ab).

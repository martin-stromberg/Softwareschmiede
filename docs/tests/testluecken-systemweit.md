# Testlücken – Systemweite Analyse (Planung + Architektur + Implementierung)

## Kontext der Analyse

- Berücksichtigte Planungs-/Architekturdokumente:
  - `docs/requirements/requirements-analysis.md`
  - `docs/architecture/architecture-blueprint.md`
  - `docs/improvements/architecture-review.md`
- Berücksichtigter Implementierungsstand: `src/**` inkl. Unit- und Integrationstests
- Coverage-Lauf (dotnet): `dotnet test Softwareschmiede.slnx --collect:"XPlat Code Coverage"` (166/166 Tests erfolgreich)

## Nicht getestete oder unzureichend getestete Funktionalitäten (priorisiert)

| Priorität | Funktionalität | Ist-Zustand Testabdeckung | Anforderungs-/Architekturbezug | Begründung |
|---|---|---|---|---|
| P1 | Git-Orchestrierung im Application-Layer (`GitOrchestrationService`: Push/Pull/PR/Reset/Commit-Flows) | Keine direkte Abdeckung (0%) | FR-2.5, FR-2.6, FR-2.7, FR-3.7; Architektur-Komponente „GitOrchestrationService“ | Kern-Git-Flows sind zentral für den Entwicklungsprozess; Fehler hier blockieren Abschluss und Synchronisation von Aufgaben. |
| P1 | KI-Orchestrierung inkl. Session-/Streaming-Lifecycle (`KiAusfuehrungsService`) | Keine direkte Abdeckung (0%) | FR-4.2, FR-4.3, FR-4.6, FR-7.3; Architektur-Review (Streaming/Reconnect-Risiken) | Realtime-Streaming und Folgeprompts sind Kernnutzen; kritische Zustands-/Race-Condition-Risiken bleiben unvalidiert. |
| P1 | CLI-Prozesssteuerung (`CliRunner`) | Keine direkte Abdeckung (0%) | NFR-12; Architektur-Review Abschnitt CLI-Prozesssteuerung (Deadlock/Cleanup) | Kritischer Infrastrukturpunkt für alle Plugin-Aufrufe; Risiken bei Deadlock, Prozessbeendigung und Fehlerausgabe ohne Testschutz. |
| P1 | Sichere Token-Speicherung (`WindowsCredentialStore`) | Keine direkte Abdeckung (0%) | NFR-4, Sicherheitskonzept Blueprint | Sicherheitsanforderung „kein Klartext“ ist ohne direkte Tests auf Plattformintegration unzureichend abgesichert. |
| P1 | Zentrale UI-Workflows Aufgaben/Projekte (`AufgabeDetail`, `NeueAufgabe`, `ProjektDetail`, `ProjektListe`, `Home`) | Praktisch keine UI-Komponententests (jeweils 0% in Coverage) | US-1 bis US-5, FR-1/FR-3/FR-4/FR-8 | Akzeptanzkriterien sind überwiegend UI-getrieben; fehlende Tests erhöhen Risiko für Regressionen in Kern-User-Journeys. |
| P2 | Entwicklungsprozess-Randfälle im Service (`EntwicklungsprozessService`) | Nur teilweise abgedeckt (ca. 50%); mehrere Methodenpfade unzureichend | FR-4.4, FR-4.5; Aufgaben-Lebenszyklus Blueprint | Bestehende Tests decken Start/Statuspfade gut ab, aber nicht alle Git-/Test-/Rollback-Operationen inkl. Fehlerszenarien. |
| P2 | Plugin-Einstellungsverwaltung (`PluginSettingsService`: Get/Set/Delete/HasValue) | Geringe Abdeckung (ca. 30%) | FR-2, FR-5; NFR-12 | Konfigurationsfehler wirken direkt auf Plugin-Funktion; CRUD-/Validierungsverhalten ist nur teilweise abgesichert. |
| P2 | Einstellungsseite über Workdir hinaus (`Einstellungen.razor.cs`) | Teilabdeckung (ca. 40%) | FR-2.1, FR-5.1, NFR-4 | Aktuelle Tests fokussieren Arbeitsverzeichnis; Plugin-bezogene Settings-Flows und Fehlerpfade fehlen weitgehend. |
| P2 | Agentenpaket-Verwaltung in der UI (`AgentenpaketeSeite.razor(.cs)`) | Keine direkte UI-Abdeckung (0%) | FR-6.1 bis FR-6.4 | Feature ist fachlich zentral für Agentenauswahl und Paketvorschau, wird aber nur indirekt über Services abgesichert. |
| P3 | Startup/DI-/App-Bootstrap (`Program.cs`, `Softwareschmiede.Client/Program.cs`) | Keine direkte Abdeckung (0%) | NFR-1, NFR-3 | Fehlkonfigurationen in DI/Startup werden ohne Smoketests erst spät entdeckt. |
| P3 | Kleine Domain-/UI-Bausteine (`PluginKonfiguration`, Badge-Komponenten) | Keine oder geringe direkte Abdeckung | NFR-11, Domain-Modell | Geringere Komplexität, aber dennoch ungeschützt gegen unbeabsichtigte Änderungen. |

## Umsetzungsstand (aktuelle Iteration)

- ✅ Erste P1-Lücke adressiert: neue Unit-Tests für `GitOrchestrationService` ergänzt
  - abgedeckt: `IssuesAbrufenAsync`, `CommitAsync`, `ResetAsync`, `PushAsync` (Fehlerfall), `PullRequestErstellenAsync` (Erfolg + Fehlerfall)
- 🔄 Offene P1-Lücken:
  - `KiAusfuehrungsService`
  - `CliRunner` und `WindowsCredentialStore`
  - UI-Kernworkflows (`AufgabeDetail`, `NeueAufgabe`, `ProjektDetail`, `ProjektListe`, `Home`)

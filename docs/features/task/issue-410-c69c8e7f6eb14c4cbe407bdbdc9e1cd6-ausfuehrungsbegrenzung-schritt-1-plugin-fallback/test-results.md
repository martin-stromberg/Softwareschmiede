# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

Ausgeführte Kommandos (Arbeitsverzeichnis: Repository-Root `D:\Repositories\softwareschmiede\c69c8e7f-6eb1-4c4c-be40-7bdbdc9e1cd6`):

| Kommando | Exit-Code | Ergebnis |
|----------|-----------|----------|
| `dotnet build Softwareschmiede.slnx` | 0 | Erfolg, 0 Warnungen, 0 Fehler |
| `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"` | 0 | Bestanden: 2591, Fehler: 0, Übersprungen: 3, Gesamt: 2594 (3 m 16 s) |
| `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface" --collect:"XPlat Code Coverage"` | 0 | Bestanden: 2591, Fehler: 0, Übersprungen: 3, Gesamt: 2594 (3 m 18 s) |
| Fokussiert (vorhergehend, vor den Code-Review-Korrekturen): `dotnet test ... --filter "FullyQualifiedName~PluginSelectionServiceTests\|FullyQualifiedName~ScmEventOrchestratorTests"` | 0 | Bestanden: 52, Fehler: 0, Übersprungen: 0 |

Die vollständige Lane wurde **nach** den Code-Review-Korrekturen erneut vollständig ausgeführt (Zeilen 2 und 3 der Tabelle) — alle neuen und bestehenden Tests bestanden.

## Fehlgeschlagene Tests

Keine.

Offener Punkt außerhalb des automatisierten Laufs: siehe Abschnitt „Reale End-to-End-Abnahme" — Szenario als `Nicht durchführbar` dokumentiert; laut Lifecycle erfordert dies eine Anwender-Entscheidung (manuelle Abnahme oder dokumentierter Verzicht). Die Auftragsvorgabe des Anwenders sieht die dokumentierte Nichtdurchführbarkeit im automatisierten Lauf ausdrücklich als zulässigen Endzustand vor.

## E2E-Abdeckung

Plan und Plan-Check stufen keine neuen E2E-Tests als erforderlich ein („Kein neuer oder geänderter Benutzerfluss — interne Auflösungs-/Fehlerlogik; Wartegrund erscheint über den bestehenden `Reason`-Mechanismus"). Alle sechs Akzeptanzszenarien aus `plan.md` sind durch die automatisierte Lane abgedeckt:

| Szenario | Test / Testklasse | Ergebnis |
|----------|-------------------|----------|
| 1 Happy Path Fallback (deaktiviert) | `ScmEventOrchestratorTests.DisabledTaskPlugin_FallsBackToGlobalDefault_Delivers` | Bestanden |
| 2 Happy Path Fallback (nicht installiert) | `PluginSelectionServiceTests.ResolveEnabledPlugin_TaskPrefixNotInstalled_FallsBack` (+ Kette) | Bestanden |
| 3 Verweigerter Start (Wartegrund, Protokoll, kein Hard-Lock) | `ScmEventOrchestratorTests.UnavailablePlugins_EventWaitsWithoutHardLock_ResumesAfterFix` (Teil 1) | Bestanden |
| 4 Wiederaufnahme nach Konfigurationskorrektur | `ScmEventOrchestratorTests.UnavailablePlugins_EventWaitsWithoutHardLock_ResumesAfterFix` (Teil 2) | Bestanden |
| 5 Strenge bei expliziter Auswahl | `ScmEventOrchestratorTests.ExplicitPluginPrefix_RemainsStrict_LocksOnUnavailable` | Bestanden |
| 6 Nichts konfiguriert (Regression) | `ScmEventOrchestratorTests.UnresolvableSettingsDefaultCli_KeepsWaitingState`, `NoDefault_ActivatesTaskBeforeWaitingForSelection` | Bestanden |

## Reale End-to-End-Abnahme

Die Anforderung berührt Persistenz (EF Core, `ScmIncomingEvent`) und Plugin-/CLI-Integration — eine reale Abnahme ist formal erforderlich, in dieser Agent-Sandbox aber nicht durchführbar:

| Szenario | Durchgeführt über | Ergebnis |
|----------|-------------------|----------|
| Happy Path + verweigerter Start + Wiederaufnahme (SCM-Ereignis real durch die laufende Anwendung, echter SCM-Provider/Webhook) | — | Nicht durchführbar: kein interaktiver Desktop in der Sandbox (FlaUI-/App-Start gehört zur ausgenommenen `OsInterface`-Lane), kein realer SCM-Provider-Zustellweg; `Softwareschmiede.App.exe` darf zudem nicht eigenständig beendet/gesteuert werden. Die identischen Akzeptanzschritte sind stattdessen über `ScmDispatchFixture` real nachgewiesen: echte SQLite-Datenbank, echte DI-/Serviceverdrahtung, echte Orchestrierung inkl. Protokoll- und Outbox-Persistenz — gemockt ist ausschließlich die Terminal-Sitzungsgrenze. |

Anwender-Entscheidung gemäß Lifecycle-Eskalation: entweder manuelle Abnahme (`Bestanden (manuell, {Datum})`) oder dokumentierter Verzicht (`Verzichtet (Anwender, {Datum}, {Begründung})`). Die Auftragsvorgabe erlaubt ausdrücklich die dokumentierte Nichtdurchführbarkeit im automatisierten Lauf; die finale Entscheidung obliegt dem Anwender.

## Zusammenfassung

- Gesamt: 2594
- Bestanden: 2591
- Fehlgeschlagen: 0
- Übersprungen: 3 (plattformabhängige Tests, `Category!=OsInterface`-Lane)

## Testabdeckung

**Abdeckung:** gemessen via coverlet (`coverage.cobertura.xml`). Geänderte Dateien:

| Datei | Abdeckung |
|-------|-----------|
| `src/Softwareschmiede/Application/Services/PluginSelectionService.cs` | 98,3 % (472/480 Zeilen) |
| `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs` | 63,2 % (604/956 Zeilen) |

Die neue Methode `ResolveEnabledDevelopmentAutomationPluginWithProjectScopeAsync` und der neue Wartepfad in `DispatchCoreAsync` sind durch die 11 neuen Tests abgedeckt; ungedeckte Zeilen im Orchestrator betreffen vorhandene, von dieser Änderung nicht berührte Pfade.

## Fehlende Tests

Keine für die geänderten Dateien — `PluginSelectionServiceTests.cs` und `ScmEventOrchestratorTests.cs` existieren und wurden erweitert.

# Testplan – Systemweite Schließung der Testlücken

## Ausgangsbasis
- Lückenliste: `docs/tests/testluecken-systemweit.md`
- Ziel: Priorisierte, testbare Arbeitspakete zur Schließung der identifizierten Lücken

## Priorisierte Arbeitspakete

1. **AP-01 (P1): Git-Orchestrierung im Application-Layer absichern**
2. **AP-02 (P1): KI-Orchestrierung/Session-Lifecycle absichern**
3. **AP-03 (P1): CLI-Prozesssteuerung und Credential-Speicher absichern**
4. **AP-04 (P1): Kern-UI-Workflows Aufgaben/Projekte/Home absichern**
5. **AP-05 (P2): Entwicklungsprozess-Randfälle vollständig testen**
6. **AP-06 (P2): PluginSettings und Einstellungsseite erweitern**
7. **AP-07 (P2): Agentenpakete-Seite absichern**
8. **AP-08 (P3): Startup/DI-Smoketests ergänzen**

## AP-01 – GitOrchestrationService
- **Betroffene Methoden:** `IssuesAbrufenAsync`, `CommitAsync`, `ResetAsync`, `PushAsync`, `PullAsync`, `PullRequestErstellenAsync`
- **Testszenarien:**
  - Erfolgsfälle für Commit/Reset/Pull/PR
  - Fehlerfälle bei fehlender Aufgabe, fehlendem Klonpfad, fehlendem Branch
  - Validierung der Protokolleinträge pro Aktion
- **Akzeptanzkriterien:**
  - Jede öffentliche Methode hat Erfolgstest plus relevante Fehlerpfade
  - Protokolleinträge enthalten die erwarteten Git-Aktionen

## AP-02 – KiAusfuehrungsService
- **Betroffene Methoden:** `StartKiLauf`, `AbortKiLauf`, `Subscribe`, `SessionBereinigen`, `GetBufferedLines`, `IsRunning`
- **Testszenarien:**
  - Start/Stop/Abort-Lifecycle
  - Subscriber-Benachrichtigung und Buffer-Verhalten
  - Fehlerpfade bei Exceptions im KI-Lauf
- **Akzeptanzkriterien:**
  - Lifecycle ist vollständig abgesichert
  - Keine hängenden Tasks oder Race-Condition-Regressions

## AP-03 – Infrastruktur (CLI + Credentials)
- **Betroffene Klassen:** `CliRunner`, `WindowsCredentialStore`
- **Testszenarien:**
  - ExitCode-Handling, stdout/stderr-Auswertung, Cancellation
  - Read/Write/Delete Credential-Flows inkl. Fehlerpfaden
- **Akzeptanzkriterien:**
  - Erfolgs- und Fehlerpfade beider Klassen sind getestet
  - Sicherheitsanforderungen für Credential-Handling bleiben verifiziert

## AP-04 – UI-Kernworkflows
- **Betroffene Komponenten:** `AufgabeDetail`, `NeueAufgabe`, `ProjektDetail`, `ProjektListe`, `Home`
- **Testszenarien:**
  - Kern-Journeys (anlegen, starten, navigieren, abschließen)
  - Fehlermeldungen und Statuswechsel
- **Akzeptanzkriterien:**
  - Kritische User-Flows sind durch Komponententests abgesichert

## AP-05 bis AP-08
- **AP-05:** Randfalltests für `EntwicklungsprozessService` erweitern
- **AP-06:** CRUD- und Fehlerpfade für `PluginSettingsService`/`EinstellungenBase` ergänzen
- **AP-07:** Datei-/Ordneraktionen der `AgentenpaketeSeite` testen
- **AP-08:** DI-/Startup-Smoketests (`Program.cs`, Client `Program.cs`) hinzufügen

## Abschlusskriterien
- P1-Lücken sind testseitig geschlossen
- `dotnet test Softwareschmiede.slnx` ist erfolgreich
- `docs/tests/testluecken-systemweit.md` wird bei Umsetzung aktualisiert (Status pro Paket)

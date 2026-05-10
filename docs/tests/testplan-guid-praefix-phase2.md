# Testplan – GUID-Präfix Phase 2

## Ausgangslage
Bereits umgesetzt (nicht mehr im Scope dieses Plans):
- Neue Tests für `EntwicklungsprozessService` (inkl. Fehlerpfad/Whitespace-`executionId`)
- Neue Tests für `KiAusfuehrungsService` (ExecutionId, Doppelstart, Abbruch/Cleanup)
- Neue Tests für `GitHubCopilotPlugin` (u. a. Schreibfehler `.copilot-task`, `ReadAgentDescription`-Fallback ohne `description:`)

---

## Priorisierte Umsetzungsplanung (nach Risiko)

| Priorität | Risiko | Lücke | Zieltest | Dateipfad | Vorgehen | Akzeptanzkriterium |
|---|---|---|---|---|---|---|
| P1 | **Hoch (fachlich)** | Semantik `AbortKiLauf`: Status bleibt aktuell `InBearbeitung`, fachlich evtl. unklar | `AbortKiLauf_ShouldSetExpectedStatus_AfterCancellation` (finaler Name nach Fachentscheidung) | `src/Softwareschmiede.Tests/Application/Services/KiAusfuehrungsServiceTests.cs` (+ ggf. `src/Softwareschmiede.Tests/Application/Services/EntwicklungsprozessServiceTests.cs`) | 1) Fachentscheidung treffen: Soll nach Abbruch `InBearbeitung` bleiben oder auf `Offen`/anderen Status wechseln? 2) Test auf final gewünschte Status-Transition anpassen/ergänzen. 3) Protokolleintrag/Statusübergang mitprüfen, falls Bestandteil der Regel. | Test dokumentiert und erzwingt die **fachlich freigegebene** Ziel-Semantik; bei Regression schlägt er deterministisch fehl. |
| P2 | **Mittel (Robustheit)** | `ReadAgentDescription` Catch-Pfad bei Datei-Lesefehler ungetestet | `GetAvailableAgentsAsync_ShouldReturnNullDescription_WhenAgentFileCannotBeRead` | `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubCopilotPluginTests.cs` | Agent-Datei erzeugen, dann exklusiv sperren (`FileShare.None`), `GetAvailableAgentsAsync` ausführen, Ergebnis prüfen: Agent wird weiter geliefert, `Beschreibung == null`, keine Exception nach außen. | Test deckt Catch-Pfad reproduzierbar ab; Agent-Discovery bleibt stabil trotz I/O-Fehler. |
| P3 | **Niedrig (Coverage/Logging)** | `.gitignore` No-Op-Rückgabe (`EnsureGitIgnoreRuleAsync` => `false`) praktisch schwer erreichbar | `StartDevelopmentAsync_ShouldLogAlreadySynced_WhenGitIgnoreIsAlreadyCanonical` **oder** refaktorierter Unit-Test auf Hilfsmethode | `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubCopilotPluginTests.cs` (ggf. plus kleine Testbarkeit-Refaktorierung in `plugins/Softwareschmiede.Plugin.GitHubCopilot/GitHubCopilotPlugin.cs`) | Kanonischen `.gitignore`-Inhalt vorbereiten (`*.copilot-task.md` bereits korrekt), `StartDevelopmentAsync` ausführen und „already-synced“-Pfad indirekt verifizieren (z. B. über Logger-Assertion oder gezielte Extraktion testbarer Helper-Logik). | No-Op-Pfad ist explizit abgesichert **oder** als bewusst nicht wirtschaftlich testbar dokumentiert (inkl. Begründung). |

---

## Umsetzungsreihenfolge

1. **P1 zuerst**: Fachliche Klärung für Abbruch-Semantik + Test festziehen (verhindert widersprüchliche zukünftige Änderungen).
2. **P2 danach**: Catch-Pfad für Agent-Dateilesen absichern (Robustheit bei realen I/O-Problemen).
3. **P3 zuletzt**: No-Op-Pfad vervollständigen oder mit sauberer Begründung als „accepted gap“ dokumentieren.

---

## Definition of Done (Phase 2)

- Alle P1/P2-Tests sind implementiert und grün.
- P3 ist entweder testseitig abgedeckt oder als bewusst akzeptierte Restrisiko-Lücke dokumentiert.
- Keine Überschneidung mit bereits erledigten Ergänzungen (`EntwicklungsprozessService`, `KiAusfuehrungsService`, `GitHubCopilotPlugin`).
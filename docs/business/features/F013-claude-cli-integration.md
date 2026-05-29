# F013 – Claude-CLI-Integration (Aufruf-Fix & Session-Wiederverwendung)

## Einleitung
Mit dieser Funktion nutzen Sie Claude als KI-Anbieter im normalen Aufgabenablauf.  
Der Aufruf-Fix verbessert die Stabilität bei langen Eingaben.  
Folgeanweisungen laufen in derselben Claude-Session weiter, damit der Kontext erhalten bleibt.

---

## Wer nutzt es?
- Fachanwender, die Aufgaben mit Claude iterativ bearbeiten.
- Teams, die neben Copilot auch Claude als produktives KI-Plugin einsetzen.

---

## Schritt-für-Schritt-Anleitung
1. In **Einstellungen** den **Anthropic API Key** eintragen und speichern.
2. Aufgabe öffnen und erste Anweisung über **🤖 KI starten** senden.
3. Folgeanweisungen in derselben Aufgabe senden.
4. Ergebnis im **📜 Protokoll** verfolgen.

---

## Was bedeutet „Aufruf-Fix & Session-Wiederverwendung“ fachlich?
- **Aufruf-Fix:** Sehr große Prompts werden stabil über einen Pipe-Weg an Claude übergeben statt als langes Inline-Argument.
- **Session-Wiederverwendung:** Folgeanweisungen nutzen dieselbe Aufgaben-Session, sodass Claude den Gesprächsverlauf fortführt.
- **Fehlerfall Session-Verlust:** Wenn eine Sitzung nicht mehr gefunden wird, startet das System automatisch einen neuen Erstlauf.

---

## Beispiel
Sie starten eine Aufgabe mit einer ersten Implementierungsanweisung.  
Danach senden Sie mehrere Folgeanweisungen zur Verfeinerung.  
Die Antworten bauen aufeinander auf, weil dieselbe Session weiterläuft.  
Falls die Session nicht mehr verfügbar ist, wird automatisch ein neuer Erstlauf gestartet.

---

## Häufige Fragen (FAQ)

**Muss ich den API-Key bei jeder Aufgabe neu eingeben?**  
Nein, nach dem Speichern bleibt er in den Einstellungen hinterlegt.

**Bleibt der Kontext bei Folgeanweisungen erhalten?**  
Ja, Folgeanweisungen werden in derselben Session weitergeführt.

**Was passiert bei „Session nicht gefunden“?**  
Der Lauf fällt automatisch auf einen neuen Erstlauf zurück.

**Wo sehe ich den Verlauf?**  
Im Aufgabenprotokoll der jeweiligen Aufgabe.

---

## Verwandte Funktionen & Nachweise
- [F003 – KI-Entwicklungsprozess](./F003-ki-entwicklungsprozess.md)
- [F011 – Agent-Auswahl bei Folgeanweisungen](./F011-agent-auswahl-bei-folgeanweisungen.md)
- [F012 – Kontextsteuerung bei Folgeanweisungen](./F012-kontextsteuerung-folgeanweisungen.md)
- [F014 – Standardplugin je Pluginart & KI-Plugin-Auswahl](./F014-standardplugin-ki-plugin-auswahl.md)
- [F015 – Einstellungen & Persistenz](./F015-einstellungen-und-persistenz.md)
- [F016 – Fehlerbehandlung & Recovery](./F016-fehlerbehandlung-und-recovery.md)
- [Requirements Analysis](../../requirements/claude-cli-integration-requirements-analysis.md)
- [Architektur-Blueprint](../../architecture/claude-cli-integration-architecture-blueprint.md)
- [Entity-Relationship-Model](../../architecture/claude-cli-integration-entity-relationship-model.md)
- [Architecture-Review](../../improvements/claude-cli-integration-architecture-review.md)
- [Planungsübersicht](../../planning-overview-claude-cli-integration.md)
- [Testplan Claude-CLI-Integration](../../tests/testplan-claude-cli-integration.md)
- [Testlücken Claude-CLI-Integration](../../tests/testluecken-claude-cli-integration.md)
- [Lifecycle Report Claude-CLI-Integration](../../lifecycle-report-claude-cli-integration.md)
- [Zurück zur Übersicht](../features.md)

---

## Hinweis zur Dokumentationsorchestrierung
Für diesen Lauf wurde die Agentendefinition aus `.github/agents/documentation-orchestrator.agent.md` verwendet, da `~/.copilot/agents/documentation-orchestrator.agent.md` in der Laufzeitumgebung nicht vorhanden war (siehe [Dokumentationsplan](../../documentation-plan.md)).

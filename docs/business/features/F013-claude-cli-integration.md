# F013 – Claude-CLI-Integration

## Einleitung

Mit dieser Funktion nutzen Sie Claude als zusätzliche KI in der Softwareschmiede.  
Sie hinterlegen Ihren Zugang einmal in den **Einstellungen**.  
Danach können Sie Aufgaben wie gewohnt starten und per KI bearbeiten.  
Die Funktion schafft mehr Auswahl für Teams mit unterschiedlichen KI-Vorgaben.  
So bleibt Ihr Arbeitsablauf gleich, auch wenn sich der KI-Anbieter ändert.

---

## Wer nutzt es?

Diese Funktion nutzen Fachanwender, die Aufgaben mit Claude bearbeiten möchten.  
Stakeholder nutzen sie, wenn mehrere KI-Anbieter im Betrieb vorgesehen sind.

---

## Schritt-für-Schritt-Anleitung

1. Sie öffnen die Seite **Einstellungen**.
2. Sie suchen die Karte **Claude CLI**.
3. Sie tragen im Feld **Anthropic API Key** Ihren gültigen Schlüssel ein.
4. Sie klicken auf **💾 Speichern**.
5. Sie öffnen eine Aufgabe und starten den Ablauf über **🚀 Starten**.
6. Sie geben im Bereich **💬 KI-Prompt** Ihren Auftrag ein.
7. Sie klicken auf **🤖 KI starten** und verfolgen die Ausgabe im **📜 Protokoll**.

---

## Beispiel

Sie erhalten eine Aufgabe zur Überarbeitung einer Rechnungsvorlage.  
Sie hinterlegen morgens den **Anthropic API Key** in **Einstellungen**.  
Danach starten Sie die Aufgabe und senden den ersten Prompt.  
Claude liefert den Vorschlag, und Sie prüfen das Ergebnis im Protokoll.

---

## Was passiert im Hintergrund?

Die Softwareschmiede speichert Ihren Schlüssel sicher im Windows-Anmeldespeicher.  
Beim Start eines KI-Laufs wird Claude automatisch mit diesem Schlüssel gestartet.  
Agentenpakete und Aufgabenablauf bleiben dabei unverändert nutzbar.

---

## Häufige Fragen (FAQ)

**Muss ich den Schlüssel bei jeder Aufgabe neu eingeben?**  
Nein. Nach dem Speichern bleibt der Wert hinterlegt.

**Kann ich die Claude-Einstellungen wieder entfernen?**  
Ja. Nutzen Sie in der Claude-Karte **↩️ Zurücksetzen**.

**Kann ich Claude ohne Schlüssel verwenden?**  
Nein. Ohne gültigen Schlüssel kann kein Claude-Lauf starten.

**Bleibt mein normaler Aufgabenablauf gleich?**  
Ja. Sie arbeiten weiter mit denselben Schritten und Ansichten.

**Wo sehe ich, was Claude gemacht hat?**  
Im Bereich **📜 Protokoll** der jeweiligen Aufgabe.

---

## Verwandte Funktionen

- [F003 – KI-Entwicklungsprozess](./F003-ki-entwicklungsprozess.md) – KI-Läufe im Alltag durchführen
- [F010 – Plugin-Prinzip für Integrationen](./F010-plugin-prinzip-integrationen.md) – Integrationen als austauschbare Bausteine verstehen
- [F011 – Agent-Auswahl bei Folgeanweisungen](./F011-agent-auswahl-bei-folgeanweisungen.md) – je Folgeanweisung den passenden Agenten wählen
- [F014 – Standardplugin je Pluginart & KI-Plugin-Auswahl](./F014-standardplugin-ki-plugin-auswahl.md) – Claude als Standard oder gezielte Prompt-Auswahl nutzen
- [F015 – Einstellungen & Persistenz](./F015-einstellungen-und-persistenz.md) – gespeicherte Einstellungen und deren Wirkung
- [F016 – Fehlerbehandlung & Recovery](./F016-fehlerbehandlung-und-recovery.md) – Vorgehen bei ungültigen Schlüsseln und Laufproblemen
- [Anforderungsanalyse](../../requirements/requirements-analysis.md) – fachliche Ziele und Rahmenbedingungen
- [Architektur-Blueprint](../../architecture/architecture-blueprint.md) – Gesamtaufbau der Lösung
- [Architektur-Review](../../improvements/architecture-review.md) – geprüfte Verbesserungen und Risiken
- [Testplan Claude-CLI-Integration](../../tests/testplan-claude-cli-integration.md) – geprüfte Testschritte und Abnahme
- [Testlücken Claude-CLI-Integration](../../tests/testluecken-claude-cli-integration.md) – dokumentierter Stand der Testabdeckung
- [Zurück zur Übersicht](../features.md)

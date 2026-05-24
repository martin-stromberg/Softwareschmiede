# F014 – Standardplugin je Pluginart & KI-Plugin-Auswahl

## Einleitung

Mit dieser Funktion legen Sie je Pluginart ein bevorzugtes Plugin fest.
Beim Senden eines Prompts wählen Sie zusätzlich das konkrete KI-Plugin für genau diesen Lauf.
So arbeiten Teams schneller mit sinnvollen Vorgaben und behalten trotzdem die volle Kontrolle.

---

## Wer nutzt es?

Diese Funktion nutzen Fachanwender in den **Einstellungen** und in der **Aufgabendetailseite**.
Stakeholder nutzen sie, um einen einheitlichen und nachvollziehbaren KI-Betrieb sicherzustellen.

---

## Schritt-für-Schritt-Anleitung

1. Sie öffnen **Einstellungen**.
2. Sie wählen für die Pluginart **KI** und bei Bedarf **SCM** jeweils ein Standardplugin.
3. Sie klicken auf **Speichern**.
4. Sie öffnen eine Aufgabe und tragen im Bereich **KI-Prompt** Ihre Anweisung ein.
5. Sie prüfen die Plugin-Auswahl: Das gespeicherte Standard-KI-Plugin ist bereits vorausgewählt.
6. Bei Bedarf wählen Sie ein anderes KI-Plugin.
7. Sie senden den Prompt.

---

## Beispiel

Ihr Team verwendet im Alltag „Copilot“ als Standard-KI, möchte aber für einzelne Aufgaben „Claude“ nutzen.
In den Einstellungen bleibt „Copilot“ als Standard hinterlegt.
Bei einer speziellen Aufgabe wählen Sie vor dem Senden des Prompts einmalig „Claude“.
Genau dieses Plugin führt dann den Prompt aus.
Beim nächsten Prompt ist wieder der Standard vorausgewählt.

---

## Was passiert im Hintergrund?

Die Softwareschmiede nutzt eine feste Reihenfolge:

1. **Explizite Auswahl im Prompt**
2. **Gespeicherte Plugin-Auswahl der Aufgabe (`KiPluginPrefix`)**
3. **Gespeichertes Standardplugin**
4. **Automatischer Fallback auf ein verfügbares Plugin**

Gespeichert wird die technische Plugin-Kennung.
Wenn ein gespeicherter Aufgabenwert oder das gespeicherte Standardplugin nicht verfügbar ist, bleibt der Ablauf trotzdem arbeitsfähig und nutzt den Fallback.

---

## Häufige Fragen (FAQ)

**Muss ich bei jedem Prompt ein Plugin neu auswählen?**  
Nein. Das Standardplugin ist bereits vorausgewählt.

**Kann ich das Plugin pro Prompt übersteuern?**  
Ja. Sie können vor jedem Senden ein anderes verfügbares KI-Plugin auswählen.

**Was passiert, wenn mein Standardplugin gerade nicht verfügbar ist?**  
Dann nutzt die Anwendung automatisch ein verfügbares Fallback-Plugin.

**Bleibt meine Auswahl für spätere Prompts erhalten?**  
Die gespeicherte Standardauswahl bleibt erhalten. Eine manuelle Auswahl gilt für den jeweiligen Prompt.

---

## Verwandte Funktionen

- [F003 – KI-Entwicklungsprozess](./F003-ki-entwicklungsprozess.md) – Prompts senden und Ergebnisse verfolgen
- [F009 – Arbeitsverzeichnis konfigurieren](./F009-arbeitsverzeichnis-konfigurieren.md) – zentrale Laufzeit-Einstellungen verstehen
- [F010 – Plugin-Prinzip für Integrationen](./F010-plugin-prinzip-integrationen.md) – Plugin-Architektur und Verfügbarkeit
- [F017 – Lokales Verzeichnis Plugin](./F017-lokales-verzeichnis-plugin.md) – SCM-Alternative ohne Remote-Provider
- [F013 – Claude-CLI-Integration](./F013-claude-cli-integration.md) – zusätzlichen KI-Anbieter einbinden
- [F015 – Einstellungen & Persistenz](./F015-einstellungen-und-persistenz.md) – gespeicherte Werte und Gültigkeit
- [F016 – Fehlerbehandlung & Recovery](./F016-fehlerbehandlung-und-recovery.md) – sicheres Vorgehen bei Störungen
- [F026 – KI-Plugin-spezifische Agenten-Discovery und -Auswahl](./F026-ki-plugin-spezifische-agenten-discovery-auswahl.md) – durchgängiger Auswahlfluss in Aufgabe/Prompt
- [Flow: Plugin-Default-Auswahl](../../flows/plugin-default-selection-flow.md) – fachlicher Ablauf der Auflösung
- [API/Technik: Plugin-Default-Auswahl](../../api/plugin-default-selection.md) – technische Vertragsdetails
- [Zurück zur Übersicht](../features.md)

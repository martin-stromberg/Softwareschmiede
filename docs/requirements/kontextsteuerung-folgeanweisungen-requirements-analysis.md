# Anforderungsanalyse – Kontextsteuerung bei Folgeanweisungen

> **Dokument-Typ:** Requirements Analysis  
> **Status:** 📋 Geplant  
> **Version:** 1.1.0  
> **Thema:** Optionale Kontextanreicherung für Folgeanweisungen über `{id}.copilot.context.md`

---

## 1. Überblick und Projektkontext

### 1.1 Projektbeschreibung
Das Feature erweitert den Folgeanweisungs-Dialog um eine steuerbare Kontextstrategie. Anwender entscheiden pro Folgeanweisung, ob historischer Aufgabenkontext aus `{id}.copilot.context.md` mitgegeben, ignoriert oder neu begonnen wird.

### 1.2 Geschäftsziele
| # | Ziel | Messbare Erfolgsgröße |
|---|---|---|
| Z-1 | Höhere Steuerbarkeit des KI-Dialogs | 100 % der Folgeanweisungen nutzen explizit eine der 3 Kontextoptionen |
| Z-2 | Bessere Antwortqualität durch relevanten Verlauf | Kontext wird vor der Nutzeranweisung injiziert und ist in der Anweisungsdatei nachvollziehbar |
| Z-3 | Stabilität bei langen Konversationen | Bei Überschreitung definierter Kontextgrenzen wird Komprimierung ausgelöst |

### 1.3 Stakeholder
| Rolle | Beschreibung | Interesse |
|---|---|---|
| Anwender | Steuert Folgeanweisungen in der Aufgabendetailseite | Präzise Kontrolle über Kontextumfang |
| Entwicklungsteam | Implementiert UI-, Datei- und Agentenfluss | Klare Regeln für Persistenz, Reihenfolge und Komprimierung |
| Product Owner | Priorisiert Dialog- und UX-Verhalten | Vorhersagbare und verständliche Nutzerführung |

### 1.4 Abgrenzung
- Fokus auf Folgeanweisungen (nicht Initialprompt).
- Fokus auf Kontextdatei `{id}.copilot.context.md` je Aufgabe.
- Keine Änderung öffentlicher HTTP-APIs im Rahmen dieser Anforderung.

---

## 2. Funktionale Anforderungen

| Kennung | Beschreibung | Kategorie | Priorität | Status |
|---------|--------------|-----------|-----------|--------|
| **FR-1** | **Persistenz von Dialogkontext:** Anfragen und Rückmeldungen werden pro Aufgabe in `{id}.copilot.context.md` gespeichert; Schreibvorgang erfolgt nach jedem Folgeanweisungszyklus konsistent. → [Architektur-Blueprint](../architecture/kontextsteuerung-folgeanweisungen-architecture-blueprint.md) · [ERM](../architecture/kontextsteuerung-folgeanweisungen-entity-relationship-model.md) | Datenverwaltung | MUST HAVE | 📋 Geplant |
| **FR-1.1** | **Kontextdatei je Aufgabeninstanz:** Dateiname basiert auf Aufgaben-ID und ist eindeutig (`{id}.copilot.context.md`). | Datenverwaltung | MUST HAVE | 📋 Geplant |
| **FR-1.2** | **Bidirektionale Verlaufsaufnahme:** Sowohl Nutzeranfrage als auch KI-Rückmeldung werden strukturiert in derselben Kontextdatei fortgeschrieben. | KI-Integration | MUST HAVE | 📋 Geplant |
| **FR-2** | **Prompt-Anreicherung mit Kontextpräfix:** Inhalt der Kontextdatei wird vor der vom Anwender eingegebenen Folgeanweisung in die aktuelle Anweisungsdatei geschrieben. → [Architektur-Blueprint](../architecture/kontextsteuerung-folgeanweisungen-architecture-blueprint.md) · [Ablaufdokument](../flows/development-process-flow.md) | KI-Integration | MUST HAVE | 📋 Geplant |
| **FR-2.1** | **Definierte Reihenfolge:** Reihenfolge ist strikt `Kontextinhalt` → `Nutzeranweisung`; keine Vermischung oder Umkehr. | KI-Integration | MUST HAVE | 📋 Geplant |
| **FR-3** | **Kontextkomprimierung bei Größenlimit:** Bei zu großer Kontextdatei wird der KI-Agent zur inhaltlichen Komprimierung aufgefordert; essenzielle Informationen bleiben erhalten. → [Architecture Review](../improvements/kontextsteuerung-folgeanweisungen-architecture-review.md) | Zuverlässigkeit | MUST HAVE | 📋 Geplant |
| **FR-3.1** | **Triggerbare Verdichtung:** Überschreitung eines konfigurierbaren Schwellwerts (z. B. Zeichen/Token) startet automatisch die Komprimierungsroutine. | Datenverwaltung | HIGH | 📋 Geplant |
| **FR-3.2** | **Informationskern-Erhalt:** Komprimierte Fassung enthält weiterhin Ziel, offene Punkte, Entscheidungen und relevante Randbedingungen. | KI-Integration | MUST HAVE | 📋 Geplant |
| **FR-4** | **UI-Kontextmodus für Folgeanweisung:** Dialog-Panel enthält exakt drei Auswahloptionen: „Kontext mitgeben“, „Kontext ignorieren“, „Kontext neu beginnen“. → [Architecture Blueprint](../architecture/kontextsteuerung-folgeanweisungen-architecture-blueprint.md) · [Architecture Review](../improvements/kontextsteuerung-folgeanweisungen-architecture-review.md) | UX / Accessibility | MUST HAVE | 📋 Geplant |
| **FR-4.1** | **Exakte Optionswerte:** Die drei Werte sind ohne zusätzliche Option verfügbar und textlich exakt benannt. | UX / Accessibility | MUST HAVE | 📋 Geplant |
| **FR-4.2** | **Verhaltensmapping je Option:** „mitgeben“ nutzt aktuellen Kontext, „ignorieren“ sendet ohne Kontextpräfix, „neu beginnen“ startet neuen Kontextverlauf für die Aufgabe. | Kern-Feature | MUST HAVE | 📋 Geplant |

---

## 3. Nicht-funktionale Anforderungen

| Kennung | Beschreibung | Kategorie | Priorität | Status |
|---------|--------------|-----------|-----------|--------|
| **NFR-1** | **Performance der Prompt-Vorbereitung:** Aufbau der finalen Anweisung (inkl. Kontextmodus-Entscheidung) erfolgt in < 500 ms im Median je Folgeanweisung. → [Architektur-Blueprint](../architecture/kontextsteuerung-folgeanweisungen-architecture-blueprint.md) | Performance | HIGH | 📋 Geplant |
| **NFR-2** | **Robustheit bei großen Verläufen:** Komprimierung verhindert Überschreiten praktischer LLM-Kontextgrenzen in mindestens 99 % der Folgeanweisungen mit Langverlauf. → [Architektur-Blueprint](../architecture/kontextsteuerung-folgeanweisungen-architecture-blueprint.md) · [Architecture Review](../improvements/kontextsteuerung-folgeanweisungen-architecture-review.md) | Skalierbarkeit | MUST HAVE | 📋 Geplant |
| **NFR-3** | **Nachvollziehbarkeit:** Gewählter Kontextmodus und Komprimierungsereignisse sind im Aufgabenprotokoll auditierbar dokumentiert. → [ERM](../architecture/kontextsteuerung-folgeanweisungen-entity-relationship-model.md) · [Architecture Review](../improvements/kontextsteuerung-folgeanweisungen-architecture-review.md) | Wartbarkeit | HIGH | 📋 Geplant |
| **NFR-4** | **Datenschutz & Sicherheit:** Kontextdateien enthalten keine zusätzlichen Secrets außerhalb bereits bestehender Prompt-/Antwortinhalte; keine Klartext-Tokens im Persistenzpfad. → [Architektur-Blueprint](../architecture/kontextsteuerung-folgeanweisungen-architecture-blueprint.md) · [Architecture Review](../improvements/kontextsteuerung-folgeanweisungen-architecture-review.md) | Sicherheit | MUST HAVE | 📋 Geplant |
| **NFR-5** | **Usability-Klarheit:** Kontextoptionen sind semantisch eindeutig und ohne Zusatzwissen interpretierbar (verständliche Beschriftung, keine versteckten Zustände). → [Architektur-Blueprint](../architecture/kontextsteuerung-folgeanweisungen-architecture-blueprint.md) · [Architecture Review](../improvements/kontextsteuerung-folgeanweisungen-architecture-review.md) | UX / Accessibility | HIGH | 📋 Geplant |

---

## 4. Akzeptanzkriterien

### User Story US-1 – Verlauf persistent führen
**Als** Anwender  
**möchte ich**, dass Folgeanfragen und KI-Rückmeldungen in einer Kontextdatei gespeichert werden,  
**damit** spätere Folgeanweisungen auf den bisherigen Verlauf zugreifen können.

- AC-1.1: Nach jeder Folgeanweisung enthält `{id}.copilot.context.md` den neuen Nutzerbeitrag und die zugehörige KI-Rückmeldung.
- AC-1.2: Der Dateiname folgt exakt dem Muster `{id}.copilot.context.md`.

### User Story US-2 – Kontext geordnet voranstellen
**Als** Anwender  
**möchte ich**, dass vorhandener Kontext vor meiner neuen Anweisung eingefügt wird,  
**damit** der Agent den Verlauf zuerst verarbeitet.

- AC-2.1: Bei Modus „Kontext mitgeben“ steht der Dateiinhalt von `{id}.copilot.context.md` vor der Nutzeranweisung in der aktuellen Anweisungsdatei.
- AC-2.2: Die Reihenfolge bleibt strikt erhalten (Kontext vor Anweisung).

### User Story US-3 – Kontext bei Größe verdichten
**Als** Anwender  
**möchte ich**, dass zu große Kontextdateien automatisch komprimiert werden,  
**damit** der Agent mit wesentlichen Informationen weiterarbeiten kann.

- AC-3.1: Bei Überschreitung des Schwellwerts wird ein Komprimierungsauftrag ausgelöst.
- AC-3.2: Die komprimierte Fassung enthält mindestens: Ziel der Aufgabe, offene Punkte, letzte Entscheidungen.

### User Story US-4 – Kontextmodus explizit steuern
**Als** Anwender  
**möchte ich** im Folgeanweisungsdialog genau drei Kontextoptionen auswählen können,  
**damit** ich den Kontext je Folgeanweisung bewusst steuere.

- AC-4.1: Das Dialog-Panel zeigt exakt die drei Optionen „Kontext mitgeben“, „Kontext ignorieren“, „Kontext neu beginnen“.
- AC-4.2: Keine weitere vierte Option ist verfügbar.
- AC-4.3: Die drei Optionen führen jeweils zum definierten Verhalten (mitgeben/ignorieren/neu beginnen).

---

## 5. Annahmen und Abhängigkeiten

| Typ | Eintrag | Auswirkung |
|---|---|---|
| Annahme | Aufgaben-ID ist beim Folgeanweisungsfluss immer verfügbar. | Ohne ID kann kein eindeutiger Kontextdateipfad erzeugt werden. |
| Annahme | Der KI-Agent unterstützt inhaltliche Zusammenfassung auf Anweisung. | Ohne Summarization-Fähigkeit steigt Risiko auf Kontextüberlauf. |
| Abhängigkeit | Folgeanweisungs-UI (`AufgabeDetail`) erlaubt Erweiterung um Modus-Auswahl. | UI-Anpassung ist Voraussetzung für steuerbares Verhalten. |
| Abhängigkeit | Prompt-Erstellungspfad kann Kontextpräfix deterministisch einfügen. | Ohne Hook ist die Reihenfolge Kontext→Anweisung nicht garantierbar. |
| Abhängigkeit | Protokoll-/Dateischreibpfad besitzt Schreibrechte im Aufgabenarbeitsverzeichnis. | Ohne Schreibrechte scheitert Persistenz der Kontextdatei. |

---

## 6. Scope und Out-of-Scope

**In-Scope ✅**
- Persistenz von Folgeanfragen und Antworten in `{id}.copilot.context.md`
- Kontextpräfix vor Nutzeranweisung in der Anweisungsdatei
- Automatische Kontextkomprimierung bei Größenlimit
- UI-Auswahl mit genau drei Kontextoptionen und zugehörigem Verhalten

**Out-of-Scope ❌**
- Änderung des Initialprompt-Verhaltens beim ersten KI-Start
- Neue öffentliche API-Endpunkte
- Historische Migration alter Aufgabenkontexte in neues Dateiformat
- Erweiterung auf mehr als die drei definierten UI-Optionen

---

## 7. Domänenmodell und Glossar

```mermaid
classDiagram
    class TaskContextFile {
      +taskId
      +path: {id}.copilot.context.md
      +append(entry)
      +compress()
    }
    class FollowUpInstruction {
      +userPrompt
      +contextMode
      +buildInstructionFile()
    }
    class ContextMode {
      <<enumeration>>
      KontextMitgeben
      KontextIgnorieren
      KontextNeuBeginnen
    }
    class CompressionCommand {
      +threshold
      +retainCoreFacts()
    }

    FollowUpInstruction --> ContextMode : uses
    FollowUpInstruction --> TaskContextFile : reads/writes
    TaskContextFile --> CompressionCommand : triggers on limit
```

**Glossar**
- **TaskContextFile:** Aufgabenbezogene Verlaufsdatei `{id}.copilot.context.md`.
- **Context Mode:** Vom Anwender gewähltes Verhalten für Kontextnutzung in der nächsten Folgeanweisung.
- **Kontextkomprimierung:** KI-gestützte Verdichtung eines zu langen Verlaufs auf Kerninformationen.
- **Anweisungsdatei:** Laufbezogene Datei, die final an den Agenten übergeben wird (Kontextpräfix + Nutzeranweisung).

---

## 8. Nutzungsfälle (Use Cases)

### UC-1: Folgeanweisung mit bestehendem Kontext senden
| Feld | Inhalt |
|---|---|
| **ID** | UC-1 |
| **Akteur** | Anwender |
| **Vorbedingung** | `{id}.copilot.context.md` existiert und enthält Verlauf. |
| **Auslöser** | Anwender wählt „Kontext mitgeben“ und sendet Folgeanweisung. |
| **Hauptszenario** | 1) System lädt Kontextdatei. 2) System schreibt Kontext vor Nutzeranweisung in die Anweisungsdatei. 3) Agent wird gestartet. 4) Anfrage+Antwort werden in Kontextdatei ergänzt. |
| **Nachbedingung** | Kontextdatei ist um einen Interaktionsblock erweitert. |
| **Anforderungen** | FR-1, FR-2, FR-4.2 |

### UC-2: Folgeanweisung ohne Kontext senden
| Feld | Inhalt |
|---|---|
| **ID** | UC-2 |
| **Akteur** | Anwender |
| **Vorbedingung** | Folgeanweisungsdialog ist geöffnet. |
| **Auslöser** | Anwender wählt „Kontext ignorieren“ und sendet Folgeanweisung. |
| **Hauptszenario** | 1) System überspringt Kontextpräfix. 2) Nur Nutzeranweisung wird in die Anweisungsdatei geschrieben. 3) Agent wird gestartet. |
| **Nachbedingung** | Lauf erfolgt ohne vorangestellten Alt-Kontext. |
| **Anforderungen** | FR-2, FR-4.2 |

### UC-3: Folgeanweisung mit neuem Kontext beginnen
| Feld | Inhalt |
|---|---|
| **ID** | UC-3 |
| **Akteur** | Anwender |
| **Vorbedingung** | Bestehender Verlauf kann vorhanden sein. |
| **Auslöser** | Anwender wählt „Kontext neu beginnen“. |
| **Hauptszenario** | 1) System verwirft bisherigen Kontext für den aktuellen Lauf. 2) Neue Anweisungsdatei startet ohne Alt-Kontext. 3) Nach Rückmeldung beginnt neuer Kontextverlauf in `{id}.copilot.context.md`. |
| **Nachbedingung** | Kontextdatei repräsentiert den neuen Verlauf ab Reset-Zeitpunkt. |
| **Anforderungen** | FR-1, FR-4.2 |

### UC-4: Kontextgröße überschreitet Grenzwert
| Feld | Inhalt |
|---|---|
| **ID** | UC-4 |
| **Akteur** | System |
| **Vorbedingung** | Kontextdatei überschreitet definierten Grenzwert. |
| **Auslöser** | Nächste Folgeanweisung wird vorbereitet. |
| **Hauptszenario** | 1) System erkennt Überschreitung. 2) Komprimierungsauftrag an Agenten. 3) Komprimierte Kernfassung ersetzt/aktualisiert Kontextdatei. 4) Lauf setzt mit komprimiertem Kontext fort. |
| **Nachbedingung** | Kontext bleibt nutzbar innerhalb zulässiger Größe. |
| **Anforderungen** | FR-3, FR-3.1, FR-3.2, NFR-2 |

---

## 9. Nächste Schritte

1. Architektur-Blueprint für Kontextdatei-Lifecycle und Prompt-Building erstellen.  
2. ERM/Dateimodell für Kontextartefakte und Komprimierungsmetadaten konkretisieren.  
3. UI-Spezifikation für die drei Kontextoptionen (inkl. exakter Labels) finalisieren.  
4. Testkonzept (Unit + Integrationsszenarien für die 4 Akzeptanzkriterien-Blöcke) ausarbeiten.  
5. Architektur-Review mit Fokus auf Größenlimits, Datenverlust-Risiko und UX-Klarheit durchführen.

---

## 10. Approval & Versionierung

### Freigabe
| Rolle | Name | Status | Datum |
|---|---|---|---|
| Product Owner | _ausstehend_ | ⏳ Ausstehend | — |
| Architektur | _ausstehend_ | ⏳ Ausstehend | — |
| Autor | GitHub Copilot Agent | ✅ Erstellt | 2026-05-11 |

### Versionshistorie
| Version | Datum | Autor | Änderung |
|---|---|---|---|
| 1.1.0 | 2026-05-11 | GitHub Copilot Agent | NFR-Hauptanforderungen um Links zu Architektur/ERM/Review ergänzt und konsistent geschärft |
| 1.0.0 | 2026-05-11 | GitHub Copilot Agent | Initiale Anforderungsanalyse für Feature „Kontextsteuerung bei Folgeanweisungen“ erstellt |


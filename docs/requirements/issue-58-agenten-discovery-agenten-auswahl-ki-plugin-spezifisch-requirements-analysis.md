# Anforderungsanalyse – Issue 58: Agenten-Discovery und Agenten-Auswahl KI-Plugin-spezifisch

> **Dokument-Typ:** Anforderungsanalyse  
> **Projekt:** Softwareschmiede  
> **Anforderungsquelle:** Issue 58 (Agenten-Discovery und Agenten-Auswahl KI-Plugin-spezifisch umsetzen)  
> **Status:** 📋 Geplant  
> **Version:** 1.0.0

---

## 1) Überblick und Projektkontext

Die Agenten-Auswahl wird plugin-spezifisch umgesetzt. Das ausgewählte KI-Plugin steuert:
- welche Agentenpakete kompatibel sind,
- welche Agenten angezeigt werden,
- welches Plugin in Start-/Prompt-/Folgeprompt-Flows verwendet wird.

Zusätzlich wird die Plugin-Auswahl pro Aufgabe in `Aufgabe.KiPluginPrefix` persistent geführt.

### Explizite Entscheidungen (Pflichtklärungen)

1. **Default ohne gespeichertes Plugin**  
   Reihenfolge: `explizite UI-Auswahl` → `Aufgabe.KiPluginPrefix` → `gespeichertes Default-KI-Plugin` → `deterministischer Fallback`.
2. **Keine kompatiblen Pakete/Agenten**  
   UI zeigt leeren Zustand mit Hinweis; Start/Senden sind deaktiviert; kein stilles Ausweichen auf inkompatible Pakete.
3. **Rückwärtskompatibilität ohne `KiPluginPrefix`**  
   `KiPluginPrefix` bleibt nullable; bestehende Aufgaben bleiben über die Fallback-Kette ausführbar.

---

## 2) Funktionale Anforderungen

| Kennung | Beschreibung | Kategorie | Priorität | Status |
|---|---|---|---|---|
| **FR-1** | **Plugin-spezifische Discovery:** Das gewählte KI-Plugin bestimmt kompatible Agentenpakete und verfügbare Agenten. Nur kompatible Pakete werden angeboten. → [Architektur-Blueprint](../architecture/issue-58-agenten-discovery-agenten-auswahl-ki-plugin-spezifisch-architecture-blueprint.md) | KI-Integration | MUST HAVE | 📋 Geplant |
| **FR-2** | **Verbindliche UI-Reihenfolge:** Reihenfolge ist in allen relevanten UIs `KI-Plugin → Agentenpaket → Agent`; bei Pluginwechsel werden abhängige Selektionen zurückgesetzt. → [Architektur-Blueprint](../architecture/issue-58-agenten-discovery-agenten-auswahl-ki-plugin-spezifisch-architecture-blueprint.md) | UX / Accessibility | MUST HAVE | 📋 Geplant |
| **FR-3** | **Persistenz pro Aufgabe:** Das ausgewählte KI-Plugin wird pro Aufgabe als `KiPluginPrefix` gespeichert und wiederverwendet. → [ERM](../architecture/issue-58-agenten-discovery-agenten-auswahl-ki-plugin-spezifisch-entity-relationship-model.md) | Datenverwaltung | MUST HAVE | 📋 Geplant |
| **FR-4** | **DB-Migration absichern:** Migration für `KiPluginPrefix` ist Bestandteil der Umsetzung und bleibt rückwärtskompatibel (nullable). → [ERM](../architecture/issue-58-agenten-discovery-agenten-auswahl-ki-plugin-spezifisch-entity-relationship-model.md) | Datenverwaltung | MUST HAVE | 📋 Geplant |
| **FR-5** | **Prompt-/Folgeprompt-Ausrichtung:** Initialprompt und Folgeprompt nutzen dieselbe Plugin-Auflösung und denselben Selektionskontext. → [Architektur-Blueprint](../architecture/issue-58-agenten-discovery-agenten-auswahl-ki-plugin-spezifisch-architecture-blueprint.md) | KI-Integration | MUST HAVE | 📋 Geplant |
| **FR-6** | **Legacy-Discovery entfernen:** Plugin-unabhängige Discovery-Pfade werden entfernt; die Agenten-Erkennung liegt bei den KI-Plugins. → [Architektur-Blueprint](../architecture/issue-58-agenten-discovery-agenten-auswahl-ki-plugin-spezifisch-architecture-blueprint.md) | Wartbarkeit | HIGH | 📋 Geplant |
| **FR-7** | **Tests aktualisieren:** Unit-, bUnit- und Integrationstests decken Discovery, UI-Reihenfolge, Fallbacks, Persistenz und Fehlerfälle ab. → [Architektur-Review](../improvements/issue-58-agenten-discovery-agenten-auswahl-ki-plugin-spezifisch-architecture-review.md) | Qualitätssicherung | MUST HAVE | 📋 Geplant |

---

## 3) Nicht-funktionale Anforderungen

| Kennung | Beschreibung | Kategorie | Priorität | Status |
|---|---|---|---|---|
| **NFR-1** | **Robustheit:** Fehlende kompatible Pakete/Agenten führen zu verständlichem, kontrolliertem UI-Fehlerzustand statt Laufzeitabbruch. | Zuverlässigkeit | MUST HAVE | 📋 Geplant |
| **NFR-2** | **Determinismus:** Plugin-Auflösung ist reproduzierbar und zentral implementiert. | Wartbarkeit | MUST HAVE | 📋 Geplant |
| **NFR-3** | **Rückwärtskompatibilität:** Bestehende Aufgaben ohne `KiPluginPrefix` bleiben ohne Datenmigration nutzbar. | Kompatibilität | MUST HAVE | 📋 Geplant |
| **NFR-4** | **Konsistenz:** Startflow und Folgeprompt-Flow verwenden identische Selektionsregeln. | Korrektheit | MUST HAVE | 📋 Geplant |
| **NFR-5** | **Testbarkeit:** Kritische Pfade (Default/Fallback/No-Compat/Legacy-Entfernung) sind automatisiert abgesichert. | Qualitätssicherung | HIGH | 📋 Geplant |

---

## 4) Akzeptanzkriterien

1. Ein Nutzer wählt zuerst das KI-Plugin; die Paket- und Agentenlisten werden plugin-spezifisch aktualisiert.
2. Beim Wechsel des KI-Plugins werden inkompatible/selektierte Paket-/Agent-Werte zurückgesetzt.
3. Das gewählte KI-Plugin wird pro Aufgabe in `KiPluginPrefix` gespeichert und beim Wiederöffnen berücksichtigt.
4. Startprompt und Folgeprompt verwenden dieselbe aufgelöste Plugin-Auswahl.
5. Ohne kompatible Pakete/Agenten sind Aktionen deaktiviert und ein klarer Hinweis wird angezeigt.
6. Aufgaben ohne `KiPluginPrefix` funktionieren weiterhin über definierte Fallback-Auflösung.
7. Alte plugin-unabhängige Discovery-Pfade sind entfernt und Tests entsprechend angepasst.

---

## 5) Annahmen und Abhängigkeiten

| Typ | Eintrag | Auswirkung |
|---|---|---|
| Annahme | `IKiPlugin` liefert `GetAvailableAgentsAsync` und `IsAgentPackageCompatibleAsync` plugin-spezifisch. | Discovery kann zentral pro Plugin erfolgen. |
| Annahme | `PluginSelectionService` bleibt zentrale Stelle der Auflösung. | Einheitliches Default-/Fallback-Verhalten. |
| Abhängigkeit | DB enthält (oder erhält) nullable `Aufgabe.KiPluginPrefix`. | Persistenz pro Aufgabe möglich ohne Breaking Change. |
| Abhängigkeit | UI für Start und Folgeprompt ist gemeinsam konsolidierbar. | Einheitliche Reihenfolge und Zustandslogik. |
| Risiko | Teilweise doppelte Auswahlpfade im UI führen zu Inkonsistenzen. | Bedarf für Refactoring + zusätzliche bUnit-Regressionen. |

---

## 6) Scope und Out-of-Scope

### In Scope ✅
- KI-Plugin-spezifische Agenten-Discovery
- UI-Reihenfolge und Zustandslogik `Plugin → Paket → Agent`
- Persistenz `KiPluginPrefix` pro Aufgabe inkl. Migrationseinbindung
- Harmonisierung von Start-/Prompt-/Folgeprompt-Flows
- Entfernen legacy Discovery-Pfade
- Anpassung/Erweiterung automatischer Tests

### Out-of-Scope ❌
- Einführung neuer KI-Plugins
- Plugin-Marketplace/Installation
- Multi-Dispatch an mehrere KI-Plugins gleichzeitig

---

## 7) Domänenmodell und Glossar

- **Aufgabe**: enthält `AgentenpaketName`, `AgentenName`, `KiPluginPrefix`.
- **KI-Plugin (`IKiPlugin`)**: bestimmt Kompatibilität und verfügbare Agenten je Paket.
- **Agentenpaket**: Dateisystemordner mit plugin-spezifischen Agentendefinitionen.
- **PluginSelectionService**: löst effektives KI-Plugin über feste Prioritätskette auf.

---

## 8) Nutzungsfälle (Use Cases)

### UC-1: Entwicklungsprozess starten
1. Nutzer öffnet Startdialog.
2. Nutzer wählt KI-Plugin.
3. System zeigt kompatible Pakete und Agenten.
4. Nutzer startet Prozess; `KiPluginPrefix` wird gespeichert.

### UC-2: Folgeprompt senden
1. System lädt vorhandenen Selektionskontext (`KiPluginPrefix` oder Fallback).
2. Nutzer sendet Folgeprompt.
3. Ausführung verwendet dasselbe aufgelöste KI-Plugin.

### UC-3: Bestandsaufgabe ohne Prefix
1. Aufgabe hat `KiPluginPrefix = null`.
2. System löst Plugin über Default/Fallback auf.
3. Aufgabe bleibt ausführbar, ohne Datenverlust.

---

## 9) Nächste Schritte

1. Architektur-/ERM-Dokumente und Review finalisieren.
2. UI-Zustandslogik und Reihenfolge konsolidieren.
3. Discovery-Altpfade entfernen.
4. Testmatrix (Unit/bUnit/Integration) aktualisieren und vollständig grün ziehen.

---

## 10) Approval & Versionierung

| Version | Datum | Autor | Änderung |
|---|---|---|---|
| 1.0.0 | 2026-05-24 | planning-orchestrator | Initiale Anforderungsanalyse für Issue 58 |


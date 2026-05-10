# Planungsübersicht – Plugin-Klassenbibliotheken für GitHub und GitHub Copilot

> **Dokument-Typ:** Orchestrator-Konsolidierung  
> **Projekt:** Softwareschmiede  
> **Feature:** Plugin-Prinzip mit ausgelagerter GitHub-/Copilot-Anbindung  
> **Status:** ✅ Planungsphase abgeschlossen (mit Auflagen aus Architektur-Review)  
> **Primärquelle:** [`../.copilot-task.md`](../.copilot-task.md)

---

## 1. Zusammenfassung

Für die verbindliche Anforderung wurde der vollständige Planungsablauf durchgeführt:

1. **Anforderungsanalyse** (Ziele, Scope, FR/NFR, ACs, Domänenmodell)
2. **Architektur-Blueprint** (Plugin-Architektur, `PluginManager`, Build-/Deploy-Strategie)
3. **Entity-Relationship-Modell** (Plugin-Discovery-/Typisierungsmodell)
4. **Architektur-Review** (priorisierte Findings und Maßnahmen)

Kernziel ist die Ablösung der bisherigen Hard-Wiring-Implementierungen durch ein Plugin-Prinzip mit zwei ausgelagerten Klassenbibliotheken unter `plugins/` und automatischer Bereitstellung der DLLs nach `<Programmverzeichnis>/plugins`.

---

## 2. Erzeugte Planungsdokumente

| Bereich | Dokument | Link |
|---|---|---|
| Requirements | Anforderungsanalyse | [requirements/plugin-klassenbibliotheken-github-und-copilot.md](requirements/plugin-klassenbibliotheken-github-und-copilot.md) |
| Architecture | Architektur-Blueprint | [architecture/plugin-klassenbibliotheken-github-und-copilot-architecture-blueprint.md](architecture/plugin-klassenbibliotheken-github-und-copilot-architecture-blueprint.md) |
| Architecture | Entity-Relationship-Modell | [architecture/plugin-klassenbibliotheken-github-und-copilot-entity-relationship-model.md](architecture/plugin-klassenbibliotheken-github-und-copilot-entity-relationship-model.md) |
| Improvements | Architektur-Review | [improvements/plugin-klassenbibliotheken-github-und-copilot-architecture-review.md](improvements/plugin-klassenbibliotheken-github-und-copilot-architecture-review.md) |

---

## 3. Konsolidierte Architekturentscheidungen

- GitHub- und GitHub-Copilot-Anbindung werden in **separate Plugin-Klassenbibliotheken** ausgelagert.
- Plugin-Projekte liegen im Repository unter **`plugins/`**.
- Die Hauptanwendung nutzt einen **`PluginManager`** für automatische Discovery und Typzuordnung:
  - `Source Code Management`
  - `Development Automation`
- Discovery-Zielpfad ist zur Laufzeit **`<Programmverzeichnis>/plugins`**.
- Der Solution-Build stellt die Plugin-DLLs automatisch im Host-Plugin-Ordner bereit.

---

## 4. Review-Ergebnis (Kurzfassung)

Die Zielrichtung ist korrekt, jedoch mit Auflagen:

- **Blocker:** Vertrags-/Typisierungsmodell eindeutig festlegen und dokumentübergreifend synchronisieren.
- **Major:** Trust-Modell für Plugin-DLLs ergänzen (Allowlist/Signatur/Hash).
- **Major:** Build-/Publish-Mechanik verbindlich und CI-prüfbar spezifizieren.
- **Major:** AssemblyLoadContext-/Dependency-Ladestrategie konkretisieren.

---

## 5. Nächste Umsetzungsschritte

1. Blocker/Major-Findings aus dem Architektur-Review schließen.
2. Shared Contracts finalisieren (einheitliches Vertragsmodell).
3. Plugin-Projekte und `PluginManager` gemäß Blueprint implementieren.
4. Build-/Publish-Targets + CI-Validierung für `plugins/` ergänzen.
5. Integrations- und Negativtests für Discovery, Typzuordnung und Robustheit umsetzen.

---

*Erstellt durch den planning-orchestrator auf Basis der verbindlichen Anforderungsquelle und der verlinkten Planungsdokumente unter `docs/`.*

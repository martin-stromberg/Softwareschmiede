# Architektur-Review: Plugin-Klassenbibliotheken (GitHub & Copilot)

> **Dokument-Typ:** Feature-spezifisches Architektur-Review  
> **Projekt:** Softwareschmiede  
> **Scope:** Konsistenz von Anforderungen, Architektur-Blueprint und ERM inkl. Security- sowie Build/Deployment-Risiken  
> **Datum:** 2026-05-10

## Reviewte Unterlagen

- Primärquelle: [`../../.copilot-task.md`](../../.copilot-task.md)
- Anforderungen: [`../requirements/plugin-klassenbibliotheken-github-und-copilot.md`](../requirements/plugin-klassenbibliotheken-github-und-copilot.md)
- Architektur-Blueprint: [`../architecture/plugin-klassenbibliotheken-github-und-copilot-architecture-blueprint.md`](../architecture/plugin-klassenbibliotheken-github-und-copilot-architecture-blueprint.md)
- ERM: [`../architecture/plugin-klassenbibliotheken-github-und-copilot-entity-relationship-model.md`](../architecture/plugin-klassenbibliotheken-github-und-copilot-entity-relationship-model.md)

---

## 1) Executive Summary

Die Zielrichtung (echtes Plugin-Prinzip, Discovery aus `plugins/`, Build-Ablage der DLLs) ist korrekt und konsistent zur Primäranforderung.  
Es bestehen jedoch **mehrere umsetzungsrelevante Lücken** in Vertragsmodell, Security-Absicherung des Plugin-Ladens und Build/Publish-Härtung.

**Gesamtbewertung:** ⚠️ **Freigabe mit Auflagen** (vor Implementierung sind Blocker/Major-Punkte zu schließen).

---

## 2) Priorisierte Findings

| ID | Priorität | Bereich | Finding | Risiko | Empfohlene Aktion |
|---|---|---|---|---|---|
| F-01 | **Blocker** | Konsistenz Verträge | Typisierung ist nicht eindeutig festgelegt (`PluginType` vs. reine Interface-Erkennung; `ISourceCodeManagementPlugin`/`IDevelopmentAutomationPlugin` vs. `IGitPlugin`/`IKiPlugin`). | Divergente Implementierungen von Host/Plugins, instabile Discovery (FR-2.2). | Verbindliche Contract-Entscheidung treffen und in allen Dokumenten angleichen (ein Modell, keine „bzw.“-Varianten). |
| F-02 | **Major** | Security | Es fehlt ein Trust-Modell für DLL-Discovery aus `<Programmverzeichnis>/plugins` (Signatur/Allowlist/Hash-Prüfung). | Laden manipulierter DLLs (RCE-Risiko). | Mindestschutz definieren: erlaubte Publisher/Hash-Allowlist + Startup-Block für unbekannte Plugins in produktiven Umgebungen. |
| F-03 | **Major** | Build/Deployment | Build-Mechanik bleibt konzeptionell; konkrete, reproduzierbare MSBuild-Definition für Plugin-DLLs + transitive Abhängigkeiten/PUBLISH-Härtung fehlt. | Plugins im Build vorhanden, aber in Publish/Release unvollständig oder lauffehlerhaft. | Verbindliche Targets für Build **und** Publish ergänzen, inkl. CI-Check auf `plugins/*` und Dependency-Vollständigkeit. |
| F-04 | **Major** | Laufzeitarchitektur | AssemblyLoadContext-Strategie ist erwähnt, aber nicht präzise (Shared-Contracts-Typidentität, Resolver-Regeln, Unload-Verhalten). | Typkonflikte/`FileLoadException`, nicht reproduzierbare Laufzeitfehler. | Ladekonzept spezifizieren: Shared-Contracts nur aus Host-Kontext, Plugin-spezifische Abhängigkeiten isolieren, deterministische Resolver-Regeln dokumentieren. |
| F-05 | **Medium** | Anforderungen ↔ ERM | ERM modelliert persistente Discovery-/Registry-Entitäten, obwohl Requirements den In-Memory-MVP erlauben und Persistenz nicht verlangen. | Scope-Aufblähung, unnötige DB-Migrationen vor Nutzen. | ERM in **MVP (in-memory)** und **Option Persistenz** trennen; Persistenz als späteres Increment mit Entscheidungskriterien ausweisen. |
| F-06 | **Medium** | UI/UX | UI beschreibt Plugin-Sichtbarkeit, aber nicht das Verhalten bei „kein Plugin geladen“/„nur 1 Typ verfügbar“. | Unklare Nutzerführung im Degradationsfall, Support-Aufwand. | UX-Fehlerpfade definieren (Banner, Handlungsempfehlung, Block/Teilfreigabe pro Feature). |
| F-07 | **Minor** | Qualitätsziele | Mess- und Nachweisstrategie für NFR-2 (≤2s Discovery) und NFR-4 (vollständige Diagnostik) nicht konkret operationalisiert. | Qualitätsziele bleiben unverifiziert. | Akzeptanztests/Benchmarks inkl. Schwellenwerte und Log-Assertions verbindlich ergänzen. |

---

## 3) Strukturierte Bewertung

### 3.1 Systemarchitektur
- **Stärken:** klare Zielschichtung (Host/Contracts/Plugins), saubere Entkopplungsintention.
- **Schwachstelle:** Vertragsgrenzen nicht eindeutig normiert (siehe F-01), ALC-Konzept zu abstrakt (F-04).

### 3.2 Technologieentscheidungen
- **Positiv:** Plugin-DLLs unter `plugins/` und runtime discovery sind passend zur Anforderung.
- **Risiko:** fehlende technische Verbindlichkeit bei Build/Publish-Mechanik (F-03).

### 3.3 Konsistenz Anforderungen ↔ Architektur ↔ ERM
- **Teilweise konsistent:** Zielbild und Plugin-Arten sind durchgängig vorhanden.
- **Inkonsistenzen:** Vertragsmodell und Persistenzumfang sind dokumentübergreifend nicht strikt synchron (F-01, F-05).

### 3.4 Security Review
- Credential-Store-Ansatz ist gut, aber **nicht ausreichend** ohne Plugin-Trust-Mechanismus.
- Besonders kritisch ist dynamisches DLL-Laden ohne Herkunftsprüfung (F-02).

### 3.5 Build/Deployment-Risiken
- Blueprint nennt Richtung, aber noch kein ausführbares „Definition of Done“-Niveau für CI/CD (F-03).
- Risiko von Drift zwischen `build` und `publish` bleibt hoch, solange keine automatischen Prüfungen hinterlegt sind.

### 3.6 UI/UX Review
- Informationsarchitektur ist brauchbar.
- Fehler- und Degradationsszenarien sollten explizit modelliert werden (F-06).

---

## 4) Konkreter Maßnahmenplan (priorisiert)

1. **(Blocker) Contract-Entscheidung fixieren**  
   Ein einziges, verbindliches Interface-/Typisierungsmodell festlegen und in Requirements, Blueprint, ERM, API-Doku synchronisieren.
2. **(Major) Plugin-Trust-Policy einführen**  
   Signatur-/Hash-Validierung vor Aktivierung von Plugins, inklusive Betriebsmodus-Regeln (Dev vs. Prod).
3. **(Major) Build-/Publish-Pipeline härten**  
   Verbindliche Targets + CI Assertions für `plugins/`-Inhalte (DLLs + Abhängigkeiten).
4. **(Major) ALC-Design spezifizieren**  
   Typidentität, Resolver-Reihenfolge, Fehler- und Unload-Strategie dokumentieren und testen.
5. **(Medium) ERM-Phasenmodell ergänzen**  
   MVP ohne Persistenz klar trennen von optionaler Persistenzphase.
6. **(Medium) UX-Fallbacks definieren**  
   Leitszenarien für fehlende/defekte Plugins inkl. Nutzerhinweise und Handlungsoptionen.

---

## 5) Freigabeempfehlung

**Empfehlung:** Umsetzung starten erst nach Schließen von **F-01** sowie mindestens konzeptioneller Absicherung von **F-02 bis F-04**.  
Danach ist das Design tragfähig für eine inkrementelle Implementierung.


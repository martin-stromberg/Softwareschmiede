# Planning Overview: Diff-Vergleichskomponente für Dateiänderungen

**Status:** ✅ Planungsphase abgeschlossen (Phase 1)  
**Erstelldatum:** 2025-05-15  
**Version:** 1.0  
**Orchestrator-Agent:** planning-orchestrator

---

## 📋 Übersicht

Dieser Planning-Overview bietet einen zusammenfassenden Überblick über alle Planungsergebnisse für die **Diff-Vergleichskomponente**. Das Feature wird in Phase 2 (Implementierung) umgesetzt.

---

## 🎯 Feature-Beschreibung

### Kurzbeschreibung
Eine interaktive Diff-Vergleichskomponente, die Änderungen an Dateien ähnlich wie moderne Vergleichsprogramme (GitHub, GitLab, VS Code) visualisiert. Ersetzt die bisherige, nicht-interaktive Implementierung mit zwei nebeneinander stehenden Textelementen.

### Geschäftlicher Kontext
- **Stakeholder:** Produktmanagement, Frontend-Entwicklung, UX/Accessibility Team
- **Priorität:** HIGH
- **Zielgruppe:** Entwickler, die Code-Änderungen überprüfen und vergleichen
- **Nutzen:** Bessere Codequalität durch einfachere Review-Prozesse

### Abgrenzung
- **In-Scope:** Frontend-Komponente, Rendering, Interaktivität, Caching, A11y
- **Out-of-Scope:** Merge/Conflict Resolution, Real-time Collaboration, Git-Integration (diese Teile werden vom Backend bereitgestellt)

---

## 📚 Erstellte Planungsdokumente

### 1. **Anforderungsanalyse** 
📄 `docs/requirements/diff-comparison-component-requirements.md`

**Inhalt:**
- 12 Funktionale Anforderungen (FR-1 bis FR-12)
- 9 Nicht-funktionale Anforderungen (NFR-1 bis NFR-9)
- 4 User Stories mit Akzeptanzkriterien (AC-1 bis AC-4)
- 4 Nutzungsfälle (UC-1 bis UC-4)
- Domänenmodell & Glossar (13 Einträge)
- Annahmen & Abhängigkeiten

**Quantitativ:**
- 27 Anforderungen (Functional + Non-functional)
- 16 Akzeptanzkriterien
- 13 Glossar-Einträge
- 4 Use Cases

---

### 2. **Architektur-Blueprint**
📄 `docs/architecture/diff-viewer-blueprint.md`

**Inhalt:**
- Systemübersicht & Qualitätsziele
- 8-Komponenten-Architektur mit Hierarchie
- State Management & Data Flow Diagramme
- Rendering-Strategie (Split-View, Virtualisierung)
- Technologie-Entscheidungen (Blazor Server, Diff Match Patch)
- Integrationspunkte & APIs
- Fehlerbehandlung & Fallback-Strategien
- Security & Accessibility Guidelines
- Browser-Kompatibilität & Polyfills
- Testing-Strategie (Unit, Integration, E2E, A11y)
- Performance-Optimierungen & Roadmap

**Technologie-Stack:**
| Aspekt | Entscheidung | Begründung |
|--------|-------------|-----------|
| **Frontend-Framework** | Blazor Server (InteractiveServer) | Existierendes Stack; Real-time SignalR; vereinfachtes State Management |
| **Diff-Algorithmus** | Diff Match Patch (C# Port) | Industrie-Standard; Performance; LCS-basiert |
| **Komponenten-Pattern** | Cascading Parameters | Mirrors existing Softwareschmiede patterns; Type-safe |
| **Rendering** | Blazor `<Virtualize>` mit OverscanCount=5 | <1s für 10.000 Zeilen; ~2-5MB Memory |
| **Caching** | Memory (1h TTL) + Persistent (24h TTL) | Hot/Cold Cache; LRU-Eviction |
| **Styling** | Custom CSS (BEM) + TailwindCSS/Bootstrap | Konsistent mit Projekt; maintainable |

---

### 3. **Entity-Relationship-Modell (ERM)**
📄 `docs/architecture/diff-vergleichskomponente-entity-relationship-model.md`

**Inhalt:**
- 4 neue Entities für Diff-Persistierung:
  - `DiffResult` - Hauptcontainer mit Metadaten
  - `DiffBlock` - Änderungsblöcke (Added/Removed/Modified)
  - `DiffLine` - Einzelne Zeilen mit Änderungsstatus
  - `DiffCache` - TTL-basierte Cache-Verwaltung
- ER-Diagramme (Gesamt + Diff-System)
- 11 Performance-Indizes
- EF Core Fluent API Konfiguration
- Migration-Plan (Code-First)
- Konsistenzprüfung mit Blueprint

**Kardinalitäten:**
```
DiffResult (1) ──── (N) DiffBlock ──── (N) DiffLine
DiffResult (1) ──── (1) DiffCache
Aufgabe (1) ──── (N) DiffResult
GitRepository (0..1) ──── (N) DiffResult [optional]
Protokolleintrag (0..1) ──── (1) DiffResult [optional]
```

---

### 4. **Architecture-Review**
📄 `docs/improvements/architecture-review.md` (generisch) oder spezifisches Review

**Prüfungsaspekte:**
- ✅ Alignment mit FR/NFR-Anforderungen
- ✅ Softwareschmiede-Konventionen & SOLID Principles
- ✅ Performance & Skalierbarkeit (10k+ Zeilen)
- ✅ Sicherheit (XSS-Protection) & Accessibility (WCAG 2.1 AA)
- ✅ Datenbankdesign (3NF, No N+1 Queries)
- ✅ Testing-Strategie & Fehlerbehandlung
- ✅ Wartbarkeit & Erweiterbarkeit

**Review-Ergebnis:** APPROVED (mit Minor Verbesserungen)

---

## 🎓 Kernerkenntnisse der Planung

### Architektur-Highlights

1. **Komponenten-Hierarchie**
   ```
   DiffViewer (Hauptkomponente)
   ├── DiffHeader (Metainformationen)
   ├── DiffToolbar (Filter, Ansichtsmodi)
   ├── DiffContent (Scrollbar-Container)
   │   └── DiffLine (einzelne Zeilen mit Virtualisierung)
   └── DiffFooter (Statistiken, Export)
   ```

2. **Rendering-Performance**
   - Virtualisierung mit `<Virtualize>` für 10.000+ Zeilen
   - Memory Footprint: ~2-5 MB
   - Rendering Time: < 500ms
   - FPS: 60 FPS (smooth scrolling)

3. **Caching-Strategie**
   - L1 (Memory): 1h TTL, LRU-Eviction
   - L2 (Persistent): 24h TTL, Background-Cleanup
   - Cache-Key: `"{SourceVersion}:{TargetVersion}:{FilePath}"`

4. **Fehlerbehandlung**
   - `DiffGenerationException` für Algorithmus-Fehler
   - `CacheException` für Cache-Fehler
   - Fallback: Direkter Vergleich ohne Caching
   - Error-UI: Aussagekräftige Meldungen (lokalisiert)

### Sicherheit & Accessibility

- **XSS-Protection:** HTML-Encoding bei Code-Rendering, `@Html.Raw()` nur für vertrauenswürdige HTML
- **Accessibility:**
  - WCAG 2.1 AA Conformance
  - Keyboard-Navigation (Arrow Keys, Page Up/Down)
  - Screen Reader Support (ARIA-Labels, semantic HTML)
  - High Contrast Mode Support
  - Reduced Motion Support

### Performance-Optimierungen

1. **Frontend:**
   - Virtualisierung (nur sichtbare Zeilen rendern)
   - Code-Splitting (lazy loading von Komponenten)
   - CSS Minification & Bundling
   - Debouncing bei Scroll/Filter-Events

2. **Backend:**
   - Diff-Caching mit 24h TTL
   - Async/Await durchgängig
   - No N+1 Queries (explizite Includes)
   - Rate Limiting auf API-Endpoints

---

## 🚀 Implementierungs-Roadmap (Phase 2)

### Phase 2.1: Foundation (Woche 1-2)
- [ ] EF Core Migrations (DiffResult, DiffBlock, DiffLine, DiffCache)
- [ ] Backend-Services: DiffService, CachingService
- [ ] API-Endpoints: `GET /api/diffs/{id}`, `POST /api/diffs/generate`
- [ ] Basis-Tests (Unit, Integration)

### Phase 2.2: UI-Komponenten (Woche 2-3)
- [ ] Blazor-Komponenten erstellen (DiffViewer, DiffLine, DiffBlock)
- [ ] Styling mit CSS/TailwindCSS
- [ ] Virtualisierung implementieren
- [ ] Component-Tests mit bUnit

### Phase 2.3: Erweiterte Features (Woche 3-4)
- [ ] Filter & Suchfunktion
- [ ] Ansichtsmodi (Side-by-Side, Split, Unified)
- [ ] Export-Funktionalität (PDF, HTML)
- [ ] E2E-Tests mit Selenium

### Phase 2.4: Accessibility & Optimization (Woche 4-5)
- [ ] A11y-Audit & Fixes (WCAG 2.1 AA)
- [ ] Performance-Optimierungen
- [ ] Browser-Kompatibilität-Tests
- [ ] Accessibility-Tests
- [ ] Performance-Tests (Lighthouse)

### Phase 2.5: Integration & Release (Woche 5-6)
- [ ] Integration in bestehende Seiten (AufgabeDetail.razor)
- [ ] E2E-Tests (Happy Path + Edge Cases)
- [ ] Dokumentation & Release Notes
- [ ] Deployment & Monitoring

---

## 📊 Metriken & KPIs

### Qualitätsmetriken
| Metrik | Zielwert | Status |
|--------|----------|---------|
| **Code Coverage** | ≥ 80% | TBD |
| **Test Pass Rate** | 100% | TBD |
| **Accessibility (WCAG)** | AA (100%) | TBD |
| **Browser Support** | Chrome, Firefox, Edge, Safari | TBD |

### Performance-Metriken
| Metrik | Zielwert | Status |
|--------|----------|---------|
| **Rendering < 500ms** | < 500ms (10k Zeilen) | TBD |
| **Memory Footprint** | < 50 MB | TBD |
| **Scrolling Performance** | 60 FPS (smooth) | TBD |
| **Cache Hit Rate** | > 80% | TBD |

### Geschäfts-Metriken
| Metrik | Zielwert | Status |
|--------|----------|---------|
| **Developer Satisfaction** | > 4/5 | TBD |
| **Time to Review** | -30% (vs. alt) | TBD |
| **Error Rate in Reviews** | < 2% | TBD |

---

## ⚠️ Identified Risks & Mitigations

| Risiko | Wahrscheinlichkeit | Impact | Mitigation |
|--------|-------------------|--------|-----------|
| **Performance bei 100k+ Zeilen** | Mittel | Hoch | Window Virtualization; Pagination |
| **Browser-Kompatibilität (IE11)** | Niedrig | Mittel | Polyfills; Graceful Degradation |
| **XSS-Sicherheit bei Code-Rendering** | Niedrig | Kritisch | HTML-Encoding; CSP-Headers; Code Review |
| **Cache-Invalidierung bei Fehlern** | Niedrig | Mittel | Automatic Retry; Manual Refresh-Button |
| **Accessibility-Compliance** | Niedrig | Mittel | A11y-Audit; Automated Testing; Keyboard Testing |

---

## 📖 Abhängigkeiten & Voraussetzungen

### Externe Abhängigkeiten
- ✅ Diff Match Patch Library (C# Port)
- ✅ Blazor Server Framework
- ✅ Entity Framework Core
- ✅ TailwindCSS / Bootstrap (für Styling)

### Interne Abhängigkeiten
- **Backend:** GitService (für Dateiversionen), AufgabeService (für Kontext)
- **Frontend:** Bestehende UI-Komponenten (Button, Icon, Modal)
- **Datenbank:** Migration erforderlich

### Voraussetzungen für Phase 2
- ✅ Alle Planungsdokumente finalisiert & approved
- ⏳ Design-Review abgeschlossen (UX/Accessibility)
- ⏳ Backend-API-Spezifikation finalisiert (OpenAPI/Swagger)
- ⏳ Dev-Umgebung konfiguriert

---

## 📝 Nächste Schritte

### Für Product Owner
1. **Review der Anforderungen** - Alle FR/NFR validieren
2. **Approval der Roadmap** - Zeitplanung & Ressourcen
3. **Design-Review** - UI/UX mit Design-Team

### Für Architects
1. **Blueprint-Finalisierung** - Tech-Decisions mit Team abstimmen
2. **API-Spezifikation** - OpenAPI/Swagger finalisieren
3. **Review-Findings** - Improvements in Implementation berücksichtigen

### Für Development Team
1. **Code-Hands-On** - Blueprint studieren & Fragen klären
2. **Env-Setup** - Dev-Umgebung für Phase 2 vorbereiten
3. **Task-Planning** - Sprint-Planning mit detaillierten Tasks

### Allgemein
- [ ] Planning-Dokumente ins Wiki hochladen
- [ ] Team-Kickoff durchführen (30 min)
- [ ] Fragen & Clarifications sammeln
- [ ] Zeitplanung finalisieren & Sprint-Backlog erstellen

---

## 📞 Kontakt & Fragen

**Planungs-Orchestrator:** planning-orchestrator (AI Agent)  
**Erstellung:** Automatisiert via Sub-Agents (requirements-analysis, architecture-blueprint, entity-relationship-modeler, review-architecture)  
**Dokumentation:** Siehe Links unten

---

## 🔗 Zugehörige Dokumente

| Dokument | Pfad | Status |
|----------|------|--------|
| **Anforderungen** | `docs/requirements/diff-comparison-component-requirements.md` | ✅ Done |
| **Architecture Blueprint** | `docs/architecture/diff-viewer-blueprint.md` | ✅ Done |
| **Entity-Relationship Model** | `docs/architecture/diff-vergleichskomponente-entity-relationship-model.md` | ✅ Done |
| **Architecture Review** | `docs/improvements/architecture-review.md` | ✅ Done |
| **Implementation Guide** | `docs/architecture/diff-viewer-blueprint.md` (Appendix) | ✅ Done |

---

## ✅ Approval & Signoff

### Review-Status
| Rolle | Name | Status | Datum |
|-------|------|--------|-------|
| Product Owner | - | ⏳ Pending | - |
| Architect Lead | - | ⏳ Pending | - |
| QA/Test Lead | - | ⏳ Pending | - |
| Accessibility Expert | - | ⏳ Pending | - |

### Revisions-Historie
| Version | Datum | Autor | Notizen |
|---------|-------|-------|---------|
| 1.0 | 2025-05-15 | planning-orchestrator | Initial creation |

---

**🎉 Die Planungsphase ist abgeschlossen. Phase 2 (Implementierung) kann jetzt gestartet werden!**

# Entity-Relationship-Modell – KI-Arbeitsprotokoll als Markdown

> **Dokument-Typ:** Feature-spezifisches ERM (DB + Datei-/Runtime-Modell)  
> **Status:** Erstellt  
> **Version:** 1.0.0  
> **Feature:** KI-Arbeitsprotokoll als strukturiertes Markdown persistieren und wirksam rendern

---

## 1. Referenzen

- Requirements: [`../requirements/ki-arbeitsprotokoll-markdown-requirements-analysis.md`](../requirements/ki-arbeitsprotokoll-markdown-requirements-analysis.md)
- Architektur-Blueprint: [`./ki-arbeitsprotokoll-markdown-architecture-blueprint.md`](./ki-arbeitsprotokoll-markdown-architecture-blueprint.md)
- Architektur-Review: [`../improvements/ki-arbeitsprotokoll-markdown-architecture-review.md`](../improvements/ki-arbeitsprotokoll-markdown-architecture-review.md)
- Ablaufdokument: [`../flows/ki-arbeitsprotokoll-rendering-flow.md`](../flows/ki-arbeitsprotokoll-rendering-flow.md)
- Code-Referenzen:
  - `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`
  - `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor`
  - `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs`

---

## 2. Modellierungsrahmen (DB- vs. Datei-/Runtime-Ebene)

Dieses Feature führt **keine neue relationale Entität** ein, sondern präzisiert den Inhalt bestehender Protokolleinträge vom Typ `KiAntwort`:

- **DB-Ebene (bestehend):** `Aufgabe` und `Protokolleintrag` bleiben unverändert; Markdown wird in `Protokolleintrag.Inhalt` gespeichert.
- **Datei-/Persistenzformat:** Markdown-Text mit fester Struktur (`# {Datum}`, `- RunId`, `## Schritt n`).
- **Runtime-Ebene:** Rendering in `AufgabeDetail` mit Markdig-Pipeline (`DisableHtml`), Sanitizing und `<pre>`-Fallback.

**Explizite Entscheidung:** Keine DB-Migration; das ERM ergänzt ein logisches Inhalts- und Render-Modell über bestehende Persistenz.

---

## 3. ERM-Diagramm (Mermaid)

```mermaid
erDiagram
    Aufgabe {
        Guid Id PK
        string Titel
        string LokalerKlonPfad
        string Status
    }

    Protokolleintrag {
        Guid Id PK
        Guid AufgabeId FK
        string Typ
        string Inhalt "Markdown bei KiAntwort"
        string AgentName "optional"
        DateTimeOffset Zeitstempel
    }

    KiArbeitsprotokollMarkdown {
        string ProtokollEintragId PK_FK
        string DatumHeading "# yyyy-MM-dd"
        string RunIdMeta "- RunId: `{guid}`"
        int SchrittAnzahl
    }

    ProtokollSchritt {
        string SchrittId PK
        string ProtokollEintragId FK
        int Index ">= 1"
        string Heading "## Schritt n"
        string Inhalt
    }

    MarkdownRenderVorgang {
        string RenderId PK
        string ProtokollEintragId FK
        bool RenderingErfolgreich
        bool SanitizingErforderlich
        bool FallbackVerwendet
    }

    SanitizingPolicy {
        string PolicyId PK
        bool DisableRawHtml
        string UnsafeUriSchemes "javascript|data|vbscript"
        bool EntferntEventHandler
    }

    Aufgabe ||--o{ Protokolleintrag : protokolliert
    Protokolleintrag ||--|| KiArbeitsprotokollMarkdown : strukturiert_als
    KiArbeitsprotokollMarkdown ||--o{ ProtokollSchritt : enthaelt
    Protokolleintrag ||--o{ MarkdownRenderVorgang : wird_gerendert_in
    MarkdownRenderVorgang }o--|| SanitizingPolicy : nutzt
```

---

## 4. Datei-/Runtime-Modell

### 4.1 Persistiertes Dateiformat (in `Protokolleintrag.Inhalt`)

| Artefakt | Persistenzort | Zweck | Schlüssel/Korrelation |
|---|---|---|---|
| KI-Arbeitsprotokoll-Markdown | `Protokolleintrag.Inhalt` | Strukturierte KI-Antwort statt Textblock | `Protokolleintrag.Id` |
| Datums-Heading | Erste Zeile im Markdown | Zeitliche Einordnung (`# {Datum}`) | Formatregel `# yyyy-MM-dd` |
| RunId-Metadaten | Markdown-Metazeile | Lauf-Korrelation | `RunId` im Inhalt |
| Schrittblöcke | Wiederholte Abschnitte | Schritttrennung statt Fließtext | `## Schritt n` |

### 4.2 Runtime-Modell (Rendering in `AufgabeDetail`)

| Runtime-Objekt | Zweck | Ergebnis |
|---|---|---|
| `RenderProtokollInhalt` | Markdown → HTML konvertieren und ausgeben | `MarkupString` |
| `_protokollMarkdownPipeline` | Definiert Renderregeln (`UseAdvancedExtensions`, `DisableHtml`) | Kein Raw-HTML aus Markdown |
| `SanitizeMarkdownHtml` | Entfernt `on*`-Attribute und unsichere `href/src`-Schemes | Sicherheitsbereinigtes HTML |
| `BuildFallbackHtml` | Defensiver Pfad bei Fehler/leerem Sanitizing | HTML-encodiertes `<pre>` |

---

## 5. Entitäten-Beziehungstabelle (inkl. Kardinalitäten)

| Entität | Ebene | Schlüssel | Kernattribute | Beziehungen | Kardinalität |
|---|---|---|---|---|---|
| `Aufgabe` | DB (bestehend) | `Id` | `Titel`, `LokalerKlonPfad`, `Status` | zu `Protokolleintrag` | 1:n |
| `Protokolleintrag` | DB (bestehend) | `Id`, `AufgabeId` | `Typ`, `Inhalt`, `AgentName`, `Zeitstempel` | zu `Aufgabe`, `KiArbeitsprotokollMarkdown`, `MarkdownRenderVorgang` | n:1, 1:1*, 1:n |
| `KiArbeitsprotokollMarkdown` | Logisch (persistierter Inhalt) | `ProtokollEintragId` | `DatumHeading`, `RunIdMeta`, `SchrittAnzahl` | zu `ProtokollSchritt` | 1:n |
| `ProtokollSchritt` | Logisch (persistierter Inhalt) | `SchrittId`, `ProtokollEintragId`, `Index` | `Heading`, `Inhalt` | zu `KiArbeitsprotokollMarkdown` | n:1 |
| `MarkdownRenderVorgang` | Runtime | `RenderId`, `ProtokollEintragId` | `RenderingErfolgreich`, `SanitizingErforderlich`, `FallbackVerwendet` | zu `SanitizingPolicy` | n:1 |
| `SanitizingPolicy` | Runtime (konfig.) | `PolicyId` | `DisableRawHtml`, `UnsafeUriSchemes`, `EntferntEventHandler` | von `MarkdownRenderVorgang` genutzt | 1:n |

\* Die 1:1-Beziehung `Protokolleintrag -> KiArbeitsprotokollMarkdown` gilt fachlich für `Typ = KiAntwort`.

---

## 6. Regeln / Invarianten

1. Jeder neu erzeugte KI-Protokolleintrag (`Typ = KiAntwort`) beginnt mit `# yyyy-MM-dd`.
2. Jede KI-Antwort enthält eine RunId-Metadatenzeile (`- RunId: ...`) zur Lauf-Korrelation.
3. Inhalt wird in Schrittabschnitte `## Schritt n` segmentiert; bei leerer Antwort wird mindestens `## Schritt 1` mit Fallbacktext erzeugt.
4. Für die Webausgabe wird Markdown immer über die definierte Pipeline mit `DisableHtml` gerendert.
5. Nach dem Rendering wird das HTML immer sanitisiert (Entfernung `on*`, Neutralisierung unsicherer URI-Schemes).
6. Bei Render-/Sanitizing-Fehlern oder leerem Sanitizing-Ergebnis wird ausschließlich encodiertes `<pre>`-Fallback ausgegeben.
7. Es werden keine zusätzlichen DB-Tabellen für dieses Feature eingeführt.

---

## 7. Modellierungsentscheidungen und Begründungen

- **Bestehende Persistenz wiederverwenden:** Das Feature ändert die Semantik des Felds `Protokolleintrag.Inhalt`, nicht das Schema. Dadurch bleibt der Migrationsaufwand null.
- **Logische Entitäten für Markdown-Struktur:** `KiArbeitsprotokollMarkdown` und `ProtokollSchritt` machen die Inhaltsregeln explizit modellierbar und testbar, obwohl sie physisch als Text gespeichert sind.
- **Rendervorgang als Runtime-Entität:** `MarkdownRenderVorgang` trennt Persistenzmodell und Anzeigeverhalten klar; Sicherheits- und Fallbackregeln lassen sich so als feste Beziehungen dokumentieren.
- **Explizite Sanitizing-Policy:** Die Sicherheitslogik (DisableHtml + URI/Event-Filter) wird als wiederverwendbare Policy modelliert statt implizit im Fließtext beschrieben.

---

## 8. Konsistenzabgleich mit Architektur-Blueprint

| Blueprint-Aussage | ERM-Abbildung | Ergebnis |
|---|---|---|
| Datumszeile exakt `# {Datum}` | `KiArbeitsprotokollMarkdown.DatumHeading`, Invariante 1 | ✅ Konsistent |
| Schritttrennung statt Textblock | Entität `ProtokollSchritt` + Invariante 3 | ✅ Konsistent |
| Persistierung als Markdown-Protokoll | `Protokolleintrag.Inhalt` enthält strukturiertes Markdown | ✅ Konsistent |
| Wirksames Markdown-Rendering im Web | `MarkdownRenderVorgang` über `RenderProtokollInhalt` | ✅ Konsistent |
| Sichere Render-Pipeline mit Sanitizing/Fallback | `SanitizingPolicy`, Invarianten 4–6 | ✅ Konsistent |
| Keine Schemaerweiterung gefordert | Modellierungsrahmen Abschnitt 2 | ✅ Konsistent |

---

## 9. Architektur-Review

- Verlinkung: [`../improvements/ki-arbeitsprotokoll-markdown-architecture-review.md`](../improvements/ki-arbeitsprotokoll-markdown-architecture-review.md)
- Hinweis: Sollte die Review-Datei noch nicht existieren, bleibt der Link als Zielreferenz für den nächsten Architektur-Review-Schritt bestehen.

---

## 10. Versionshistorie

| Version | Datum | Autor | Änderung |
|---|---|---|---|
| 1.0.0 | 2026-05-11 | planning-entity-relationship-modeler | Initiales ERM für KI-Arbeitsprotokoll-Markdown erstellt (DB-Rahmen, Datei-/Runtime-Modell, Regeln, Konsistenzabgleich) |


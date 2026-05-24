# Entity-Relationship-Modell – KI-Arbeitsprotokoll als Markdown

> **Dokument-Typ:** Feature-spezifisches ERM (Persistenz + logisches Strukturmodell + Runtime-Rendering)  
> **Status:** Aktualisiert  
> **Version:** 1.1.0  
> **Feature:** `ki-arbeitsprotokoll-markdown`

---

## 1. Referenzen

- Requirements: [`../requirements/ki-arbeitsprotokoll-markdown-requirements-analysis.md`](../requirements/ki-arbeitsprotokoll-markdown-requirements-analysis.md)
- Architektur-Blueprint: [`./ki-arbeitsprotokoll-markdown-architecture-blueprint.md`](./ki-arbeitsprotokoll-markdown-architecture-blueprint.md)
- Ablaufdokument: [`../flows/ki-arbeitsprotokoll-rendering-flow.md`](../flows/ki-arbeitsprotokoll-rendering-flow.md)
- Implementierungsreferenzen:
  - `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs` (`BuildKiArbeitsprotokollMarkdown`)
  - `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs` (`RenderProtokollInhalt`, `SanitizeMarkdownHtml`, `BuildFallbackHtml`)

---

## 2. Modellierungsrahmen

Dieses Feature ergänzt **kein physisches Datenbankschema**, sondern definiert:

1. Persistenzbezug über bestehende Entität `Protokolleintrag` (`Typ = KiAntwort`, `Inhalt = Markdown`).
2. Logische Strukturentitäten für Run, Markdown-Protokoll und Schrittblöcke.
3. Runtime-Entitäten für Renderdarstellung inkl. Markdown-Rendermodus und Sanitizing/Fallback.

---

## 3. ERM-Diagramm (Mermaid)

```mermaid
erDiagram
    Aufgabe {
        Guid Id PK
        string Titel
        string Status
    }

    Protokolleintrag {
        Guid Id PK
        Guid AufgabeId FK
        string Typ
        string Inhalt "Markdown bei KiAntwort"
        DateTimeOffset Zeitstempel
        string AgentName
    }

    KiRun {
        Guid RunId PK
        Guid AufgabeId FK
        DateTimeOffset ZeitpunktUtc
        string ErgebnisStatus
    }

    MarkdownProtokoll {
        Guid ProtokollEintragId PK_FK
        string DatumsHeader "# yyyy-MM-dd"
        string RunIdMeta "- RunId: `{guid}`"
        int SchrittAnzahl ">= 1"
        string SchrittTrennregel "Leerzeile zwischen Blöcken"
    }

    Schrittblock {
        Guid SchrittblockId PK
        Guid ProtokollEintragId FK
        int SchrittNr ">= 1"
        string Heading "## Schritt n"
        string Inhaltszeile
        bool HatNachfolgendenBlockabstand
    }

    Renderdarstellung {
        Guid RenderId PK
        Guid ProtokollEintragId FK
        string Rendermodus "MarkdownToHtml"
        bool DisableRawHtml
        bool SanitizingAktiv
        bool FallbackVerwendet
        string AusgabeTyp "sanitized-html|encoded-pre"
    }

    SanitizingRegel {
        string RegelId PK
        bool EntferntEventAttribute
        string BlockierteUriSchemes "javascript|data|vbscript"
        string UnsafeUriErsatz "#"
    }

    Aufgabe ||--o{ Protokolleintrag : hat
    Aufgabe ||--o{ KiRun : fuehrt_aus
    KiRun ||--|| MarkdownProtokoll : erzeugt
    Protokolleintrag ||--|| MarkdownProtokoll : speichert_als
    MarkdownProtokoll ||--o{ Schrittblock : enthaelt
    Protokolleintrag ||--o{ Renderdarstellung : wird_angezeigt_als
    Renderdarstellung }o--|| SanitizingRegel : nutzt
```

---

## 4. Entitäten, Attribute, Beziehungen, Kardinalitäten

| Entität | Ebene | Schlüssel | Zentrale Attribute | Beziehungen | Kardinalität |
|---|---|---|---|---|---|
| `Aufgabe` | DB (bestehend) | `Id` | `Titel`, `Status` | zu `Protokolleintrag`, `KiRun` | 1:n, 1:n |
| `Protokolleintrag` | DB (bestehend) | `Id`, `AufgabeId` | `Typ`, `Inhalt`, `Zeitstempel`, `AgentName` | zu `MarkdownProtokoll`, `Renderdarstellung` | 1:1* , 1:n |
| `KiRun` | Logisch (fachlich) | `RunId`, `AufgabeId` | `ZeitpunktUtc`, `ErgebnisStatus` | zu `MarkdownProtokoll` | 1:1 |
| `MarkdownProtokoll` | Logisch (persistierte Struktur) | `ProtokollEintragId` | `DatumsHeader`, `RunIdMeta`, `SchrittAnzahl`, `SchrittTrennregel` | zu `Schrittblock` | 1:n |
| `Schrittblock` | Logisch (persistierte Struktur) | `SchrittblockId`, `ProtokollEintragId`, `SchrittNr` | `Heading`, `Inhaltszeile`, `HatNachfolgendenBlockabstand` | zu `MarkdownProtokoll` | n:1 |
| `Renderdarstellung` | Runtime (UI) | `RenderId`, `ProtokollEintragId` | `Rendermodus`, `DisableRawHtml`, `SanitizingAktiv`, `FallbackVerwendet`, `AusgabeTyp` | zu `SanitizingRegel` | n:1 |
| `SanitizingRegel` | Runtime-Konfiguration | `RegelId` | `EntferntEventAttribute`, `BlockierteUriSchemes`, `UnsafeUriErsatz` | von `Renderdarstellung` genutzt | 1:n |

\* `Protokolleintrag` ↔ `MarkdownProtokoll` gilt fachlich für `Typ = KiAntwort`.

---

## 5. Integritätsregeln (fachlich + technisch)

1. **Datums-Header-Regel:**  
   Jeder neue KI-Protokolleintrag beginnt in Zeile 1 mit `# yyyy-MM-dd`.
2. **Run-Korrelation:**  
   `MarkdownProtokoll.RunIdMeta` enthält genau eine Metazeile `- RunId: \`{guid}\`` und referenziert `KiRun.RunId`.
3. **Schritttrennung:**  
   Für jede nicht-leere Antwortzeile existiert genau ein `Schrittblock` mit `Heading = ## Schritt n`; zwischen aufeinanderfolgenden Blöcken liegt mindestens eine Leerzeile.
4. **Fallback bei leerer Antwort:**  
   Ist keine nicht-leere Antwortzeile vorhanden, wird trotzdem `SchrittNr = 1` mit Fallback-Inhalt erzeugt.
5. **Rendermodus verbindlich:**  
   `Renderdarstellung.Rendermodus = MarkdownToHtml` (Markdig-Pipeline), nicht Rohtext/Raw-HTML.
6. **Sicherheitsregel Rendering:**  
   `DisableRawHtml = true` und `SanitizingAktiv = true`; `on*`-Attribute werden entfernt, unsichere URI-Schemes durch `#` ersetzt.
7. **Robuster Ausgabepfad:**  
   Bei Render-/Sanitizing-Fehlern gilt `FallbackVerwendet = true` und `AusgabeTyp = encoded-pre`.
8. **Schema-Stabilität:**  
   Für dieses Feature werden keine neuen physischen DB-Tabellen benötigt.

---

## 6. Begründungen zu Modellierungsentscheidungen

- **KiRun als eigene logische Entität:** macht RunId und Zeitpunkt fachlich explizit, ohne DB-Migration.
- **MarkdownProtokoll + Schrittblock:** modelliert die geforderte Struktur (`# {Datum}`, `## Schritt n`, Leerzeilen) testbar statt nur als Fließtextbeschreibung.
- **Renderdarstellung als Runtime-Entität:** trennt Persistenzinhalt klar von Anzeigeverhalten (Rendermodus, Sanitizing, Fallback).
- **SanitizingRegel explizit:** Sicherheitsanforderungen bleiben als wiederverwendbare Integritätsregeln nachvollziehbar.

---

## 7. Konsistenzabgleich mit Architektur-Blueprint

| Blueprint-Anforderung | ERM-Abbildung | Ergebnis |
|---|---|---|
| Datumskopf `# {Datum}` als erste Zeile | `MarkdownProtokoll.DatumsHeader` + Integritätsregel 1 | ✅ Konsistent |
| Schritttrennung `## Schritt n` inkl. Leerzeilen | `Schrittblock` + Integritätsregel 3 | ✅ Konsistent |
| Persistierung als Markdown in `Protokolleintrag.Inhalt` | Beziehung `Protokolleintrag` ↔ `MarkdownProtokoll` | ✅ Konsistent |
| Definierter Markdown-Rendermodus in UI | `Renderdarstellung.Rendermodus` | ✅ Konsistent |
| Sanitizing + Fallback bei Problemen | `Renderdarstellung`, `SanitizingRegel`, Integritätsregeln 6–7 | ✅ Konsistent |
| Keine DB-Schemaänderung | Modellierungsrahmen + Integritätsregel 8 | ✅ Konsistent |

---

## 8. Versionshistorie

| Version | Datum | Autor | Änderung |
|---|---|---|---|
| 1.1.0 | 2026-05-24 | planning-entity-relationship-modeler | ERM auf aktualisierte Anforderungen ausgerichtet: explizite Entitäten `KiRun`, `Schrittblock`, `Renderdarstellung`, Integritätsregeln zu Datums-Header, Schritttrennung und Markdown-Rendermodus ergänzt |
| 1.0.0 | 2026-05-11 | planning-entity-relationship-modeler | Initiale ERM-Fassung erstellt |

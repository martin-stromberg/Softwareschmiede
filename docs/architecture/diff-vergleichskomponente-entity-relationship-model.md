# Entity-Relationship-Modell: Diff-Vergleichskomponente

**Version:** 1.0  
**Datum:** 2026-05-15  
**Status:** Freigegeben  
**Kontext:** Erweiterung des Softwareschmiede-ERM um Diff-Persistierung und Caching

---

## Verwandte Dokumente

- [Anforderungsanalyse](../requirements/requirements-analysis.md)
- [Architektur-Blueprint: Diff-Vergleichskomponente](diff-viewer-blueprint.md)
- [Architektur-Blueprint: Softwareschmiede](architecture-blueprint.md)
- [Entity-Relationship-Modell: Softwareschmiede](entity-relationship-model.md)
- [Architektur-Review](../improvements/architecture-review.md)

---

## Inhaltsverzeichnis

1. [Einleitung & Domänen-Analyse](#1-einleitung--domänen-analyse)
2. [ERM-Diagramm (Gesamt)](#2-erm-diagramm-gesamt)
3. [Neue Entitäten für Diff-System](#3-neue-entitäten-für-diff-system)
   - 3.1 [DiffResult](#31-diffresult)
   - 3.2 [DiffBlock](#32-diffblock)
   - 3.3 [DiffLine](#33-diffline)
   - 3.4 [DiffCache](#34-diffcache)
4. [Integrationen mit bestehenden Entities](#4-integrationen-mit-bestehenden-entities)
5. [Beziehungsübersicht (Diff-System)](#5-beziehungsübersicht-diff-system)
6. [Modellierungsentscheidungen](#6-modellierungsentscheidungen)
7. [Attribut-Katalog (Referenztabelle)](#7-attribut-katalog-referenztabelle)
8. [C# Entity-Klassen](#8-c-entity-klassen)
9. [EF Core Konfiguration & Indizes](#9-ef-core-konfiguration--indizes)
10. [Migration-Plan (EF Core Code-First)](#10-migration-plan-ef-core-code-first)
11. [Konsistenzprüfung](#11-konsistenzprüfung)
12. [Performance-Optimierungen](#12-performance-optimierungen)
13. [Querverweise](#13-querverweise)

---

## 1. Einleitung & Domänen-Analyse

### Kontext

Die Diff-Vergleichskomponente erweitert die Softwareschmiede um die Fähigkeit, **Dateiänderungen zwischen zwei Versionen persistent** zu speichern, zu cachen und über eine moderne UI ähnlich VS Code/JetBrains darzustellen.

### Anforderungen

| # | Anforderung | Umsetzung im ERM |
|---|---|---|
| 1 | Persistierung von Diff-Ergebnissen | Entity \DiffResult\ + \DiffBlock\ + \DiffLine\ |
| 2 | Caching mit TTL (24h) | Entity \DiffCache\ für Invalidierung |
| 3 | Integration mit Aufgaben-Kontext | \DiffResult.AufgabeId\ (FK zu \Aufgabe\) |
| 4 | Integration mit Git-Repositories | \DiffResult.GitRepositoryId\ (optional) |
| 5 | Diff-Metadaten (SourceVersion, TargetVersion) | Attribute in \DiffResult\ |
| 6 | Blockweise Änderungen | Entity \DiffBlock\ mit \BlockType\ (Added/Removed/Modified/Context) |
| 7 | Zeilenweise Granularität | Entity \DiffLine\ mit \LineStatus\ |
| 8 | Versionierung & Audit | \DiffResult.GeneratedAt\, \GeneratedBy\, Protokolleintrag-Verweis |
| 9 | Häufige Abfragen optimieren | Composite Indexes, Partitionierung nach Status |
| 10 | Backward-Compatibility | Neues ERM erweitert bestehendes, keine Breaking Changes |

### Domain-Modell-Übersicht

\\\
┌─────────────────────────────────────────────┐
│          Diff-Domäne                        │
├─────────────────────────────────────────────┤
│                                             │
│  DiffResult (Einstiegspunkt)                │
│  ├─ DiffBlock (große Änderungsblöcke)       │
│  │  └─ DiffLine (einzelne Zeilen)           │
│  └─ DiffCache (TTL-basiert)                 │
│                                             │
│  Verknüpfungen:                             │
│  DiffResult → Aufgabe (1:N)                 │
│  DiffResult → GitRepository (0..1:N)        │
│  DiffResult → Protokolleintrag (0..1:1)     │
│                                             │
└─────────────────────────────────────────────┘
\\\


---

## 2. ERM-Diagramm (Gesamt)

\\\mermaid
erDiagram
    %% Bestehende Entities
    Projekt {
        Guid Id PK
        string Name
        string Beschreibung
        DateTimeOffset ErstellungsDatum
        string Status
    }

    GitRepository {
        Guid Id PK
        Guid ProjektId FK
        string PluginTyp
        string RepositoryUrl
        string RepositoryName
        bool Aktiv
    }

    Aufgabe {
        Guid Id PK
        Guid ProjektId FK
        Guid GitRepositoryId FK
        string Titel
        string Status
        string BranchName
        DateTimeOffset ErstellungsDatum
    }

    Protokolleintrag {
        Guid Id PK
        Guid AufgabeId FK
        string Typ
        string Inhalt
        DateTimeOffset Zeitstempel
    }

    %% Neue Entities für Diff-System
    DiffResult {
        Guid Id PK
        Guid AufgabeId FK
        Guid GitRepositoryId FK "optional"
        Guid ProtokollEintragId FK "optional"
        string FilePath
        string SourceVersion
        string TargetVersion
        string DiffType "Full | SideBySide | Split"
        int LineCount
        int AddedLines
        int RemovedLines
        int ModifiedLines
        string Status "Pending | Generated | Cached | Error"
        DateTimeOffset GeneratedAt
        string GeneratedBy
        string SourceContent "optional, nullable"
        string TargetContent "optional, nullable"
        DateTimeOffset ExpiresAt "optional für Cache-TTL"
    }

    DiffBlock {
        Guid Id PK
        Guid DiffResultId FK
        string BlockType "Added | Removed | Modified | Context"
        int SourceStartLine
        int SourceEndLine
        int TargetStartLine
        int TargetEndLine
        int BlockSequence
    }

    DiffLine {
        Guid Id PK
        Guid DiffBlockId FK
        string LineStatus "Added | Removed | Modified | Context"
        string Content
        int SourceLineNumber "optional"
        int TargetLineNumber "optional"
        int LineSequence
    }

    DiffCache {
        Guid Id PK
        Guid DiffResultId FK "unique"
        string CacheKey
        string CachedData "BLOB oder JSON"
        DateTimeOffset CachedAt
        DateTimeOffset ExpiresAt
        string CachingStrategy "TTL | LRU | Manual"
        bool IsValid
    }

    %% Beziehungen: Bestehend
    Projekt ||--o{ GitRepository : "hat"
    Projekt ||--o{ Aufgabe : "enthält"
    GitRepository |o--o{ Aufgabe : "ist zugeordnet"
    Aufgabe ||--o{ Protokolleintrag : "protokolliert"

    %% Beziehungen: Neu (Diff-System)
    Aufgabe ||--o{ DiffResult : "erzeugt"
    GitRepository ||--o{ DiffResult : "hat"
    Protokolleintrag |o--o| DiffResult : "referenziert"
    DiffResult ||--o{ DiffBlock : "enthält"
    DiffBlock ||--o{ DiffLine : "enthält"
    DiffResult ||--|| DiffCache : "verwendet"
\\\

---

## 3. Neue Entitäten für Diff-System

### 3.1 DiffResult

**Beschreibung:** Übergeordneter Container für einen Diff-Vergleich zwischen zwei Dateiversionen. Speichert Metadaten, Statistiken und optionale Quellinhalte.

**Primärschlüssel:** \Id\  
**Fremdschlüssel:** 
- \AufgabeId → Aufgabe.Id\ (NOT NULL)
- \GitRepositoryId → GitRepository.Id\ (nullable, optional)
- \ProtokollEintragId → Protokolleintrag.Id\ (nullable, optional)

| Attribut | Typ | Constraint | Beschreibung |
|---|---|---|---|
| \Id\ | \Guid\ | PK, NOT NULL | Eindeutiger Bezeichner des Diff-Ergebnisses |
| \AufgabeId\ | \Guid\ | FK, NOT NULL | Zugehörige Aufgabe (jeder Diff gehört zu einer Aufgabe) |
| \GitRepositoryId\ | \Guid?\ | FK, nullable | Optionale Zuordnung zum Git-Repository (für Repository-Kontext) |
| \ProtokollEintragId\ | \Guid?\ | FK, nullable, UNIQUE | Optionaler Verweis auf Protokolleintrag (wenn Diff als Ereignis protokolliert wird) |
| \FilePath\ | \string\ | NOT NULL, max. 500 Zeichen | Relative Dateipfad im Repository (z. B. \src/App.razor\) |
| \SourceVersion\ | \string\ | NOT NULL, max. 100 Zeichen | Quellversion (Branch/Commit-Hash/Tag, z. B. \main\ oder \bc1234\) |
| \TargetVersion\ | \string\ | NOT NULL, max. 100 Zeichen | Zielversion (Branch/Commit-Hash/Tag, z. B. \eature/x\ oder \def5678\) |
| \DiffType\ | \string (Enum)\ | NOT NULL, Default: \Full\ | Diff-Renderingtyp: \Full\ (Unified), \SideBySide\ (Zwei Spalten), \Split\ (Split-View) |
| \LineCount\ | \int\ | NOT NULL, Default: 0 | Gesamtzahl der Zeilen im Diff |
| \AddedLines\ | \int\ | NOT NULL, Default: 0 | Zahl hinzugefügter Zeilen |
| \RemovedLines\ | \int\ | NOT NULL, Default: 0 | Zahl gelöschter Zeilen |
| \ModifiedLines\ | \int\ | NOT NULL, Default: 0 | Zahl modifizierter Zeilen |
| \Status\ | \string (Enum)\ | NOT NULL, Default: \Pending\ | Status: \Pending\, \Generated\, \Cached\, \Error\ |
| \GeneratedAt\ | \DateTimeOffset\ | NOT NULL | Zeitstempel der Diff-Generierung |
| \GeneratedBy\ | \string\ | NOT NULL, max. 200 Zeichen | Agent/Service-Name, der den Diff generiert hat (z. B. \DiffGeneratorService\, \GitDiffCommand\) |
| \SourceContent\ | \string?\ | NULL | **Optional:** Vollinhalt der Quelldatei (für kleine Dateien; nullable für große Dateien) |
| \TargetContent\ | \string?\ | NULL | **Optional:** Vollinhalt der Zieldatei (für kleine Dateien; nullable für große Dateien) |
| \ExpiresAt\ | \DateTimeOffset?\ | NULL | Ablaufzeit für Caching-Zwecke (TTL-Invalidierung); null = keine Expiration |

---

### 3.2 DiffBlock

**Beschreibung:** Größere zusammenhängende Änderungsblöcke innerhalb eines Diffs. Ein \DiffResult\ kann mehrere \DiffBlock\-Einträge enthalten (z. B. wenn mehrere nicht-zusammenhängende Abschnitte des Diffs Änderungen haben).

**Primärschlüssel:** \Id\  
**Fremdschlüssel:** \DiffResultId → DiffResult.Id\

| Attribut | Typ | Constraint | Beschreibung |
|---|---|---|---|
| \Id\ | \Guid\ | PK, NOT NULL | Eindeutiger Bezeichner des Diff-Blocks |
| \DiffResultId\ | \Guid\ | FK, NOT NULL | Zugehöriges DiffResult (kaskadierendes Löschen) |
| \BlockType\ | \string (Enum)\ | NOT NULL | Blocktyp: \Added\ (neuer Block), \Removed\ (gelöschter Block), \Modified\ (veränderter Block), \Context\ (Kontextzeilen) |
| \SourceStartLine\ | \int\ | NOT NULL | Startzeile in der Quelldatei (0 = nicht vorhanden für Added-Blöcke) |
| \SourceEndLine\ | \int\ | NOT NULL | Endzeile in der Quelldatei |
| \TargetStartLine\ | \int\ | NOT NULL | Startzeile in der Zieldatei (0 = nicht vorhanden für Removed-Blöcke) |
| \TargetEndLine\ | \int\ | NOT NULL | Endzeile in der Zieldatei |
| \BlockSequence\ | \int\ | NOT NULL | Reihenfolge des Blocks im Diff (für korrekte Sortierung beim Rendering) |

---

### 3.3 DiffLine

**Beschreibung:** Einzelne Zeilen mit Änderungsstatus. Jede \DiffLine\ gehört zu einem \DiffBlock\.

**Primärschlüssel:** \Id\  
**Fremdschlüssel:** \DiffBlockId → DiffBlock.Id\

| Attribut | Typ | Constraint | Beschreibung |
|---|---|---|---|
| \Id\ | \Guid\ | PK, NOT NULL | Eindeutiger Bezeichner der Zeile |
| \DiffBlockId\ | \Guid\ | FK, NOT NULL | Zugehöriger DiffBlock (kaskadierendes Löschen) |
| \LineStatus\ | \string (Enum)\ | NOT NULL | Zeilenstatus: \Added\, \Removed\, \Modified\, \Context\ |
| \Content\ | \string\ | NOT NULL | Zeileninhalt (Leerzeichen/Tabs beibehalten für korrekte Renderierung) |
| \SourceLineNumber\ | \int?\ | nullable | Zeilennummer in der Quelldatei (null für Added-Zeilen) |
| \TargetLineNumber\ | \int?\ | nullable | Zeilennummer in der Zieldatei (null für Removed-Zeilen) |
| \LineSequence\ | \int\ | NOT NULL | Reihenfolge der Zeile im Block (für korrekte Sortierung) |

---

### 3.4 DiffCache

**Beschreibung:** Caching-Management-Entity für TTL-basierte Invalidierung. Ein \DiffCache\ speichert gecachte Diff-Daten und verwaltet Ablaufinformationen.

**Primärschlüssel:** \Id\  
**Fremdschlüssel:** \DiffResultId → DiffResult.Id\ (UNIQUE, 1:1-Beziehung)

| Attribut | Typ | Constraint | Beschreibung |
|---|---|---|---|
| \Id\ | \Guid\ | PK, NOT NULL | Eindeutiger Bezeichner des Cache-Eintrags |
| \DiffResultId\ | \Guid\ | FK, NOT NULL, UNIQUE | Zugehöriges DiffResult (1:1, nur ein Cache pro Diff) |
| \CacheKey\ | \string\ | NOT NULL, max. 300 Zeichen, UNIQUE | Eindeutiger Cache-Schlüssel, z. B. \sha256(AufgabeId + FilePath + Source + Target)\ |
| \CachedData\ | \string\ | NOT NULL | Gecachte Diff-Daten (JSON-serialisiert oder BLOB-Format) |
| \CachedAt\ | \DateTimeOffset\ | NOT NULL | Zeitstempel der Cache-Erstellung |
| \ExpiresAt\ | \DateTimeOffset\ | NOT NULL | Ablaufzeitpunkt des Cache (aktuell: \CachedAt + 24h\) |
| \CachingStrategy\ | \string (Enum)\ | NOT NULL, Default: \TTL\ | Caching-Strategie: \TTL\ (zeitbasiert), \LRU\ (least recently used), \Manual\ (manuelles Invalidieren) |
| \IsValid\ | \ool\ | NOT NULL, Default: \	rue\ | Gibt an, ob der Cache noch gültig ist (kann zur manuellen Invalidierung auf \alse\ gesetzt werden) |


---

## 4. Integrationen mit bestehenden Entities

### Aufgabe ↔ DiffResult (1:N)

| Aspekt | Details |
|---|---|
| Beziehung | Eine Aufgabe kann mehrere Diffs haben (z. B. für verschiedene Dateien oder Vergleiche über mehrere Commit-Ranges) |
| Fremdschlüssel | \DiffResult.AufgabeId → Aufgabe.Id\ (NOT NULL) |
| Delete-Verhalten | **Cascade**: Wenn eine Aufgabe gelöscht wird, werden alle zugehörigen DiffResults und deren Kinder (DiffBlocks, DiffLines, DiffCache) gelöscht |
| Lazy Loading | FALSE (gemäß Architektur-Blueprint: alle Relationships sind explizit mit Include() zu laden) |

**Use Case:**
\\\csharp
// Eine Aufgabe hat mehrere Diffs (z. B. für Datei-A, Datei-B, Datei-C)
var aufgabe = await dbContext.Aufgaben
    .Include(a => a.DiffResults)
        .ThenInclude(dr => dr.DiffBlocks)
            .ThenInclude(db => db.DiffLines)
    .FirstOrDefaultAsync(a => a.Id == aufgabeId);
\\\

---

### GitRepository ↔ DiffResult (0..1:N)

| Aspekt | Details |
|---|---|
| Beziehung | Ein GitRepository kann mehrere Diffs haben; ein Diff kann optional zu einem Repository gehören |
| Fremdschlüssel | \DiffResult.GitRepositoryId → GitRepository.Id\ (nullable) |
| Delete-Verhalten | **SetNull**: Wenn ein Repository gelöscht wird, wird \DiffResult.GitRepositoryId\ auf NULL gesetzt (Diff bleibt erhalten) |
| Rationale | Diff-Kontext ist nicht primär vom Repository abhängig; der Repository-Verweis ist optional für Zusatzinformationen |

**Use Case:**
\\\csharp
// Alle Diffs für ein bestimmtes Repository abrufen
var diffs = await dbContext.DiffResults
    .Where(dr => dr.GitRepositoryId == repositoryId)
    .Include(dr => dr.Aufgabe)
    .ToListAsync();
\\\

---

### Protokolleintrag ↔ DiffResult (0..1:1)

| Aspekt | Details |
|---|---|
| Beziehung | Optionale 1:1-Beziehung: Ein Diff kann optional einen Protokolleintrag haben; ein Protokolleintrag kann maximal einen Diff referenzieren |
| Fremdschlüssel | \DiffResult.ProtokollEintragId → Protokolleintrag.Id\ (nullable, UNIQUE) |
| Delete-Verhalten | **SetNull**: Wenn ein Diff gelöscht wird, wird der Protokolleintrag behalten und \ProtokollEintragId\ wird NULL gesetzt |
| Protokoll-Typ | Neu: \Protokolleintrag.Typ = "DiffGenerated"\ (zusätzlich zu bestehenden Typen: Prompt, Antwort, StatusUebergang, TestErgebnis) |

**Use Case:**
\\\csharp
// Diff-Generierung protokollieren
var diffResult = new DiffResult { ... };
await dbContext.DiffResults.AddAsync(diffResult);
await dbContext.SaveChangesAsync();

var protokoll = new Protokolleintrag
{
    AufgabeId = aufgabeId,
    Typ = "DiffGenerated",
    Inhalt = \$"Diff for {diffResult.FilePath} generated (SourceVersion={diffResult.SourceVersion}, TargetVersion={diffResult.TargetVersion})",
    Zeitstempel = DateTimeOffset.UtcNow
};
diffResult.ProtokollEintragId = protokoll.Id;
await dbContext.SaveChangesAsync();
\\\

---

### DiffResult ↔ DiffBlock (1:N) [Identifizierende Beziehung]

| Aspekt | Details |
|---|---|
| Beziehung | Ein DiffResult kann mehrere DiffBlocks haben; jeder DiffBlock gehört zu genau einem DiffResult |
| Fremdschlüssel | \DiffBlock.DiffResultId → DiffResult.Id\ (NOT NULL) |
| Delete-Verhalten | **Cascade**: Wenn DiffResult gelöscht wird, werden alle DiffBlocks (und deren DiffLines) gelöscht |
| Composite PK | Optional: \(DiffResultId, BlockSequence)\ für zusätzliche Eindeutigkeit |

---

### DiffBlock ↔ DiffLine (1:N) [Identifizierende Beziehung]

| Aspekt | Details |
|---|---|
| Beziehung | Ein DiffBlock kann mehrere DiffLines haben; jede DiffLine gehört zu genau einem DiffBlock |
| Fremdschlüssel | \DiffLine.DiffBlockId → DiffBlock.Id\ (NOT NULL) |
| Delete-Verhalten | **Cascade**: Wenn DiffBlock gelöscht wird, werden alle DiffLines gelöscht |
| Composite PK | Optional: \(DiffBlockId, LineSequence)\ für zusätzliche Eindeutigkeit |

---

### DiffResult ↔ DiffCache (1:1) [Identifizierende Beziehung]

| Aspekt | Details |
|---|---|
| Beziehung | Ein DiffResult hat maximal einen DiffCache; ein DiffCache gehört zu genau einem DiffResult |
| Fremdschlüssel | \DiffCache.DiffResultId → DiffResult.Id\ (NOT NULL, UNIQUE) |
| Delete-Verhalten | **Cascade**: Wenn DiffResult gelöscht wird, wird auch DiffCache gelöscht |

---

## 5. Beziehungsübersicht (Diff-System)

\\\
┌───────────────────────────────────────────────────────┐
│                  BEZIEHUNGS-ÜBERSICHT                 │
└───────────────────────────────────────────────────────┘

Aufgabe (1)
    ├──→ DiffResult (N) [1:N, Cascade Delete]
    │        ├──→ DiffBlock (N) [1:N, Cascade Delete, Identifying]
    │        │       └──→ DiffLine (N) [1:N, Cascade Delete, Identifying]
    │        │
    │        ├──→ DiffCache (1) [1:1, Cascade Delete, Identifying]
    │        │
    │        └──→ GitRepository (0..1) [0..1:N, Set Null]
    │
    └──→ Protokolleintrag (N)
             └──→ DiffResult (0..1) [0..1:1, Set Null, Unique FK]

Kardinalitäten:
  (1)     = genau eine / genau einer
  (0..1)  = optional / null oder eins
  (N)     = null oder mehr / mehrere

Delete-Verhalten:
  Cascade   = Abhängige Datensätze werden automatisch gelöscht
  Set Null  = FK wird auf NULL gesetzt; Datensatz bleibt erhalten
\\\

---

## 6. Modellierungsentscheidungen

### 6.1 Separate DiffBlock & DiffLine Entities vs. JSON Blob

**Entscheidung:** Separate, strukturierte Entities \DiffBlock\ und \DiffLine\ (keine JSON-Serialisierung der Diff-Struktur).

**Begründung:**
- ✅ **Datenbankabfragen ohne JSON-Parsing:** Zeilen können direkt mit SQL gefiltert werden (z. B. \WHERE LineStatus = 'Added'\)
- ✅ **Performance:** Für große Diffs (tausende Zeilen) schneller als JSON-Deserialisierung
- ✅ **Zukunftserweiterungen:** Annotations/Kommentare auf Zeilenebene einfach möglich (neue Tabelle \DiffLineAnnotation\ ohne Schema-Bruch)
- ✅ **SQLite-Kompatibilität:** JSON-Funktionen in SQLite sind begrenzt; strukturierte Abfragen sind robuster
- ⚠️ **Tradeoff:** Mehr Datenbank-Reads (aber mit Caching-Strategie kompensiert)

---

### 6.2 Optional SourceContent/TargetContent in DiffResult

**Entscheidung:** Optionale Attribute \SourceContent\ und \TargetContent\ in \DiffResult\ (nullable).

**Begründung:**
- ✅ **Speichereffizienz:** Große Dateien (> 500 KB) werden nicht vollständig gespeichert; nur Diff-Struktur
- ✅ **Git-Fallback:** Service kann original-Inhalt aus Git abrufen (via \git show\)
- ✅ **UI-freundlich:** Frontend braucht meist nur Diff-Blöcke, nicht den kompletten Dateiinhalt
- 📋 **Konfigurierbar:** Schwellenwert \MaxContentStorageSize\ definiert, ab wann Content nicht gespeichert wird

**Konfigurationsbeispiel:**
\\\csharp
public class DiffGeneratorOptions
{
    public int MaxContentStorageSizeBytes { get; set; } = 512_000; // 500 KB
}
\\\

---

### 6.3 DiffCache als Persistent Entity vs. In-Memory Only

**Entscheidung:** \DiffCache\ als separate Tabelle (nicht nur In-Memory-Cache).

**Begründung:**
- ✅ **App-Restart-Persistierung:** Cache wird bei App-Restart nicht verloren
- ✅ **TTL-Management:** Background-Job kann TTL-abgelaufene Caches einfach mit \WHERE ExpiresAt < DateTime.Now AND IsValid = true\ löschen
- ✅ **Manuelle Invalidierung:** Admin/Service kann Caches selektiv invalidieren (\UPDATE DiffCache SET IsValid = false WHERE ...\)
- ✅ **Analytics:** Caching-Statistiken können direkt abgerufen werden
- ⚠️ **Extra Schreiblast:** Aber typischerweise werden Caches selten invalidiert, Lese-Last dominiert

---

### 6.4 Integration mit Protokolleintrag (Optional)

**Entscheidung:** Optionale 1:1-Beziehung zu \Protokolleintrag\ mit neuem Typ \DiffGenerated\.

**Begründung:**
- ✅ **Audit Trail:** Wichtige Diff-Generierungen können protokolliert werden
- ✅ **Optionalität:** Nicht jeder Diff muss protokolliert werden (nur signifikante)
- ✅ **Änderungsverfolgung:** Wann wurde ein Diff generiert? Von welchem Service? (\GeneratedBy\ im Protokoll)
- ⚠️ **Schema-Erweiterung:** \Protokolleintrag.Typ\ muss neuen Wert \DiffGenerated\ hinzufügen

**Neue Enum-Werte:**
\\\csharp
public enum ProtokollEintragTyp
{
    Prompt = 1,
    Antwort = 2,
    StatusUebergang = 3,
    TestErgebnis = 4,
    DiffGenerated = 5  // NEU
}
\\\

---

### 6.5 Keine Breaking Changes für bestehendes Schema

**Entscheidung:** Alle neuen Entities sind rein additiv; bestehende Entities werden NICHT modifiziert (außer \Protokolleintrag.Typ\ um neuen Wert).

**Begründung:**
- ✅ **Backward-Compatibility:** Bestehendes Code läuft ungeändert weiter
- ✅ **Datenmigration:** Keine Daten-Migrationen nötig
- ✅ **Dependency-Management:** Kein Risiko für bestehende Features

---

## 7. Attribut-Katalog (Referenztabelle)

### 7.1 Enum-Werte

#### DiffResult.DiffType

| Wert | Beschreibung | Beispiel-UI |
|---|---|---|
| \Full\ | Unified Diff (durchgehender Diff, default) | VS Code / GitHub Web-Interface |
| \SideBySide\ | Zwei-Spalten-Ansicht (Quelle vs. Ziel) | JetBrains IDEs, GitLab |
| \Split\ | Split-View mit Header | meld, Perforce P4V |

#### DiffResult.Status

| Wert | Beschreibung | Übergänge |
|---|---|---|
| \Pending\ | Diff-Generierung angefordert, aber noch nicht gestartet | → \Generated\ oder \Error\ |
| \Generated\ | Diff erfolgreich generiert, aber nicht gecacht | → \Cached\ oder (TTL-Expiration) |
| \Cached\ | Diff wurde gecacht (Meta in \DiffCache\) | → \Generated\ (bei Cache-Miss) |
| \Error\ | Diff-Generierung fehlgeschlagen (Fehlerdetails in Service-Log) | → \Pending\ (Retry) |

#### DiffBlock.BlockType

| Wert | Beschreibung | Beispiel |
|---|---|---|
| \Added\ | Neue Zeilen hinzugefügt | \+\ in unified diff |
| \Removed\ | Zeilen gelöscht | \-\ in unified diff |
| \Modified\ | Zeilen geändert (bestehende + neue Version) | Wird als Removed + Added gezeigt |
| \Context\ | Kontextzeilen (keine Änderung) | Umgebender Code ohne Änderung |

#### DiffLine.LineStatus

| Wert | Beschreibung |
|---|---|
| \Added\ | Diese Zeile wurde hinzugefügt |
| \Removed\ | Diese Zeile wurde gelöscht |
| \Modified\ | Diese Zeile wurde modifiziert |
| \Context\ | Diese Zeile ist ungeändert (Kontext) |

#### DiffCache.CachingStrategy

| Wert | Beschreibung | Invalidierungsmechanismus |
|---|---|---|
| \TTL\ | Time-To-Live (Standard: 24h) | Ablauf nach ExpiresAt; Background-Job löscht |
| \LRU\ | Least Recently Used | Älteste ungenutzten Caches löschen (Platz/Speicher-Management) |
| \Manual\ | Manuelle Invalidierung | Admin/Service setzt \IsValid = false\ |

---

## 8. C# Entity-Klassen

\\\csharp
// DiffResult.cs
using System;
using System.Collections.Generic;

namespace Softwareschmiede.Domain.Entities
{
    /// <summary>
    /// Übergeordneter Container für einen Diff-Vergleich zwischen zwei Dateiversionen.
    /// </summary>
    public class DiffResult
    {
        public Guid Id { get; set; }
        
        public Guid AufgabeId { get; set; }
        public Aufgabe? Aufgabe { get; set; }
        
        public Guid? GitRepositoryId { get; set; }
        public GitRepository? GitRepository { get; set; }
        
        public Guid? ProtokollEintragId { get; set; }
        public Protokolleintrag? Protokolleintrag { get; set; }
        
        public string FilePath { get; set; } = string.Empty;
        public string SourceVersion { get; set; } = string.Empty;
        public string TargetVersion { get; set; } = string.Empty;
        
        public string DiffType { get; set; } = "Full";
        
        public int LineCount { get; set; } = 0;
        public int AddedLines { get; set; } = 0;
        public int RemovedLines { get; set; } = 0;
        public int ModifiedLines { get; set; } = 0;
        
        public string Status { get; set; } = "Pending";
        
        public DateTimeOffset GeneratedAt { get; set; }
        public string GeneratedBy { get; set; } = string.Empty;
        
        public string? SourceContent { get; set; }
        public string? TargetContent { get; set; }
        
        public DateTimeOffset? ExpiresAt { get; set; }
        
        // Navigation
        public ICollection<DiffBlock> DiffBlocks { get; set; } = new List<DiffBlock>();
        public DiffCache? DiffCache { get; set; }
    }
}

// DiffBlock.cs
using System;
using System.Collections.Generic;

namespace Softwareschmiede.Domain.Entities
{
    /// <summary>
    /// Größere zusammenhängende Änderungsblöcke innerhalb eines Diffs.
    /// </summary>
    public class DiffBlock
    {
        public Guid Id { get; set; }
        
        public Guid DiffResultId { get; set; }
        public DiffResult? DiffResult { get; set; }
        
        public string BlockType { get; set; } = "Context";
        
        public int SourceStartLine { get; set; }
        public int SourceEndLine { get; set; }
        public int TargetStartLine { get; set; }
        public int TargetEndLine { get; set; }
        
        public int BlockSequence { get; set; }
        
        // Navigation
        public ICollection<DiffLine> DiffLines { get; set; } = new List<DiffLine>();
    }
}

// DiffLine.cs
using System;

namespace Softwareschmiede.Domain.Entities
{
    /// <summary>
    /// Einzelne Zeile mit Änderungsstatus innerhalb eines DiffBlock.
    /// </summary>
    public class DiffLine
    {
        public Guid Id { get; set; }
        
        public Guid DiffBlockId { get; set; }
        public DiffBlock? DiffBlock { get; set; }
        
        public string LineStatus { get; set; } = "Context";
        public string Content { get; set; } = string.Empty;
        
        public int? SourceLineNumber { get; set; }
        public int? TargetLineNumber { get; set; }
        
        public int LineSequence { get; set; }
    }
}

// DiffCache.cs
using System;

namespace Softwareschmiede.Domain.Entities
{
    /// <summary>
    /// Caching-Management-Entity für TTL-basierte Invalidierung.
    /// </summary>
    public class DiffCache
    {
        public Guid Id { get; set; }
        
        public Guid DiffResultId { get; set; }
        public DiffResult? DiffResult { get; set; }
        
        public string CacheKey { get; set; } = string.Empty;
        public string CachedData { get; set; } = string.Empty;
        
        public DateTimeOffset CachedAt { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        
        public string CachingStrategy { get; set; } = "TTL";
        public bool IsValid { get; set; } = true;
    }
}
\\\


---

## 9. EF Core Konfiguration & Indizes

\\\csharp
// SoftwareschmiedelDbContext.cs - Auszug der Entity-Konfigurationen

using Microsoft.EntityFrameworkCore;
using Softwareschmiede.Domain.Entities;

namespace Softwareschmiede.Persistence
{
    public class SoftwareschmiedelDbContext : DbContext
    {
        public DbSet<DiffResult> DiffResults { get; set; }
        public DbSet<DiffBlock> DiffBlocks { get; set; }
        public DbSet<DiffLine> DiffLines { get; set; }
        public DbSet<DiffCache> DiffCaches { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ============ DiffResult ============
            modelBuilder.Entity<DiffResult>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.FilePath)
                    .IsRequired()
                    .HasMaxLength(500);

                entity.Property(e => e.SourceVersion)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.TargetVersion)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.DiffType)
                    .IsRequired()
                    .HasDefaultValue("Full")
                    .HasConversion<string>();

                entity.Property(e => e.Status)
                    .IsRequired()
                    .HasDefaultValue("Pending")
                    .HasConversion<string>();

                entity.Property(e => e.GeneratedAt)
                    .IsRequired();

                entity.Property(e => e.GeneratedBy)
                    .IsRequired()
                    .HasMaxLength(200);

                // Foreign Keys
                entity.HasOne(e => e.Aufgabe)
                    .WithMany(a => a.DiffResults)
                    .HasForeignKey(e => e.AufgabeId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.GitRepository)
                    .WithMany(gr => gr.DiffResults)
                    .HasForeignKey(e => e.GitRepositoryId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.Protokolleintrag)
                    .WithOne(p => p.DiffResult)
                    .HasForeignKey<DiffResult>(e => e.ProtokollEintragId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Indizes
                entity.HasIndex(e => e.AufgabeId)
                    .HasDatabaseName("idx_diff_result_aufgabe_id");

                entity.HasIndex(e => new { e.AufgabeId, e.FilePath })
                    .HasDatabaseName("idx_diff_result_aufgabe_filepath");

                entity.HasIndex(e => e.Status)
                    .HasDatabaseName("idx_diff_result_status");

                entity.HasIndex(e => e.ExpiresAt)
                    .HasDatabaseName("idx_diff_result_expires_at");

                entity.HasIndex(e => e.GitRepositoryId)
                    .HasDatabaseName("idx_diff_result_repo_id");
            });

            // ============ DiffBlock ============
            modelBuilder.Entity<DiffBlock>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.BlockType)
                    .IsRequired()
                    .HasConversion<string>();

                entity.Property(e => e.BlockSequence)
                    .IsRequired();

                // Foreign Key
                entity.HasOne(e => e.DiffResult)
                    .WithMany(dr => dr.DiffBlocks)
                    .HasForeignKey(e => e.DiffResultId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Indizes
                entity.HasIndex(e => e.DiffResultId)
                    .HasDatabaseName("idx_diff_block_result_id");

                entity.HasIndex(e => new { e.DiffResultId, e.BlockSequence })
                    .HasDatabaseName("idx_diff_block_result_seq");
            });

            // ============ DiffLine ============
            modelBuilder.Entity<DiffLine>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Content)
                    .IsRequired();

                entity.Property(e => e.LineStatus)
                    .IsRequired()
                    .HasConversion<string>();

                entity.Property(e => e.LineSequence)
                    .IsRequired();

                // Foreign Key
                entity.HasOne(e => e.DiffBlock)
                    .WithMany(db => db.DiffLines)
                    .HasForeignKey(e => e.DiffBlockId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Indizes
                entity.HasIndex(e => e.DiffBlockId)
                    .HasDatabaseName("idx_diff_line_block_id");

                entity.HasIndex(e => e.LineStatus)
                    .HasDatabaseName("idx_diff_line_status");

                entity.HasIndex(e => new { e.DiffBlockId, e.LineSequence })
                    .HasDatabaseName("idx_diff_line_block_seq");
            });

            // ============ DiffCache ============
            modelBuilder.Entity<DiffCache>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.CacheKey)
                    .IsRequired()
                    .HasMaxLength(300);

                entity.Property(e => e.CachedData)
                    .IsRequired();

                entity.Property(e => e.CachingStrategy)
                    .IsRequired()
                    .HasDefaultValue("TTL")
                    .HasConversion<string>();

                entity.Property(e => e.IsValid)
                    .IsRequired()
                    .HasDefaultValue(true);

                // Foreign Key (1:1)
                entity.HasOne(e => e.DiffResult)
                    .WithOne(dr => dr.DiffCache)
                    .HasForeignKey<DiffCache>(e => e.DiffResultId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Indizes
                entity.HasIndex(e => e.DiffResultId)
                    .IsUnique()
                    .HasDatabaseName("idx_diff_cache_result_id_unique");

                entity.HasIndex(e => e.CacheKey)
                    .IsUnique()
                    .HasDatabaseName("idx_diff_cache_key_unique");

                entity.HasIndex(e => e.ExpiresAt)
                    .HasDatabaseName("idx_diff_cache_expires_at");

                entity.HasIndex(e => e.IsValid)
                    .HasDatabaseName("idx_diff_cache_is_valid");
            });
        }
    }
}
\\\

---

## 10. Migration-Plan (EF Core Code-First)

### 10.1 Migration-Erstellung

\\\ash
# PowerShell (im Projektverzeichnis)
dotnet ef migrations add AddDiffComparison --project Softwareschmiede.Persistence
\\\

### 10.2 Generierte Migration-Datei (Beispiel)

\\\csharp
using Microsoft.EntityFrameworkCore.Migrations;
using System;

namespace Softwareschmiede.Persistence.Migrations
{
    public partial class AddDiffComparison : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DiffResult Table
            migrationBuilder.CreateTable(
                name: "DiffResult",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AufgabeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GitRepositoryId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProtokollEintragId = table.Column<Guid>(type: "TEXT", nullable: true),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    SourceVersion = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TargetVersion = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DiffType = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "Full"),
                    LineCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    AddedLines = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    RemovedLines = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    ModifiedLines = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    Status = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "Pending"),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    GeneratedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SourceContent = table.Column<string>(type: "TEXT", nullable: true),
                    TargetContent = table.Column<string>(type: "TEXT", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiffResult", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiffResult_Aufgabe_AufgabeId",
                        column: x => x.AufgabeId,
                        principalTable: "Aufgabe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DiffResult_GitRepository_GitRepositoryId",
                        column: x => x.GitRepositoryId,
                        principalTable: "GitRepository",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DiffResult_Protokolleintrag_ProtokollEintragId",
                        column: x => x.ProtokollEintragId,
                        principalTable: "Protokolleintrag",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            // DiffBlock Table
            migrationBuilder.CreateTable(
                name: "DiffBlock",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DiffResultId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BlockType = table.Column<string>(type: "TEXT", nullable: false),
                    SourceStartLine = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceEndLine = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetStartLine = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetEndLine = table.Column<int>(type: "INTEGER", nullable: false),
                    BlockSequence = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiffBlock", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiffBlock_DiffResult_DiffResultId",
                        column: x => x.DiffResultId,
                        principalTable: "DiffResult",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // DiffLine Table
            migrationBuilder.CreateTable(
                name: "DiffLine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DiffBlockId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LineStatus = table.Column<string>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    SourceLineNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    TargetLineNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    LineSequence = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiffLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiffLine_DiffBlock_DiffBlockId",
                        column: x => x.DiffBlockId,
                        principalTable: "DiffBlock",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // DiffCache Table
            migrationBuilder.CreateTable(
                name: "DiffCache",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DiffResultId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CacheKey = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    CachedData = table.Column<string>(type: "TEXT", nullable: false),
                    CachedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CachingStrategy = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "TTL"),
                    IsValid = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiffCache", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiffCache_DiffResult_DiffResultId",
                        column: x => x.DiffResultId,
                        principalTable: "DiffResult",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Create Indexes
            migrationBuilder.CreateIndex(
                name: "idx_diff_result_aufgabe_id",
                table: "DiffResult",
                column: "AufgabeId");

            migrationBuilder.CreateIndex(
                name: "idx_diff_result_aufgabe_filepath",
                table: "DiffResult",
                columns: new[] { "AufgabeId", "FilePath" });

            migrationBuilder.CreateIndex(
                name: "idx_diff_result_status",
                table: "DiffResult",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "idx_diff_result_expires_at",
                table: "DiffResult",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "idx_diff_result_repo_id",
                table: "DiffResult",
                column: "GitRepositoryId");

            migrationBuilder.CreateIndex(
                name: "idx_diff_block_result_id",
                table: "DiffBlock",
                column: "DiffResultId");

            migrationBuilder.CreateIndex(
                name: "idx_diff_block_result_seq",
                table: "DiffBlock",
                columns: new[] { "DiffResultId", "BlockSequence" });

            migrationBuilder.CreateIndex(
                name: "idx_diff_line_block_id",
                table: "DiffLine",
                column: "DiffBlockId");

            migrationBuilder.CreateIndex(
                name: "idx_diff_line_status",
                table: "DiffLine",
                column: "LineStatus");

            migrationBuilder.CreateIndex(
                name: "idx_diff_line_block_seq",
                table: "DiffLine",
                columns: new[] { "DiffBlockId", "LineSequence" });

            migrationBuilder.CreateIndex(
                name: "idx_diff_cache_result_id_unique",
                table: "DiffCache",
                column: "DiffResultId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_diff_cache_key_unique",
                table: "DiffCache",
                column: "CacheKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_diff_cache_expires_at",
                table: "DiffCache",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "idx_diff_cache_is_valid",
                table: "DiffCache",
                column: "IsValid");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "DiffLine");
            migrationBuilder.DropTable(name: "DiffCache");
            migrationBuilder.DropTable(name: "DiffBlock");
            migrationBuilder.DropTable(name: "DiffResult");
        }
    }
}
\\\

### 10.3 Migrations-Ausführung

\\\ash
# Migrations in die Datenbank anwenden
dotnet ef database update

# Oder mit spezifischer Migration
dotnet ef database update AddDiffComparison
\\\

---

## 11. Konsistenzprüfung

### 11.1 ERM-Validierung gegen Diff-Viewer-Blueprint

| Anforderung (Diff-Viewer-Blueprint) | ERM-Umsetzung | Status |
|---|---|---|
| Persistente Speicherung von Diffs | \DiffResult\, \DiffBlock\, \DiffLine\ Entities | ✅ |
| TTL-basiertes Caching (24h) | \DiffCache\ mit \ExpiresAt\ und \IsValid\-Flag | ✅ |
| Integration mit Aufgaben-Kontext | \DiffResult.AufgabeId → Aufgabe.Id\ | ✅ |
| Git-Repository-Verknüpfung (optional) | \DiffResult.GitRepositoryId → GitRepository.Id\ (nullable) | ✅ |
| Zeilenweise Granularität | \DiffLine\ Entity mit \LineStatus\ | ✅ |
| Blockweise Änderungen | \DiffBlock\ Entity mit \BlockType\ | ✅ |
| Versionsinformationen (SourceVersion, TargetVersion) | \DiffResult.SourceVersion\, \DiffResult.TargetVersion\ | ✅ |
| Diff-Metadaten (Statistiken) | \DiffResult.AddedLines\, \RemovedLines\, \ModifiedLines\, \LineCount\ | ✅ |
| Audit/Protokollierung | \DiffResult.ProtokollEintragId\ (optional); neuer Typ \DiffGenerated\ | ✅ |
| Diff-Renderingtypen (Full/SideBySide/Split) | \DiffResult.DiffType\ Enum | ✅ |
| Content-Speicherung (optional für große Dateien) | \DiffResult.SourceContent\, \DiffResult.TargetContent\ (nullable) | ✅ |
| Status-Management (Pending/Generated/Cached/Error) | \DiffResult.Status\ Enum | ✅ |

### 11.2 Architektur-Konsistenz (gegen Architecture-Blueprint)

| Architektur-Pattern | ERM-Einhaltung | Bemerkung |
|---|---|---|
| No Lazy Loading | ✅ Alle Navigation-Properties benötigen explizite \Include()\ | Abfrageseite muss \ThenInclude()\ nutzen |
| Cascade Delete für hierarchisch (1:N) | ✅ \DiffResult → DiffBlock → DiffLine\ | Löschen eines DiffResult löscht Kinder |
| Set Null für optionale FKs | ✅ \GitRepository\ und \Protokolleintrag\ | Löschen setzt FK auf NULL |
| Enum-Werte als Strings | ✅ \DiffType\, \Status\, \BlockType\, etc. | SQLite-native String-Speicherung |
| Guid als Primärschlüssel | ✅ Alle Entities nutzen \Guid Id\ | Konsistent mit bestehendem Schema |
| SQLite Kompatibilität | ✅ Keine speziellen DB-Features | Index-Syntax, SQL-Typen alle unterstützt |

---

## 12. Performance-Optimierungen

### 12.1 Indizes für häufige Abfragen

| Index | Zweck | Tabelle | Spalten |
|---|---|---|---|
| \idx_diff_result_aufgabe_id\ | Alle Diffs einer Aufgabe | DiffResult | AufgabeId |
| \idx_diff_result_aufgabe_filepath\ | **Composite:** Diffs einer Aufgabe nach Dateiname | DiffResult | (AufgabeId, FilePath) |
| \idx_diff_result_status\ | Filter nach Status (Pending/Generated/Cached/Error) | DiffResult | Status |
| \idx_diff_result_expires_at\ | Background-Job: Abgelaufene Diffs finden | DiffResult | ExpiresAt |
| \idx_diff_result_repo_id\ | Diffs eines Repositories | DiffResult | GitRepositoryId |
| \idx_diff_block_result_seq\ | **Composite:** Blöcke in Reihenfolge abrufen | DiffBlock | (DiffResultId, BlockSequence) |
| \idx_diff_line_block_seq\ | **Composite:** Zeilen in Reihenfolge abrufen | DiffLine | (DiffBlockId, LineSequence) |
| \idx_diff_cache_result_id_unique\ | 1:1 Cache-Lookup | DiffCache | DiffResultId (UNIQUE) |
| \idx_diff_cache_key_unique\ | Cache-Schlüssel-Duplikate verhindern | DiffCache | CacheKey (UNIQUE) |
| \idx_diff_cache_expires_at\ | Background-Job: Abgelaufene Caches invalidieren | DiffCache | ExpiresAt |

### 12.2 Query-Optimierung (Beispiele)

**🔴 Ineffizient (N+1-Problem):**
\\\csharp
var aufgabe = dbContext.Aufgaben.First(a => a.Id == aufgabeId);
foreach (var diff in aufgabe.DiffResults) // Lazy Loading → N weitere DB-Abfragen
{
    foreach (var block in diff.DiffBlocks) // Noch mehr N+1
    {
        // ...
    }
}
\\\

**✅ Optimal (Explicit Includes):**
\\\csharp
var aufgabe = await dbContext.Aufgaben
    .Where(a => a.Id == aufgabeId)
    .Include(a => a.DiffResults)
        .ThenInclude(dr => dr.DiffBlocks)
            .ThenInclude(db => db.DiffLines)
    .AsNoTracking() // Für reine Leseabfragen
    .FirstOrDefaultAsync();
\\\

**Filter nach Zeilenstatus (nutzt Index):**
\\\csharp
var addedLines = await dbContext.DiffLines
    .Where(dl => dl.LineStatus == "Added")
    .Include(dl => dl.DiffBlock)
        .ThenInclude(db => db.DiffResult)
    .AsNoTracking()
    .ToListAsync();
\\\

**Caches invalidieren (TTL-abgelaufen):**
\\\csharp
var expiredCaches = await dbContext.DiffCaches
    .Where(dc => dc.ExpiresAt < DateTime.UtcNow && dc.IsValid)
    .ToListAsync();

foreach (var cache in expiredCaches)
{
    cache.IsValid = false;
}
await dbContext.SaveChangesAsync();
\\\

### 12.3 Datenbankgröße-Kontrolle

| Szenario | Mitigation |
|---|---|
| Sehr große Diffs (tausende Zeilen) | \SourceContent\ und \TargetContent\ nullable; nur für kleine Dateien füllen |
| Viele Diffs über Zeit | Batch-Löschen alter Diffs/Caches mit Background-Job |
| Cache-Speicher wächst unkontrolliert | TTL mit 24h Standard; LRU-Strategie für aggressive Cleanup |

---

## 13. Querverweise

### 13.1 Verwandte Dokumente in diesem Projekt

| Dokument | Zweck | Relevanz |
|---|---|---|
| [Anforderungsanalyse](../requirements/requirements-analysis.md) | Detaillierte Business-Requirements für Diff-Vergleichskomponente | Funktionale Grundlagen |
| [Diff-Viewer-Blueprint](diff-viewer-blueprint.md) | Architektur-Design für Diff-UI und Rendering | Gesamtarchitektur der Feature |
| [Architektur-Blueprint: Softwareschmiede](architecture-blueprint.md) | System-Architektur (Blazor Server, EF Core, SQLite, Plugins) | Pattern und Konventionen |
| [Entity-Relationship-Modell: Softwareschmiede](entity-relationship-model.md) | Bestehendes ERM (Projekt, GitRepository, Aufgabe, Protokolleintrag, etc.) | Basis-Entities und Muster |
| [Architektur-Review](../improvements/architecture-review.md) | Validierung von Architektur-Entscheidungen | Qualitätssicherung |

### 13.2 Implementierungs-Referenzen (nächste Schritte)

| Komponente | Beschreibung | Verantwortung |
|---|---|---|
| \DiffGeneratorService\ | Service zur Generierung von DiffResult/DiffBlock/DiffLine Entities | Backend-Service-Implementierung |
| \DiffCachingService\ | Service zur Cache-Verwaltung (TTL, Invalidierung, Lookup) | Backend-Service-Implementierung |
| \DiffResult.Typ\ Enum | Neue Protokolleintrag-Typ-Wert für Diff-Ereignisse | Domain-Entity Erweiterung |
| \AddDiffComparison\ Migration | EF Core Code-First Migration (oben in Sektion 10) | Datenbank-Schema |
| Background-Job: Cache-Cleanup | Regelmäßige Invalidierung abgelaufener Caches | Scheduling/Hosting-Layer |
| Blazor-Komponenten: DiffViewer | UI-Renderer für Diff-Anzeige (Unified/SideBySide/Split) | Frontend-Implementierung |

### 13.3 Enum-Wert-Erweiterung in Protokolleintrag

**Datei:** \src/Domain/Entities/Protokolleintrag.cs\  
**Änderung:** Neuer Wert in \ProtokollEintragTyp\ Enum

\\\csharp
public enum ProtokollEintragTyp
{
    Prompt = 1,
    Antwort = 2,
    StatusUebergang = 3,
    TestErgebnis = 4,
    DiffGenerated = 5  // NEU für diese Feature
}
\\\

---

## Zusammenfassung

Dieses ERM-Dokument definiert die Datenbank-Schema für die Diff-Vergleichskomponente der Softwareschmiede-Anwendung. Die neuen Entities (\DiffResult\, \DiffBlock\, \DiffLine\, \DiffCache\) werden nahtlos in das bestehende ERM integriert und folgen den Architektur-Patterns:

- ✅ **No Lazy Loading:** Explizite \Include()\-Abfragen erforderlich
- ✅ **Cascade Deletes:** Hierarchische Entities löschen Kinder
- ✅ **SQLite-kompatibel:** Alle Features in SQLite unterstützt
- ✅ **Backward-compatible:** Keine Breaking Changes für bestehendes Code
- ✅ **Performance-optimiert:** Composite Indizes für häufige Abfragen
- ✅ **TTL-Caching:** Persistente DiffCache-Entities mit 24h Standard-TTL

Die Migration kann sofort mit \dotnet ef database update\ angewendet werden; die Schema-Erweiterung ist rein additiv.

---

**Gültig bis:** 2026-05-15 oder bis zur nächsten Überprüfung  
**Zuletzt aktualisiert:** 2026-05-15  
**Status:** ✅ Freigegeben

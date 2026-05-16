# Entity-Relationship-Modell: Repository-Startskript mit freier Portzuweisung

**Version:** 1.0  
**Datum:** 2026-05-14  
**Status:** Entwurf

---

## Verwandte Dokumente

- [Anforderungsanalyse](../requirements/repository-startskript-freier-port-requirements-analysis.md)
- [Architektur-Blueprint](repository-startskript-freier-port-architecture-blueprint.md)
- [Architektur-Review](../improvements/repository-startskript-freier-port-architecture-review.md)

---

## 1. Einleitung

Die Portzuweisung und das Repository-Startskript werden nicht als globale Einstellung modelliert, sondern repositorybezogen gespeichert. Das Skript bleibt als Datei im Repository, die Konfiguration dazu liegt in der Datenbank.

### Abgrenzung

| Datenkategorie | Speicherort | Begründung |
|---|---|---|
| Startskript-Datei | Dateisystem im Repository | Teil des Branch-Contents |
| Freier Port | Laufzeit / In-Memory | Flüchtiger Wert pro Aufgabenstart |
| Repository-Startkonfiguration | Datenbank | Persistente Auswahl und Portstrategie |

---

## 2. ERM-Diagramm

```mermaid
erDiagram

    Projekt {
        Guid Id PK
        string Name
        string Beschreibung "optional"
        DateTimeOffset ErstellungsDatum
        string Status "Enum"
    }

    GitRepository {
        Guid Id PK
        Guid ProjektId FK
        string PluginTyp
        string RepositoryUrl
        string RepositoryName
        bool Aktiv
    }

    RepositoryStartKonfiguration {
        Guid Id PK
        Guid GitRepositoryId FK "unique"
        string StartScriptRelativePath
        string? StartScriptArgumentsTemplate
        string PortModus "Enum: Auto | Fest | ScriptGesteuert"
        int? PortBereichVon
        int? PortBereichBis
        bool Aktiv
    }

    Aufgabe {
        Guid Id PK
        Guid ProjektId FK
        Guid? GitRepositoryId FK
        string Titel
        string Status "Enum"
        string? BranchName
        string? LokalerKlonPfad
    }

    Projekt ||--o{ GitRepository : hat
    GitRepository ||--o| RepositoryStartKonfiguration : besitzt
    Projekt ||--o{ Aufgabe : enthält
    GitRepository |o--o{ Aufgabe : ist_zugeordnet
```

---

## 3. Tabellarische Entitätenübersicht

### 3.1 GitRepository

**Beschreibung:** Repository eines Projekts. Es bleibt die fachliche Klammer für Startskript und Portzuweisung.

| Attribut | Typ | Constraint | Beschreibung |
|---|---|---|---|
| `Id` | `Guid` | PK, NOT NULL | Eindeutiger Bezeichner |
| `ProjektId` | `Guid` | FK, NOT NULL | Zugehöriges Projekt |
| `PluginTyp` | `string` | NOT NULL | Git-Provider |
| `RepositoryUrl` | `string` | NOT NULL | Remote-URL |
| `RepositoryName` | `string` | NOT NULL | Anzeigename |
| `Aktiv` | `bool` | NOT NULL | Aktivitätsflag |

### 3.2 RepositoryStartKonfiguration

**Beschreibung:** Persistierte Startkonfiguration für ein Repository. Sie enthält die ausgewählte Skriptdatei und die Portstrategie.

**Primärschlüssel:** `Id`  
**Fremdschlüssel:** `GitRepositoryId → GitRepository.Id` (1:0..1)

| Attribut | Typ | Constraint | Beschreibung |
|---|---|---|---|
| `Id` | `Guid` | PK, NOT NULL | Eindeutiger Bezeichner |
| `GitRepositoryId` | `Guid` | FK, NOT NULL, UNIQUE | Zugehöriges Repository |
| `StartScriptRelativePath` | `string` | NOT NULL | Relativer Pfad zum Startskript |
| `StartScriptArgumentsTemplate` | `string?` | NULL | Optionales Argumentmuster |
| `PortModus` | `string` (Enum) | NOT NULL | `Auto`, `Fest`, `ScriptGesteuert` |
| `PortBereichVon` | `int?` | NULL | Optionaler Start des Portbereichs |
| `PortBereichBis` | `int?` | NULL | Optionales Ende des Portbereichs |
| `Aktiv` | `bool` | NOT NULL | Konfiguration aktiv? |

### 3.3 Aufgabe

**Beschreibung:** Die Aufgabe nutzt das Repository und dessen Startkonfiguration beim Start des Entwicklungsprozesses.

| Attribut | Typ | Constraint | Beschreibung |
|---|---|---|---|
| `GitRepositoryId` | `Guid?` | FK, NULL | Verknüpftes Repository |
| `BranchName` | `string?` | NULL | Branch der Aufgabe |
| `LokalerKlonPfad` | `string?` | NULL | Lokaler Branch-Klon |

---

## 4. Beziehungsübersicht

| Beziehung | Kardinalität | Bedeutung |
|---|---|---|
| Projekt → GitRepository | 1 : n | Ein Projekt kann mehrere Repositories enthalten. |
| GitRepository → RepositoryStartKonfiguration | 1 : 0..1 | Ein Repository hat optional eine Startkonfiguration. |
| Projekt → Aufgabe | 1 : n | Ein Projekt besitzt mehrere Aufgaben. |
| GitRepository → Aufgabe | 1 : 0..n | Eine Aufgabe kann an ein Repository gebunden sein. |

---

## 5. Modellierungsentscheidungen

1. Die Startkonfiguration liegt als separate 1:1-Entität vor, damit `GitRepository` nicht mit Laufzeitdetails überladen wird.
2. Der Port ist kein persistenter Primärwert, sondern ein Laufzeitkandidat.
3. Das Startskript wird nur als relativer Pfad gespeichert.
4. Der Bereich `PortBereichVon/Bis` ist optional, um spätere Einschränkungen zu erlauben.

---

## 6. Konsistenzprüfung mit dem Architektur-Blueprint

- Das ERM deckt die repositorybezogene Startkonfiguration ab.
- Die Portreservierung bleibt bewusst außerhalb der Persistenz.
- Die Skriptausführung wird nicht als eigene Entität gespeichert, da sie flüchtig ist.


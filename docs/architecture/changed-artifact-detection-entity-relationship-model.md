# ERM – Changed Artifact Detection

## Entitäten

| Entität | Attribute | Beschreibung |
|---|---|---|
| `ChangedArtifact` | `path`, `status`, `category` | Repräsentiert eine geänderte Datei aus Git. |
| `ArtifactCategory` | `codeFiles`, `planningDocs` | Klassifiziert Artefakte in technische und planerische Änderungen. |
| `PlanningDocumentRule` | `basePath`, `extension` | Regelwerk zur Erkennung relevanter Planungsdokumente. |

## Beziehungen

| Von | Beziehung | Nach | Kardinalität |
|---|---|---|---|
| `ChangedArtifact` | wird klassifiziert durch | `ArtifactCategory` | N:1 |
| `ChangedArtifact` | wird geprüft gegen | `PlanningDocumentRule` | N:M |

## Mermaid-Diagramm
```mermaid
erDiagram
    CHANGED_ARTIFACT {
        string path
        string status
        string category
    }
    ARTIFACT_CATEGORY {
        string name
    }
    PLANNING_DOCUMENT_RULE {
        string basePath
        string extension
    }
    CHANGED_ARTIFACT }o--|| ARTIFACT_CATEGORY : classified_as
    CHANGED_ARTIFACT }o--o{ PLANNING_DOCUMENT_RULE : evaluated_by
```

## Konsistenzhinweis
Die Planungsregeln sind auf die in der Anforderungen definierten Pfade abgestimmt:
- `docs/requirements/**/*.md`
- `docs/architecture/**/*.md`
- `docs/improvements/**/*.md`

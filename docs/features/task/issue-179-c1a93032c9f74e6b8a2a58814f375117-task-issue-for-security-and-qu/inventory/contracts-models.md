# Contracts und Modelle

## `IGitPlugin`

**Datei:** `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs`

Der zentrale SCM-Contract enthaelt aktuell:

| Methode | Zweck | Relevanz |
|---------|-------|----------|
| `GetIssuesAsync(string repositoryId, CancellationToken ct)` | Liest offene Issues aus einem Repository. | Aktueller Einstieg fuer offene Anforderungen. |
| `CloneRepositoryAsync`, `CreateBranchAsync`, `PushBranchAsync`, `PullAsync` | Git-Operationen fuer Aufgabenstart und PR-Fluss. | Indirekt betroffen, aber nicht fuer Alert-Lesen. |
| `CreatePullRequestAsync` | Erstellt Pull Requests. | Nicht direkt betroffen. |
| `GetAvailableRepositoriesAsync`, `GetRepositoryStructureAsync` | Repository-Auswahl und Arbeitsverzeichnis. | Nicht direkt betroffen. |

Es gibt keine Methode wie `GetAlertsAsync()` und keinen gemeinsamen Typ fuer "SCM-Anforderung". Damit kann die bestehende Schnittstelle fachlich nicht zwischen Issue-Quelle und Alert-Quelle unterscheiden.

## `GitPluginBase<TPlugin>`

**Datei:** `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/GitPluginBase.cs`

Die Basisklasse implementiert `IGitPlugin`, `IIssueCreateProvider` und `IIssueTemplateProvider`. Sie erzwingt `GetIssuesAsync()` als abstrakte Methode und stellt fuer Issue-Anlage Default-Verhalten bereit:

- `CanCreateIssueAsync()` gibt standardmaessig `false` zurueck.
- `CreateIssueAsync()` liefert `NotSupported`.
- `GetIssueTemplatesAsync()` liefert `NotSupported`.

Wenn Alert-Support optional bleiben soll, passt ein Default-Interface- oder Basisklassen-Pattern: Nicht-GitHub-Plugins koennen ohne Alert-Implementierung weiter funktionieren.

## `Issue`

**Datei:** `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/Issue.cs`

Der Record beschreibt normale Provider-Issues:

```csharp
public sealed record Issue(
    int Nummer,
    string Titel,
    string? Body,
    IReadOnlyList<string> Labels,
    string? Milestone,
    string? IssueUrl);
```

Fuer Alerts fehlen fachlich wichtige Felder:

- Quellenart, z. B. `Issue` vs. `Alert`
- stabile Alert-ID oder Alert-URL
- Alert-Typ, z. B. Code Scanning, Dependabot, Secret Scanning
- Schweregrad und Status
- Tool-/Rule-Information
- betroffene Datei, Zeile oder Beschreibung

## `IIssueCreateProvider`

**Datei:** `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IIssueCreateProvider.cs`

Diese optionale Provider-Faehigkeit ist vorhanden und fuer die Anforderung wiederverwendbar:

| Methode | Zweck |
|---------|-------|
| `CanCreateIssueAsync(repositoryId, ct)` | Prueft, ob externe Issue-Anlage moeglich ist. |
| `CreateIssueAsync(repositoryId, IssueCreateRequest, ct)` | Erstellt beim Provider ein Issue. |

## `IssueCreateRequest` und `IssueCreateResult`

**Dateien:**

- `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/IssueCreateRequest.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/IssueCreateResult.cs`

`IssueCreateRequest` enthaelt nur Titel und Body. Das reicht fuer die erste automatische Anlage eines GitHub-Issues aus einem Alert. Labels oder Assignees werden aktuell weder im Contract noch im GitHub-Plugin uebergeben.

`IssueCreateResult` liefert bei Erfolg ein `Issue`. Diese Rueckgabe kann als externe GitHub-Issue-Referenz an der neu erzeugten Aufgabe gespeichert werden.

## Domain-Entity `IssueReferenz`

**Datei:** `src/Softwareschmiede/Domain/Entities/IssueReferenz.cs`

Persistiert an einer Aufgabe derzeit:

- `IssueNummer`
- `Titel`
- `Body`
- `LabelsJson`
- `Milestone`
- `IssueUrl`

Fuer Alerts reicht diese Entity nur fuer das neu angelegte GitHub-Issue. Sie kann nicht abbilden, dass die Aufgabe urspruenglich aus einem Alert stammt. Fuer Duplikatschutz und Nachvollziehbarkeit braucht es entweder eine Erweiterung der Entity oder eine separate Alert-Referenz.


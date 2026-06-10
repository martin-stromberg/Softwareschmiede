# Value Objects & Records

## `AgentInfo`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/AgentInfo.cs`

```csharp
public sealed record AgentInfo(
    string Name,
    string? Beschreibung,
    string DateiPfad
);
```

| Eigenschaft | Typ | Beschreibung |
|---|---|---|
| `Name` | `string` | Name des Agenten |
| `Beschreibung` | `string?` | Optionale Beschreibung des Agenten |
| `DateiPfad` | `string` | Dateipfad zur Agenten-Konfigurationsdatei |

## `PullRequest`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PullRequest.cs`

```csharp
public sealed record PullRequest(
    int Nummer,
    string Titel,
    string Url,
    string BranchName
);
```

| Eigenschaft | Typ | Beschreibung |
|---|---|---|
| `Nummer` | `int` | PR-Nummer im Provider |
| `Titel` | `string` | Titel des Pull Requests |
| `Url` | `string` | URL des PR im Provider |
| `BranchName` | `string` | Name des Quell-Branches |

## `TestResult`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/TestResult.cs`

```csharp
public sealed record TestResult(
    bool Bestanden,
    IReadOnlyList<TestErgebnisInfo> Ergebnisse
);
```

| Eigenschaft | Typ | Beschreibung |
|---|---|---|
| `Bestanden` | `bool` | Gibt an, ob alle Tests bestanden wurden |
| `Ergebnisse` | `IReadOnlyList<TestErgebnisInfo>` | Liste der einzelnen Testergebnisse |

## `TestErgebnisInfo`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/TestResult.cs`

```csharp
public sealed record TestErgebnisInfo(
    string TestName,
    TestStatus Status,
    string? Fehlermeldung,
    TimeSpan Dauer
);
```

| Eigenschaft | Typ | Beschreibung |
|---|---|---|
| `TestName` | `string` | Name des Tests |
| `Status` | `TestStatus` | Status: `Bestanden`, `Fehlgeschlagen`, `Uebersprungen` |
| `Fehlermeldung` | `string?` | Optionale Fehlermeldung bei Fehler |
| `Dauer` | `TimeSpan` | Dauer des Testlaufs |

## `Issue`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/Issue.cs`

```csharp
public sealed record Issue(
    int Nummer,
    string Titel,
    string? Body,
    IReadOnlyList<string> Labels,
    string? Milestone,
    string? IssueUrl
);
```

| Eigenschaft | Typ | Beschreibung |
|---|---|---|
| `Nummer` | `int` | Issue-Nummer im Provider |
| `Titel` | `string` | Titel des Issues |
| `Body` | `string?` | Beschreibungstext |
| `Labels` | `IReadOnlyList<string>` | Labels des Issues |
| `Milestone` | `string?` | Milestone des Issues |
| `IssueUrl` | `string?` | URL des Issues im Provider |

## `PluginSettingGroup`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PluginSettingGroup.cs`

```csharp
public sealed record PluginSettingGroup(
    string GroupName,
    IReadOnlyList<PluginSettingField> Fields
);
```

| Eigenschaft | Typ | Beschreibung |
|---|---|---|
| `GroupName` | `string` | Anzeigename der Gruppe (z.B. "Authentifizierung") |
| `Fields` | `IReadOnlyList<PluginSettingField>` | Felder dieser Gruppe |

## `PluginSettingField`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PluginSettingField.cs`

Beschreibt ein einzelnes Einstellungsfeld eines Plugins. Bestimmt die Art und Weise, wie die Einstellung in der UI dargestellt wird.

## `PluginSettingFieldType`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PluginSettingFieldType.cs`

Enum für die verschiedenen Feldtypen (String, Integer, Boolean, Enum, File-Path, etc.).

## `AgentPackageInfo`
Datei: `src/Softwareschmiede/Domain/ValueObjects/AgentPackageInfo.cs`

Informationen über ein Agentenpaket.

## `GitActionCapabilities`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/GitActionCapabilities.cs`

Beschreibt die verfügbaren Git-Aktionen für ein Plugin.

| Eigenschaft | Typ | Beschreibung |
|---|---|---|
| `RepositoryKind` | `RepositoryKind` | Art des Repositories (z.B. RemoteGit) |
| `IsWorkingDirectoryCopy` | `bool` | Gibt an, ob es sich um eine Working-Directory-Kopie handelt |
| `CanPush` | `bool` | Push möglich? |
| `CanPull` | `bool` | Pull möglich? |
| `CanCreatePullRequest` | `bool` | Pull Request erstellung möglich? |
| `CanMergeToSource` | `bool` | Merge zur Quelle möglich? |

## `CliResult`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/CliResult.cs`

Ergebnis einer CLI-Ausführung.

## `FileTreeNode`
Datei: `src/Softwareschmiede/Domain/ValueObjects/FileTreeNode.cs`

Darstellung eines Knoten im Dateibaum.

## `ArbeitsverzeichnisResolutionResult`
Datei: `src/Softwareschmiede/Domain/ValueObjects/ArbeitsverzeichnisResolutionResult.cs`

Ergebnis der Auflösung eines Arbeitsverzeichnis-Pfads.

## `KiSession`
Datei: Im `KiAusfuehrungsService` definiert

Hält den Zustand einer laufenden KI-Ausführung:
- `IsRunning` – Gibt an, ob Session aktiv läuft
- `GetLines()` – Gibt alle gepufferten Ausgabezeilen zurück
- `Subscribe(onLine)` – Abonniert neue Zeilen

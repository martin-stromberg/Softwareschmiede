# Enums: GitHub-PAT-Authentifizierung

## `PullRequestCompletionStrategy`
Datei: `src/Softwareschmiede.Domain/Enums/PullRequestCompletionStrategy.cs` (referenziert in `GitHubPlugin.cs`, Zeile 59)

| Wert | Bedeutung |
|------|-----------|
| `ApprovalOnly` | Nur genehmigen, nicht mergen |
| `Merge` | Standard-Merge (wird in `CompletePullRequestAsync()` als Standard verwendet) |
| `AutoMerge` | Automatisches Mergen wenn alle Checks bestanden |

**Relevanz für Anforderung:** Wird in PR-Completion-Optionen genutzt, nicht direkt relevant für Token-Separation.

---

## `PullRequestMergeMethod`
Datei: `src/Softwareschmiede.Domain/Enums/PullRequestMergeMethod.cs` (referenziert in `GitHubPlugin.cs`, Zeile 66)

| Wert | Bedeutung |
|------|-----------|
| `Merge` | Merge-Commit erstellen |
| `Rebase` | Rebase und fast-forward |
| `Squash` | Standard-Methode: Alle Commits squashen |

**Relevanz für Anforderung:** Wird in PR-Completion-Optionen genutzt, nicht direkt relevant für Token-Separation.

---

## `PluginType`
Datei: `src/Softwareschmiede.Domain/Enums/PluginType.cs` (referenziert in `GitHubPlugin.cs`, Zeile 31)

| Wert | Bedeutung |
|------|-----------|
| `SourceCodeManagement` | Git-basierte Plugin (GitHub, Bitbucket, etc.) |
| `ArtificialIntelligence` | KI-Provider-Plugin |

**Relevanz für Anforderung:** `GitHubPlugin` ist vom Typ `SourceCodeManagement`, nicht relevant für Token-Separation.

---

## `PluginSettingFieldType`
Datei: `src/Softwareschmiede.Domain/Enums/PluginSettingFieldType.cs` (referenziert in `GitHubPlugin.cs`, Zeile 41)

| Wert | Bedeutung |
|------|-----------|
| `Secret` | Passwort-ähnliches Feld (maskiert in UI) — **Token-Feld nutzt diesen Typ** |
| `Text` | Normale Textfeld |
| `Boolean` | Ja/Nein Checkbox |
| `Enum` | Dropdown-Auswahl |
| `Url` | URL-Eingabefield |

**Relevanz für Anforderung:** Token-Feld ist vom Typ `Secret`, was bedeutet, dass der Wert in der UI maskiert wird und über `ICredentialStore` sicher gespeichert wird.

---

## `WorkflowRunStatus`
Datei: `src/Softwareschmiede.Domain/Enums/WorkflowRunStatus.cs` (wird in `GitHubPlugin` Zeile 1178 genutzt)

| Wert | Bedeutung |
|------|-----------|
| `Queued` | Workflow steht in der Warteschlange |
| `InProgress` | Workflow läuft |
| `Completed` | Workflow abgeschlossen |
| `Unknown` | Status unbekannt |

**Relevanz für Anforderung:** Nicht relevant für Token-Separation.

---

## `WorkflowRunConclusion`
Datei: `src/Softwareschmiede.Domain/Enums/WorkflowRunConclusion.cs` (wird in `GitHubPlugin` Zeile 1184 genutzt)

| Wert | Bedeutung |
|------|-----------|
| `Success` | Erfolgreich |
| `Failure` | Fehlgeschlagen |
| `Cancelled` | Abgebrochen |
| `Skipped` | Übersprungen |
| `TimedOut` | Zeitüberschreitung |
| `ActionRequired` | Aktion erforderlich |
| `Unknown` | Unbekannt |

**Relevanz für Anforderung:** Nicht relevant für Token-Separation.

---

## `PullRequestStatus`
Datei: `src/Softwareschmiede.Domain/Enums/PullRequestStatus.cs` (wird in `GitHubPlugin` Zeile 1071 genutzt)

| Wert | Bedeutung |
|------|-----------|
| `Open` | PR ist offen |
| `Merged` | PR wurde gemerged |
| `Closed` | PR wurde geschlossen ohne zu mergen |
| `Unknown` | Status unbekannt |

**Relevanz für Anforderung:** Nicht relevant für Token-Separation.

---

## `PullRequestMergeStatus`
Datei: `src/Softwareschmiede.Domain/Enums/PullRequestMergeStatus.cs` (wird in `GitHubPlugin` Zeile 1159 genutzt)

| Wert | Bedeutung |
|------|-----------|
| `Mergeable` | PR kann gemerged werden |
| `Conflicting` | Merge-Konflikte vorhanden |
| `Blocked` | Merge ist blockiert (Protected Branch, Draft, etc.) |
| `Merged` | Bereits gemerged |
| `Unknown` | Status unbekannt |

**Relevanz für Anforderung:** Nicht relevant für Token-Separation.

---

## `ScmAlertType`
Datei: `src/Softwareschmiede.Domain/Enums/ScmAlertType.cs` (wird in `GitHubPlugin` Zeile 641 genutzt)

| Wert | Bedeutung |
|------|-----------|
| `CodeScanning` | GitHub Code Scanning Alert |
| `SecretScanning` | GitHub Secret Scanning Alert (nicht im Code verwendet) |
| `DependencyCheck` | Abhängigkeits-Warnung (nicht im Code verwendet) |

**Relevanz für Anforderung:** Nicht relevant für Token-Separation.

---

## `RepositoryStructureLoadStatus`
Datei: `src/Softwareschmiede.Domain/Enums/RepositoryStructureLoadStatus.cs` (wird in `GitHubPlugin` Zeile 1332 genutzt)

| Wert | Bedeutung |
|------|-----------|
| `Success` | Verzeichnisstruktur erfolgreich geladen |
| `Failed` | Fehler beim Laden |

**Relevanz für Anforderung:** Nicht relevant für Token-Separation.

---

## `IssueTemplateLoadResultStatus`
Datei: `src/Softwareschmiede.Domain/Enums/IssueTemplateLoadResultStatus.cs` (wird in `GitHubPlugin` Zeile 503 genutzt)

| Wert | Bedeutung |
|------|-----------|
| `Success` | Templates erfolgreich geladen |
| `Failed` | Fehler beim Laden |

**Relevanz für Anforderung:** Nicht relevant für Token-Separation.

---

## `IssueCreateResultStatus`
Datei: `src/Softwareschmiede.Domain/Enums/IssueCreateResultStatus.cs` (wird in `GitHubPlugin` Zeile 266 genutzt)

| Wert | Bedeutung |
|------|-----------|
| `Success` | Issue erfolgreich erstellt |
| `Failed` | Fehler beim Erstellen |

**Relevanz für Anforderung:** Nicht relevant für Token-Separation.

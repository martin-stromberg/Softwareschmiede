# Logik

## `EntwicklungsprozessService`
Datei: `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`

### Öffentliche Methoden

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `ProzessStartenAsync(Guid, string, string?, string?, CancellationToken)` | `public` | Repository klonen, Branch anlegen, `issue.md` und `.gitignore` schreiben, Aufgabe auf `Gestartet` setzen |
| `ProzessStartenUndCliStartenAsync(Guid, string, string?, string?, CancellationToken)` | `public` | Kombiniert `ProzessStartenAsync` mit dem Start der KI-CLI; Rollback bei Fehler |
| `CliNeustartenAsync(Guid, CancellationToken)` | `public` | Neustart der KI-CLI für eine laufende Aufgabe |
| `CommitDurchfuehrenAsync(Guid, string, CancellationToken)` | `public` | Git-Commit im Klon-Verzeichnis der Aufgabe |
| `ResetDurchfuehrenAsync(Guid, string, string?, CancellationToken)` | `public` | Git-Reset im Klon-Verzeichnis |
| `PushDurchfuehrenAsync(Guid, CancellationToken)` | `public` | Git-Push des Branches |
| `PullDurchfuehrenAsync(Guid, CancellationToken)` | `public` | Git-Pull im Branch |
| `PullRequestErstellenAsync(Guid, CancellationToken)` | `public` | Pull Request über den SCM-Provider erstellen; hängt Closing-Direktive an, wenn `IssueReferenz` vorhanden |
| `AbschliessenAsync(Guid, CancellationToken)` | `public` | Aufgabe abschließen und Protokolleintrag hinzufügen |
| `GetRemoteBranchesAsync(string, string?, CancellationToken)` | `public` | Remote-Branches des Repositories abfragen |
| `RepositoryStartskriptAusfuehrenAsync(Guid, CancellationToken)` | `public` | Repository-Startskript für eine bereits gestartete Aufgabe ausführen |

### Private Methoden (relevant für die Anforderung)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| **`CreateIssueFileAsync(string, Aufgabe, string, RepositoryStartKonfiguration?, CancellationToken)`** | **`private`** | **Erstellt `issue.md` im effektiven Arbeitsverzeichnis; erzeugt derzeit einen Markdown-Block mit Titel, ID, Branch und `AnforderungsBeschreibung`. Die `IssueReferenz` der `Aufgabe` wird noch nicht ausgewertet.** |
| `UpdateGitignoreAsync(string, RepositoryStartKonfiguration?, CancellationToken)` | `private` | Trägt `issue.md` in `.gitignore` ein, falls noch nicht vorhanden |
| `FinalizeStartAsync(...)` | `private` | Ruft `CreateIssueFileAsync` und `UpdateGitignoreAsync` auf, setzt danach den Status |
| `RollbackStartAsync(Guid, CancellationToken)` | `private` | Setzt Aufgabe nach einem fehlerhaften Start zurück |
| `ResolvePluginAsync(...)` | `private` | Löst das passende Git-Plugin für das Repository auf |
| `PrepareCloneDirectoryAsync(...)` | `private` | Klont das Repository in ein temporäres Verzeichnis |
| `SetupBranchAsync(...)` | `private` | Ermittelt und legt den Feature-Branch an (berücksichtigt `IssueNummer` für den Branch-Namen) |
| `ResolveRepositoryAsync(...)` | `private` | Löst das Git-Repository anhand von URL oder Projekt-Zuordnung auf |
| `EnsureEffectiveWorkingDirectory(string, RepositoryStartKonfiguration?)` | `private static` | Bestimmt das effektive Arbeitsverzeichnis und erstellt es bei Bedarf |
| `RunInitialisierungsskriptAsync(...)` | `private` | Führt optionales Initialisierungsskript aus |
| `RunStartskriptAsync(...)` | `private` | Führt optionales Startskript aus |
| `ValidateBaseBranchExistsAsync(...)` | `private static` | Prüft, ob der Basis-Branch im Remote existiert |

### Aktueller Markdown-Inhalt der `issue.md` (Stand vor der Anforderung)

`CreateIssueFileAsync` generiert heute folgenden Inhalt:

```markdown
# Aufgabe: {aufgabe.Titel}

**Aufgaben-ID:** {aufgabe.Id}
**Branch:** {branchName}
**Erstellt:** {aufgabe.ErstellungsDatum:yyyy-MM-dd}

## Anforderung

{beschreibung}
```

Ein Issue-Abschnitt aus `aufgabe.IssueReferenz` (mit `IssueNummer` und `Titel`) fehlt vollständig.

### Aufrufkette bis `CreateIssueFileAsync`

```
ProzessStartenAsync / ProzessStartenUndCliStartenAsync
  └─ FinalizeStartAsync
       └─ CreateIssueFileAsync   ← zu erweiternde Methode
```

Das `aufgabe`-Objekt kommt aus `_aufgabeService.GetDetailAsync()`, das `IssueReferenz` bereits per Eager Loading enthält (`.Include(a => a.IssueReferenz)`).

---

## `AufgabeService`
Datei: `src/Softwareschmiede/Application/Services/AufgabeService.cs`

Relevant: `GetDetailAsync` lädt die `IssueReferenz` bereits vollständig per Eager Loading. Das im `EntwicklungsprozessService` übergebene `aufgabe`-Objekt ist damit immer befüllt — kein zusätzliches Laden nötig.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `GetDetailAsync(Guid, CancellationToken)` | `public` | Lädt Aufgabe inklusive `IssueReferenz`, `AlertReferenz`, `PullRequests`, `GitRepository` und `Projekt` per Eager Loading |

# Service- und Prozesslogik

## `AufgabeService`

**Datei:** `src/Softwareschmiede/Application/Services/AufgabeService.cs`

| Methode | Zeile | Relevanz |
|---------|-------|----------|
| `GetByProjektAsync(...)` | 32-41 | Laedt Aufgabenlisten inklusive `IssueReferenz`. |
| `GetDetailAsync(...)` | 79-91 | Laedt Aufgabe inklusive `Projekt`, `IssueReferenz`, `GitRepository` und Protokoll; wird fuer PR-Erstellung genutzt. |
| `CreateFromIssueAsync(...)` | 167-205 | Erstellt Aufgabe und `IssueReferenz` aus einem `Issue`-Value-Object. |
| `UpdateIssueReferenzAsync(...)` | 229-276 | Setzt, aktualisiert oder entfernt die Issue-Referenz einer bestehenden Aufgabe. |
| `StartenAsync(...)` | 343-356 | Persistiert `Status = Gestartet`, `BranchName` und `LokalerKlonPfad`. |
| `AbschliessenAsync(...)` | 393-407 | Setzt `BranchName` und `LokalerKlonPfad` beim Abschluss zurueck. |

Die Issue-Referenz wird beim Starten nicht ueberschrieben. Dadurch bleibt die Verknuepfung von der Issue-Erstellung bis zur PR-Erstellung erhalten.

## `EntwicklungsprozessService`

**Datei:** `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`

| Methode | Zeile | Relevanz |
|---------|-------|----------|
| `ProzessStartenAsync(...)` | 84-108 | Laedt Aufgabe per `GetDetailAsync(...)`, bereitet Klon und Branch vor. |
| `SetupBranchAsync(...)` | 436-466 | Entscheidet zwischen vorhandenem Branch und neuem Task-Branch. |
| `FinalizeStartAsync(...)` | 468-510 | Erstellt `issue.md`, aktualisiert `.gitignore`, ruft `StartenAsync(...)` auf. |
| `ErstelleTaskBranchName(...)` | 558-566 | Nutzt `IssueReferenz.IssueNummer` fuer Branch-Praefix `task/issue-<nr>-...`. |
| `CreateIssueFileAsync(...)` | 590-622 | Schreibt Kontextdatei in den Klon; kein Einfluss auf PR-Closing. |
| `PullRequestErstellenAsync(Guid, string, string, string, ...)` | 282-306 | Aelterer PR-Pfad mit explizitem Repository/Title/Body; wertet keine Issue-Referenz aus. |

Der Hauptpfad fuer Branch-Erstellung ist bereits issue-aware. Der alternative PR-Pfad in `EntwicklungsprozessService` ist ein Risiko, falls er noch von UI oder Tests verwendet wird.

## `GitOrchestrationService`

**Datei:** `src/Softwareschmiede/Application/Services/GitOrchestrationService.cs`

| Methode | Zeile | Relevanz |
|---------|-------|----------|
| `PullRequestErstellenAsync(Guid, string?, string?, ...)` | 178-214 | Hauptpfad fuer PR-Erstellung anhand einer Aufgabe. |
| `BuildPullRequestBody(Aufgabe, string?)` | 216-235 | Baut PR-Body und ergaenzt aktuell bereits `Closes #<IssueNummer>`. |
| `ContainsClosingDirectiveForIssue(string, int)` | 237-246 | Erkennt vorhandene Closing-Direktiven fuer dieselbe Issue. |
| `ResolveRepositoryIdAsync(Aufgabe, CancellationToken)` | 282-312 | Ermittelt `owner/repo` aus verknuepftem oder einzig aktivem Repository. |
| `ResolveGitPluginAsync(Aufgabe, CancellationToken)` | 314-326 | Waehlt das passende SCM-Plugin. |

### Vorhandenes Verhalten im aktuellen Arbeitsbaum

- Ohne Branch bricht `PullRequestErstellenAsync(...)` kontrolliert ab.
- Ohne expliziten Titel wird `aufgabe.Titel` genutzt.
- Ohne Body wird `Automatisch erstellt fuer Aufgabe: {Titel}` genutzt.
- Bei `IssueReferenz.IssueNummer > 0` wird `Closes #<IssueNummer>` an den Body angehaengt.
- Ein reiner Whitespace-Body wird durch die Closing-Direktive ersetzt.
- Eine vorhandene Closing-Direktive fuer dieselbe Issue wird nicht dupliziert.
- Closing-Direktiven fuer andere Issues bleiben erhalten; die aktuelle Issue wird angehaengt.
- Der Protokolleintrag enthaelt bei gueltiger Issue-Nummer einen Hinweis auf Auto-Close.

Diese Logik entspricht dem geforderten Implementierungsansatz und sollte in der Planung als vorhandener Stand beruecksichtigt werden.

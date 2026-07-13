# Detailinventar - SCM-Plugins und Remote-Abruf

## Gemeinsame Schnittstelle

`IGitPlugin` definiert `GetRepositoryStructureAsync(string repositoryUrl, int maxDepth = 2, CancellationToken ct = default)`. Die Methode liefert `IEnumerable<RepositoryDirectoryEntry>`, wobei `RepositoryDirectoryEntry` nur `Path` und `IsDirectory` enthaelt.

Folge: Die Schnittstelle kennt keinen Erfolgs-/Fehlerstatus. Ein leeres Ergebnis kann bedeuten:

- Repository besitzt keine Unterverzeichnisse.
- URL konnte nicht geparst werden.
- Remote-API war nicht erreichbar.
- Authentifizierung oder Berechtigung fehlte.
- Plugin unterstuetzt den Abruf nicht oder wirft eine Exception.

## GitHub-Referenz

`GitHubPlugin.GetRepositoryStructureAsync` beginnt bei Zeile 606. Der Ablauf:

- Repository-ID aus HTTPS- oder SSH-URL extrahieren (`TryExtractRepositoryId`, Zeile 684).
- Default-Branch ueber `git ls-remote --symref ... HEAD` ermitteln (`GetDefaultBranchAsync`, Zeile 570).
- Remote-Struktur ueber `gh api repos/{repositoryId}/git/trees/{branch}?recursive=1` laden.
- Nur Eintraege mit `type == "tree"` und Tiefe `<= maxDepth` als Verzeichnisse zurueckgeben.
- Fehler beim API-Aufruf, ungueltige URL oder JSON-Parse-Fehler fuehren zu `[]`.

Das bestehende GitHub-Verhalten ist funktional und durch Tests abgedeckt. Es muss beim Fallback-Umbau erhalten bleiben.

## Bitbucket-Implementierung

`BitbucketPlugin.GetRepositoryStructureAsync` beginnt bei Zeile 696. Der Ablauf:

- Repository-ID aus Cloud-, Browser-, API- oder SCM-URL extrahieren (`TryExtractRepositoryId`, Zeile 1007).
- Default-Branch ueber `GetDefaultBranchAsync` ermitteln (Zeile 670).
- Hosting-Modus aus `Softwareschmiede.Bitbucket.HostingMode` lesen.
- Bei `SelfHosted`: `GetSelfHostedRepositoryStructureAsync` verwenden.
- Sonst: `GetCloudRepositoryStructureAsync` verwenden.

### Bitbucket Cloud

`GetCloudRepositoryStructureAsync` beginnt bei Zeile 728.

- Nutzt die Source-API: `/2.0/repositories/{repositoryId}/src/{branch}/?max_depth={maxDepth}&pagelen=100`.
- Folgt dem `next`-Link bis maximal 50 Seiten.
- `ParseCloudSourcePage` filtert `type == "commit_directory"`.
- Dateien werden ignoriert.
- API-Fehlerpayloads und Parse-Fehler beenden den Abruf und liefern keine explizite Fehlersemantik nach oben.

### Bitbucket Self-Hosted/Data Center

`GetSelfHostedRepositoryStructureAsync` beginnt bei Zeile 839.

- Nutzt die Browse-API levelweise, weil die Self-Hosted-API keine Cloud-kompatible rekursive Source-API bietet.
- `FetchSelfHostedDirectoryChildrenAsync` beginnt bei Zeile 888 und paginiert ueber `isLastPage`/`nextPageStart`.
- Es werden nur Eintraege mit `type == "DIRECTORY"` uebernommen.
- Bei fehlgeschlagenem Browse-Aufruf wird die bis dahin bekannte Liste zurueckgegeben, im Root-Fehlerfall also `[]`.

## Bewertung

Der Bitbucket-Remote-Abruf ist fuer die Akzeptanzkriterien 1 und 2 bereits weitgehend vorhanden. Die Umsetzung sollte diesen Code nur anfassen, falls ein Ergebnisstatus eingefuehrt werden soll, der Fehler explizit nach oben transportiert. Ein kompletter Neuaufbau des Bitbucket-Abrufs waere unnoetig riskant.

## Risiken

- Aktuelle Fehler werden in den Plugin-Methoden teilweise in leere Listen umgewandelt. Ein neuer Fallback-Zustand laesst sich nur sauber anzeigen, wenn Fehler nicht schon zu `[]` normalisiert werden oder wenn ein neuer Result-Typ verwendet wird.
- Ein leeres Repository und ein API-Fehler sind aktuell nicht unterscheidbar.
- Der Cache-Key des `DirectoryStructureBrowserService` enthaelt nur die Repository-URL, nicht Plugin-Prefix, MaxDepth oder Fehlerstatus.


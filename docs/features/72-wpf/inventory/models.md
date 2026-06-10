# Datenmodell

## `Projekt`
Datei: `src/Softwareschmiede/Domain/Entities/Projekt.cs`

| Eigenschaft | Typ | Beschreibung |
|---|---|---|
| `Id` | `Guid` | Eindeutige ID des Projekts |
| `Name` | `string` | Name des Projekts |
| `Beschreibung` | `string?` | Optionale Beschreibung |
| `ErstellungsDatum` | `DateTimeOffset` | Erstellt-Zeitstempel |
| `Status` | `ProjektStatus` | Status: `Aktiv` oder `Archiviert` |
| `Repositories` | `List<GitRepository>` | Zugeordnete Git-Repositories (1:n) |
| `Aufgaben` | `List<Aufgabe>` | Aufgaben des Projekts (1:n) |

## `Aufgabe`
Datei: `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`

| Eigenschaft | Typ | Beschreibung |
|---|---|---|
| `Id` | `Guid` | Eindeutige ID |
| `ProjektId` | `Guid` | Referenz zum Projekt (FK) |
| `GitRepositoryId` | `Guid?` | Optionale Referenz zu Git-Repository |
| `Titel` | `string` | Aufgabentitel |
| `AnforderungsBeschreibung` | `string?` | Anforderungsbeschreibung für KI |
| `Status` | `AufgabeStatus` | Aktueller Status der Aufgabe |
| `BranchName` | `string?` | Git-Branch-Name für diese Aufgabe |
| `LokalerKlonPfad` | `string?` | Lokaler Pfad des geklonten Repositories |
| `AgentenpaketName` | `string?` | Name des Agentenpakets |
| `AgentenName` | `string?` | Name des verwendeten Agenten |
| `KiPluginPrefix` | `string?` | Prefix des KI-Plugins |
| `ErstellungsDatum` | `DateTimeOffset` | Erstellung |
| `AbschlussDatum` | `DateTimeOffset?` | Abschluss (null wenn nicht beendet) |
| `AktiveRunId` | `string?` | ID einer laufenden KI-Ausführung |
| `LastHeartbeatUtc` | `DateTimeOffset?` | Zeitstempel des letzten Heartbeats |
| `RecoveryVersion` | `int` | Concurrency-Token für Recovery |
| `VorschlagPrompt` | `string?` | Persistierter Prompt-Vorschlag |
| `VorschlagAusfuehrenAbUtc` | `DateTimeOffset?` | Geplanter Ausführungszeitpunkt für Prompt |
| `Projekt` | `Projekt` | Navigationseigenschaft |
| `GitRepository` | `GitRepository?` | Navigationseigenschaft |
| `IssueReferenz` | `IssueReferenz?` | Verknüpfte Issue-Referenz |
| `Protokolleintraege` | `List<Protokolleintrag>` | Protokolleinträge dieser Aufgabe |
| `DiffResults` | `List<DiffResult>` | Diff-Ergebnisse |

## `GitRepository`
Datei: `src/Softwareschmiede/Domain/Entities/GitRepository.cs`

| Eigenschaft | Typ | Beschreibung |
|---|---|---|
| `Id` | `Guid` | Eindeutige ID |
| `ProjektId` | `Guid` | Referenz zum Projekt (FK) |
| `PluginTyp` | `string` | Plugin-Typ, z.B. "GitHub" |
| `RepositoryUrl` | `string` | URL des Repositories |
| `RepositoryName` | `string` | Name des Repositories |
| `Aktiv` | `bool` | Gibt an, ob Repository aktiv ist |
| `StartKonfiguration` | `RepositoryStartKonfiguration?` | Optionale Startkonfiguration |
| `Projekt` | `Projekt` | Navigationseigenschaft |
| `DiffResults` | `List<DiffResult>` | Diff-Ergebnisse |

## `Protokolleintrag`
Datei: `src/Softwareschmiede/Domain/Entities/Protokolleintrag.cs`

| Eigenschaft | Typ | Beschreibung |
|---|---|---|
| `Id` | `Guid` | Eindeutige ID |
| `AufgabeId` | `Guid` | Referenz zur Aufgabe (FK) |
| `Typ` | `ProtokollTyp` | Typ des Eintrags (Prompt, KiAntwort, StatusUebergang, TestErgebnis, GitAktion) |
| `Inhalt` | `string` | Inhalt des Protokolleintrags |
| `AgentName` | `string?` | Name des beteiligten Agenten |
| `Zeitstempel` | `DateTimeOffset` | Zeitstempel des Eintrags |
| `Aufgabe` | `Aufgabe` | Navigationseigenschaft |
| `TestErgebnisse` | `List<TestErgebnis>` | Testergebnisse (wenn Typ = TestErgebnis) |
| `DiffResult` | `DiffResult?` | Optionales zugehöriges Diff-Ergebnis |

## `TestErgebnis`
Datei: `src/Softwareschmiede/Domain/Entities/TestErgebnis.cs`

| Eigenschaft | Typ | Beschreibung |
|---|---|---|
| `Id` | `Guid` | Eindeutige ID |
| `ProtokollEintragId` | `Guid` | Referenz zum Protokolleintrag (FK) |
| `TestName` | `string` | Name des Tests |
| `Status` | `TestStatus` | Status: `Bestanden`, `Fehlgeschlagen`, `Uebersprungen` |
| `Fehlermeldung` | `string?` | Fehlermeldung bei fehlgeschlagenem Test |
| `Dauer` | `TimeSpan` | Dauer des Testlaufs |
| `Protokolleintrag` | `Protokolleintrag` | Navigationseigenschaft |

## `IssueReferenz`
Datei: `src/Softwareschmiede/Domain/Entities/IssueReferenz.cs`

| Eigenschaft | Typ | Beschreibung |
|---|---|---|
| `Id` | `Guid` | Eindeutige ID |
| `AufgabeId` | `Guid` | Referenz zur Aufgabe (FK) |
| `IssueNummer` | `int?` | Nummer des Issues im Provider |
| `Titel` | `string` | Titel des Issues |
| `Body` | `string?` | Beschreibungstext |
| `LabelsJson` | `string` | JSON-Array der Labels |
| `Milestone` | `string?` | Milestone des Issues |
| `IssueUrl` | `string?` | URL des Issues im Provider |
| `Aufgabe` | `Aufgabe` | Navigationseigenschaft |

## `AppEinstellung`
Datei: `src/Softwareschmiede/Domain/Entities/AppEinstellung.cs`

| Eigenschaft | Typ | Beschreibung |
|---|---|---|
| `Id` | `Guid` | Eindeutige ID |
| `Schluessel` | `string` | Maschinenlesbarer Schlüssel (z.B. "repositories.workdir") |
| `Wert` | `string?` | Optionaler Wert; null/leer bedeutet Default verwenden |
| `AktualisiertAm` | `DateTimeOffset` | Zeitpunkt der letzten Aktualisierung |

## `PluginKonfiguration`
Datei: `src/Softwareschmiede/Domain/Entities/PluginKonfiguration.cs`

| Eigenschaft | Typ | Beschreibung |
|---|---|---|
| `Id` | `Guid` | Eindeutige ID |
| `PluginTyp` | `string` | Typ des Plugins, z.B. "GitHub" oder "GitHubCopilot" |
| `PluginKategorie` | `PluginKategorie` | Kategorie: `Git` oder `Ki` |
| `AnzeigeName` | `string` | Anzeigename des Plugins |
| `CredentialStoreKey` | `string` | Schlüssel im Credential Store für API-Token |
| `BaseUrl` | `string?` | Optionale Basis-URL (z.B. für Self-Hosted) |
| `Aktiviert` | `bool` | Gibt an, ob Plugin aktiviert ist |

## `RepositoryStartKonfiguration`
Datei: `src/Softwareschmiede/Domain/Entities/RepositoryStartKonfiguration.cs`

| Eigenschaft | Typ | Beschreibung |
|---|---|---|
| `Id` | `Guid` | Eindeutige ID |
| `GitRepositoryId` | `Guid` | Referenz zum Repository (FK) |
| `StartScriptRelativePath` | `string` | Relativer Pfad zum Startskript im Repository |
| `Aktiv` | `bool` | Gibt an, ob die Konfiguration aktiv verwendet wird |
| `GitRepository` | `GitRepository` | Navigationseigenschaft |

## `DiffResult`
Datei: `src/Softwareschmiede/Domain/Entities/DiffResult.cs`

| Eigenschaft | Typ | Beschreibung |
|---|---|---|
| `Id` | `Guid` | Eindeutige ID |
| `AufgabeId` | `Guid` | Referenz zur Aufgabe (FK) |
| `GitRepositoryId` | `Guid?` | Optionale Referenz zu Git-Repository |
| `ProtokollEintragId` | `Guid?` | Optionale Referenz zu Protokolleintrag |
| `FilePath` | `string` | Relative Dateipfad im Repository |
| `SourceVersion` | `string` | Quellversion (Branch/Commit/Tag) |
| `TargetVersion` | `string` | Zielversion (Branch/Commit/Tag) |
| `DiffType` | `DiffType` | Rendering-Typ: `Full`, `SideBySide`, `Split` |
| `LineCount` | `int` | Gesamtzahl Zeilen im Diff |
| `AddedLines` | `int` | Anzahl hinzugefügter Zeilen |
| `RemovedLines` | `int` | Anzahl gelöschter Zeilen |
| `ModifiedLines` | `int` | Anzahl modifizierter Zeilen |
| `Status` | `DiffResultStatus` | Status: `Pending`, `Generated`, `Cached`, `Error` |
| `GeneratedAt` | `DateTimeOffset` | Zeitstempel der Generierung |
| `GeneratedBy` | `string` | Name des Services/Agenten, der Diff generiert hat |
| `SourceContent` | `string?` | Optionaler Vollinhalt der Quelldatei |
| `TargetContent` | `string?` | Optionaler Vollinhalt der Zieldatei |
| `ExpiresAt` | `DateTimeOffset?` | Ablaufzeit für Caching; null = keine Expiration |
| `Aufgabe` | `Aufgabe` | Navigationseigenschaft |
| `GitRepository` | `GitRepository?` | Navigationseigenschaft |
| `ProtokollEintrag` | `Protokolleintrag?` | Navigationseigenschaft |
| `DiffBlocks` | `List<DiffBlock>` | Diff-Blöcke |
| `DiffCache` | `DiffCache?` | Zugehöriger Cache-Eintrag |

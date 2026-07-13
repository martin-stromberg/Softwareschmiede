# Detailinventar - Tests und Testluecken

## Vorhandene Testabdeckung

### Bitbucket Remote-Struktur

`BitbucketPluginTests_GetRepositoryStructureAsync` deckt den Kernabruf ab:

- Cloud-Verzeichnisse bis `maxDepth` und ohne Dateien (`GetRepositoryStructureAsync_ShouldReturnDirectories_UpToMaxDepth`, Zeile 51)
- Cloud-Pagination ueber `next` (`GetRepositoryStructureAsync_ShouldFollowPagination_ViaNextLink`, Zeile 85)
- fehlgeschlagene Cloud-API liefert leere Liste (`GetRepositoryStructureAsync_ShouldReturnEmpty_WhenApiCallFails`, Zeile 124)
- API-Fehlerpayload liefert leere Liste (`GetRepositoryStructureAsync_ShouldReturnEmpty_WhenApiReturnsErrorPayload`, Zeile 142)
- unparsebare URL ruft die API nicht auf (`GetRepositoryStructureAsync_ShouldReturnEmpty_ForUnparsableUrl`, Zeile 161)
- Self-Hosted-Level-Walk (`GetRepositoryStructureAsync_ShouldWalkDirectoryLevels_WhenHostingModeIsSelfHosted`, Zeile 184)
- fehlgeschlagener Self-Hosted-Browse liefert leere Liste (`GetRepositoryStructureAsync_ShouldReturnEmpty_WhenSelfHostedBrowseFails`, Zeile 240)

### GitHub Referenz

`GitHubPluginTests_GetRepositoryStructureAsync` deckt API-Abruf, URL-Parsing, Basisklassen-Dispatch, Fehlerfaelle, `truncated` und malformed JSON ab.

### DirectoryStructureBrowserService

`DirectoryStructureBrowserServiceTests` deckt ab:

- Filterung auf Verzeichnisse
- Aufruf mit konfigurierter MaxDepth
- MemoryCache-TTL
- Fehler werden abgefangen und als leere Liste geliefert (`GetDirectoriesAsync_ShouldHandleErrors_Gracefully`, Zeile 80)

### ViewModels

`RepositoryAssignViewModelTests_WorkingDirectory` deckt:

- Laden bei Repository-Wechsel
- Cancellation bei schnellem Wechsel
- Loading-Flag
- Root-Eintrag `"."`
- Default-Auswahl `"."`
- Fehlerfallback auf Root-only (`LoadDirectoryStructureAsync_ShouldHandleErrors_WithLogging`, Zeile 207)

`ArbeitsverzeichnisBearbeitenViewModelTests` deckt:

- Root plus geladene Verzeichnisse
- Vorauswahl aktueller Werte
- Erhalt eines gespeicherten Werts, der nicht in der Struktur vorkommt
- Root-Fallback ohne Plugin
- Fehler werden ohne Exception behandelt (`LadenAsync_ShouldHandleErrors_Gracefully`, Zeile 111)
- Cancellation ohne Ueberschreiben des Zustands

## Testluecken fuer diese Anforderung

- Kein Test prueft, dass ein technischer Fehler einen manuellen Eingabemodus aktiviert.
- Kein Test prueft, dass bei erfolgreichem leerem Repository weiterhin die Auswahlbox mit `"."` angezeigt wird.
- Kein Test prueft eine TextBox-Bindung oder eine ViewModel-Property fuer manuelle Eingabe.
- Kein Test prueft, dass manuell eingegebene Werte beim Zuweisen eines neuen Repositories gespeichert werden.
- Kein Test prueft, dass ein manuell gespeicherter Wert bei erneutem Oeffnen im Fallback-Modus im Eingabefeld erscheint.
- Kein Test unterscheidet Plugin-Fehler, API-Fehlerpayload und normales leeres Ergebnis auf Service-Ebene.

## Empfohlene neue Tests

- `DirectoryStructureBrowserService` oder neuer Result-Service: Fehlerstatus wird getrennt von leerem Erfolg geliefert.
- `RepositoryAssignViewModel`: API-/Plugin-Fehler setzt manuellen Eingabemodus und uebernimmt Eingabetext beim Speichern.
- `RepositoryAssignViewModel`: erfolgreicher leerer Abruf bleibt Auswahlmodus mit `"."`.
- `ArbeitsverzeichnisBearbeitenViewModel`: bestehender manueller Wert wird im Fehlerfallback als Text angezeigt.
- XAML-/BUnit- oder ViewModel-nahe Tests: ComboBox sichtbar im Erfolgsfall, TextBox sichtbar im Fallbackfall.
- Regressionstest: GitHub-Verhalten bleibt Auswahlmodus bei erfolgreichem Abruf.


# Umsetzungsplan: Arbeitsverzeichnis fuer Bitbucket

## Uebersicht

Der vorhandene Remote-Abruf der Bitbucket-Verzeichnisstruktur bleibt erhalten und wird nicht neu aufgebaut. Umgesetzt wird eine explizite Lade-Ergebnissemantik zwischen Plugin, `DirectoryStructureBrowserService`, gemeinsamem Ladehelfer und WPF-ViewModels, damit erfolgreiche leere Repositories weiterhin die Auswahlbox mit `"."` zeigen, technische Fehler oder fehlende Strukturunterstuetzung aber in einen manuellen Eingabemodus wechseln. Betroffen sind Plugin-Contract/Plugin-Implementierungen, Application-Service, zwei ViewModels, zwei Dialog-XAMLs und die zugehoerigen Tests.

## Designentscheidungen

| Komponente / Bereich | Gewaehlter Ansatz | Begruendung |
|----------------------|------------------|-------------|
| Ergebnissemantik fuer Remote-Struktur | Neuer Result-Typ `RepositoryStructureLoadResult` plus `RepositoryStructureLoadStatus`; `IGitPlugin` erhaelt eine default-implementierte Result-Methode, bestehendes `GetRepositoryStructureAsync` bleibt kompatibel | Ein leerer Erfolg und ein technischer Fehler muessen unterscheidbar werden, ohne alle bestehenden Plugin-Aufrufer hart zu brechen. Die default-Methode kapselt vorhandene Implementierungen, Bitbucket/GitHub koennen gezielt ueberschreiben. |
| UI-Zustand | ViewModel-Properties `IsWorkingDirectoryManualInput` und `WorkingDirectoryInputText`; `SelectedWorkingDirectory` bleibt die Speicherquelle fuer bestaetigte Dialoge | Die Persistenz erwartet bereits einen einzelnen relativen Pfad. Der manuelle Modus ist damit UI-Zustand, kein neues Datenmodell. |
| Leeres Repository | Erfolgreicher Abruf mit leerer Directory-Liste bleibt Auswahlmodus mit nur `"."` | Das ist fachlich ein gueltiger Zustand und darf nicht wie ein technischer Fehler behandelt werden. |
| Nicht verfuegbare Struktur | Technischer Fehler, nicht unterstuetzter Abruf oder fehlendes Plugin fuehren zum manuellen Eingabemodus | Damit blockieren Projektanlage und Projektbearbeitung nicht und Benutzer koennen einen relativen Pfad selbst erfassen. |
| Manuelle Eingabevalidierung | Minimal syntaktische Validierung im ViewModel: leer wird als `"."` normalisiert; absolute Pfade und Traversal ueber `..` werden abgelehnt | Die Persistenz kann freie Werte speichern, der Runtime-Resolver validiert spaeter die Existenz. Absolute oder ausbrechende Pfade sollen aber schon im Dialog verhindert werden. |

## Programmablaeufe

### Erfolgreicher Abruf mit Auswahlbox

1. `RepositoryAssignViewModel` oder `ArbeitsverzeichnisBearbeitenViewModel` startet `DirectoryStructureLoadHelper.LoadWithLoadingStateAsync`.
2. Der Helper ruft `DirectoryStructureBrowserService.GetDirectoryLoadResultAsync` auf.
3. Der Service ruft `IGitPlugin.GetRepositoryStructureLoadResultAsync` mit Repository-URL, `MaxDepth` und `CancellationToken` auf.
4. Bei `RepositoryStructureLoadStatus.Success` filtert und sortiert der Service die Directory-Pfade, cached den Erfolg und gibt den Result zurueck.
5. Der Helper stellt `"."` voran und liefert einen erfolgreichen `WorkingDirectoryLoadResult`.
6. Das ViewModel setzt `IsWorkingDirectoryManualInput = false`, befuellt `AvailableWorkingDirectories` und setzt `SelectedWorkingDirectory` auf `"."` oder den vorhandenen Wert.
7. Die XAML zeigt die `ComboBox`; die `TextBox` bleibt verborgen.

Beteiligte Klassen/Komponenten: `IGitPlugin`, `RepositoryStructureLoadResult`, `DirectoryStructureBrowserService`, `DirectoryStructureLoadHelper`, `RepositoryAssignViewModel`, `ArbeitsverzeichnisBearbeitenViewModel`, `RepositoryAssignDialog.xaml`, `ArbeitsverzeichnisBearbeitenDialog.xaml`

### Technischer Fehler oder nicht unterstuetzter Abruf

1. Das Plugin meldet `RepositoryStructureLoadStatus.Failed` oder wirft eine Nicht-Cancellation-Exception.
2. `DirectoryStructureBrowserService.GetDirectoryLoadResultAsync` loggt den Fehler, cached keine Fehler und gibt einen Fehlerstatus zurueck.
3. `DirectoryStructureLoadHelper` gibt einen `WorkingDirectoryLoadResult` mit `RequiresManualInput = true` zurueck.
4. Das aufrufende ViewModel leert oder ignoriert die Auswahl-Liste, setzt `IsWorkingDirectoryManualInput = true` und initialisiert `WorkingDirectoryInputText`.
5. Bei Repository-Zuweisung ist der Initialwert `"."`; beim Bearbeiten wird `currentWorkingDirectory` uebernommen, andernfalls `"."`.
6. Die XAML zeigt statt der `ComboBox` eine `TextBox`, gebunden an `WorkingDirectoryInputText`.
7. Beim Bestaetigen synchronisiert das ViewModel den normalisierten Text nach `SelectedWorkingDirectory`; `ProjectDetailViewModel` speichert weiter ueber `SaveRepositoryWorkingDirectoryAsync`.

Beteiligte Klassen/Komponenten: `IGitPlugin`, `DirectoryStructureBrowserService`, `DirectoryStructureLoadHelper`, `RepositoryAssignViewModel`, `ArbeitsverzeichnisBearbeitenViewModel`, `ProjectDetailViewModel`, `ProjektService`

### Abbruch durch Repository- oder Plugin-Wechsel

1. Ein laufender Ladevorgang wird ueber das bestehende `CancellationTokenSource` abgebrochen.
2. `OperationCanceledException` wird weiterhin nicht als Fehlerstatus gewertet.
3. `DirectoryStructureLoadHelper.LoadWithLoadingStateAsync` gibt wie bisher `null` zurueck.
4. Das aufrufende ViewModel laesst den bisherigen Zustand unveraendert beziehungsweise setzt beim Bearbeiten den Loading-Status zurueck.

Beteiligte Klassen/Komponenten: `RepositoryAssignViewModel`, `ArbeitsverzeichnisBearbeitenViewModel`, `DirectoryStructureLoadHelper`

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `RepositoryStructureLoadResult` | Datenmodellklasse / Value Object | Traegt Status, Directory-Eintraege und optionale Fehlermeldung fuer den Plugin-nahen Remote-Strukturabruf. |
| `RepositoryStructureLoadStatus` | Enum | Unterscheidet mindestens `Success`, `Failed` und `NotSupported`. |
| `WorkingDirectoryLoadResult` | Datenmodellklasse / Value Object | App-nahe Helper-Rueckgabe mit Root-ergaenzter Liste, `RequiresManualInput` und optionaler Meldung fuer die ViewModels. |

## Aenderungen an bestehenden Klassen

### `IGitPlugin` (Interface)

- **Neue Methoden:** `GetRepositoryStructureLoadResultAsync(string repositoryUrl, int maxDepth = 2, CancellationToken ct = default)` - default-implementierte Result-Methode; ruft standardmaessig `GetRepositoryStructureAsync` auf und wandelt Erfolg, `NotSupportedException` und sonstige Fehler in `RepositoryStructureLoadResult` um.
- **Geaenderte Methoden:** `GetRepositoryStructureAsync` bleibt als bestehender Kompatibilitaetsvertrag erhalten.

### `GitPluginBase<TPlugin>` (abstrakte Basisklasse)

- **Neue Methoden:** optionaler Override oder Weiterleitung fuer `GetRepositoryStructureLoadResultAsync`, konsistent zur Interface-default-Implementierung.

### `BitbucketPlugin` (Plugin)

- **Neue Methoden:** `GetRepositoryStructureLoadResultAsync` - fuehrt den bestehenden Cloud-/Self-Hosted-Abruf aus und liefert `Success` nur bei wirklich erfolgreichem Abruf; API-Fehler, Fehlerpayloads, unparsebare URLs und Root-Browse-Fehler liefern `Failed`.
- **Geaenderte Methoden:** `GetRepositoryStructureAsync` bleibt kompatibel und gibt fuer bestehende direkte Aufrufer weiterhin nur die Eintraege des Result-Typs zurueck.
- **Geaenderte interne Methoden:** Cloud- und Self-Hosted-Hilfsmethoden sollen Fehlerstatus an den Result-Wrapper durchreichen, ohne den eigentlichen Traversal-Algorithmus neu zu bauen.

### `GitHubPlugin` (Plugin)

- **Neue Methoden:** `GetRepositoryStructureLoadResultAsync` - bildet den bestehenden GitHub-Abruf auf den neuen Result-Typ ab.
- **Geaenderte Methoden:** `GetRepositoryStructureAsync` bleibt kompatibel; bestehende direkte Tests koennen weiter die leere Liste im Fehlerfall erwarten, waehrend neue Service/UI-Tests den Result-Status pruefen.

### `LocalDirectoryPlugin` (Plugin)

- **Neue Methoden:** optionaler `GetRepositoryStructureLoadResultAsync`-Override, damit nicht existierende lokale Pfade als `Failed` statt leerer Erfolg gemeldet werden koennen.
- **Geaenderte Methoden:** `GetRepositoryStructureAsync` bleibt kompatibel.

### `DirectoryStructureBrowserService` (Service)

- **Neue Methoden:** `GetDirectoryLoadResultAsync(IGitPlugin gitPlugin, string repositoryUrl, CancellationToken ct = default)` - liefert `WorkingDirectoryLoadResult` oder service-nahen Result mit Status, sortierten Verzeichnissen und Fehlerstatus; cached nur erfolgreiche Abrufe.
- **Geaenderte Methoden:** `GetDirectoriesAsync` bleibt als Kompatibilitaetsmethode erhalten und gibt aus einem erfolgreichen Result die Directory-Liste zurueck; bei Fehlern weiterhin `[]`.
- **Geaenderte Logik:** Cache-Key um mindestens `gitPlugin.PluginPrefix`, Repository-URL und `MaxDepth` erweitern, damit verschiedene Plugins/Depths nicht kollidieren.

### `DirectoryStructureLoadHelper` (Helper)

- **Geaenderte Methoden:** `LoadWorkingDirectoriesAsync` und `LoadWithLoadingStateAsync` geben kuenftig `WorkingDirectoryLoadResult` beziehungsweise `WorkingDirectoryLoadResult?` zurueck; `OperationCanceledException` bleibt der einzige erwartete Null-Fall.
- **Neue Logik:** Root-Eintrag `"."` nur in erfolgreichen Auswahl-Ergebnissen voranstellen; Fehlerstatus als manuellen Eingabemodus weiterreichen.

### `RepositoryAssignViewModel` (ViewModel)

- **Neue Eigenschaften:** `IsWorkingDirectoryManualInput` (`bool`) - steuert ComboBox/TextBox; `WorkingDirectoryInputText` (`string?`) - TextBox-Wert im manuellen Modus; optional `WorkingDirectoryInputError` (`string?`) - Validierungsfeedback.
- **Geaenderte Methoden:** `OnSelectedRepositoryChanged` setzt Auswahl und manuellen Text zurueck; `LoadDirectoryStructureAsync` wertet den neuen Helper-Result aus; `BestaetigenCommand` validiert und synchronisiert im manuellen Modus `WorkingDirectoryInputText` nach `SelectedWorkingDirectory`.
- **Geaenderte Command-Logik:** Im manuellen Modus darf bestaetigt werden, wenn ein Repository gewaehlt ist und der manuelle Pfad syntaktisch gueltig ist.

### `ArbeitsverzeichnisBearbeitenViewModel` (ViewModel)

- **Neue Eigenschaften:** `IsWorkingDirectoryManualInput` (`bool`), `WorkingDirectoryInputText` (`string?`), optional `WorkingDirectoryInputError` (`string?`).
- **Geaenderte Methoden:** `LoadDirectoryStructureAsync` setzt bei Fehler/fehlendem Plugin den manuellen Modus und uebernimmt `currentWorkingDirectory`; bei Erfolg bleibt die bestehende Logik zum Hinzufuegen eines gespeicherten Werts erhalten.
- **Geaenderte Command-Logik:** `BestaetigenCommand` validiert und synchronisiert im manuellen Modus den Text nach `SelectedWorkingDirectory`.

### `RepositoryAssignDialog.xaml` (View)

- **Geaenderte Bindings:** Die vorhandene `ComboBox` erhaelt `Visibility` auf `InverseBoolToVisibilityConverter` mit `IsWorkingDirectoryManualInput`.
- **Neue Controls:** Eine `TextBox` fuer `WorkingDirectoryInputText`, sichtbar bei `IsWorkingDirectoryManualInput`; optional ein `TextBlock` fuer `WorkingDirectoryInputError`.

### `ArbeitsverzeichnisBearbeitenDialog.xaml` (View)

- **Geaenderte Bindings:** Analog zur Repository-Zuweisung zwischen `ComboBox` und `TextBox` umschalten.
- **Neue Controls:** `TextBox` fuer manuelle Eingabe und optionales Fehlerfeedback.

### `ProjectDetailViewModel` (ViewModel)

- **Geaenderte Methoden:** Keine fachliche Speicherlogik aendern; nur sicherstellen, dass beim bestaetigten Dialog weiterhin `vm.SelectedWorkingDirectory` gelesen wird, nachdem der manuelle Text synchronisiert wurde.

## Datenbankmigrationen

Keine.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `WorkingDirectoryInputText` | `null`, leer oder Whitespace wird als `"."` normalisiert | Kein Fehler; Speicherung bleibt Root. |
| `WorkingDirectoryInputText` | Pfad muss relativ sein (`Path.IsPathRooted` false) | Bestaetigen bleibt deaktiviert oder zeigt `WorkingDirectoryInputError`. |
| `WorkingDirectoryInputText` | Pfad darf keine Traversal-Segmente `..` enthalten | Bestaetigen bleibt deaktiviert oder zeigt `WorkingDirectoryInputError`. |
| `WorkingDirectoryInputText` | Backslashes werden vor Speicherung optional zu `/` normalisiert | Ungueltige Pfadzeichen fuehren zu `WorkingDirectoryInputError`. |

## Konfigurationsaenderungen

Keine.

## Seiteneffekte und Risiken

- **Plugin-Vertrag:** Ein neuer default-implementierter Interface-Member ist quellenkompatibel fuer vorhandene Implementierungen, kann aber Tests/Fakes betreffen, die das Interface streng mocken. Moq-basierte Tests muessen fuer neue Result-Pfade Setups ergaenzen.
- **Bitbucket/GitHub-Fehlerverhalten:** Direkte `GetRepositoryStructureAsync`-Tests sollen kompatibel bleiben. Neue Result-Tests pruefen zusaetzlich Fehlerstatus; dabei darf das bestehende UI-Verhalten fuer GitHub-Erfolg nicht veraendert werden.
- **Caching:** Wenn Fehler nicht gecached werden, kann ein erneuter Dialogaufruf direkt wieder die API versuchen. Das ist erwuenscht, kann aber bei dauerhaftem Fehler mehr API-Aufrufe erzeugen als bisher.
- **UI-Layout:** Beide Dialoge sind derzeit knapp dimensioniert. Die TextBox und Fehlerzeile muessen ohne Ueberlappung in `RepositoryAssignDialog.xaml` und `ArbeitsverzeichnisBearbeitenDialog.xaml` passen; bei Bedarf Hoehe moderat erhoehen.
- **Manuelle Werte:** Bestehende gespeicherte Werte duerfen beim Bearbeiten nicht verloren gehen; besonders der Fallback-Modus muss `currentWorkingDirectory` in `WorkingDirectoryInputText` anzeigen.

## Umsetzungsreihenfolge

1. **Result-Typen fuer Repository-Struktur anlegen**
   - Voraussetzungen: Keine.
   - Beschreibung: `RepositoryStructureLoadResult` und `RepositoryStructureLoadStatus` im Plugin-Contracts-Projekt anlegen; Factory-Methoden fuer Erfolg, Fehler und Nicht-Unterstuetzung vorsehen.

2. **`IGitPlugin` und `GitPluginBase<TPlugin>` erweitern**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Default-Result-Methode einfuehren, bestehende `GetRepositoryStructureAsync`-Signatur erhalten und Cancellation unveraendert propagieren.

3. **`DirectoryStructureBrowserService` result-faehig machen**
   - Voraussetzungen: Schritte 1-2.
   - Beschreibung: Neue Result-Methode einfuehren, erfolgreiche Ergebnisse filtern/sortieren/cachen, Fehler loggen und nicht cachen; `GetDirectoriesAsync` als Wrapper erhalten.

4. **`WorkingDirectoryLoadResult` und Helper anpassen**
   - Voraussetzungen: Schritt 3.
   - Beschreibung: App-nahen Helper-Result einfuehren; Root-Eintrag im Erfolgsfall ergaenzen; Fehlerstatus als manuellen Modus weiterreichen; Cancellation-Null-Verhalten beibehalten.

5. **Bitbucket-Result-Override implementieren**
   - Voraussetzungen: Schritte 1-2.
   - Beschreibung: Bestehende Cloud- und Self-Hosted-Abrufe so kapseln, dass technische Fehler, API-Fehlerpayloads und unparsebare URLs `Failed` liefern, erfolgreiche leere Repositories aber `Success` mit leerer Liste.

6. **GitHub- und LocalDirectory-Result-Verhalten ergaenzen**
   - Voraussetzungen: Schritte 1-2.
   - Beschreibung: GitHub auf neuen Result-Typ abbilden; LocalDirectory bei fehlendem Pfad als Fehlerstatus behandeln, leeres existierendes Verzeichnis als Erfolg.

7. **`RepositoryAssignViewModel` auf Auswahl-/Manuell-Modus erweitern**
   - Voraussetzungen: Schritt 4.
   - Beschreibung: Neue Properties, Validierung und Synchronisation im Bestaetigen-Command ergaenzen; erfolgreichen Auswahlmodus und Fehlerfallback getrennt behandeln.

8. **`ArbeitsverzeichnisBearbeitenViewModel` auf Auswahl-/Manuell-Modus erweitern**
   - Voraussetzungen: Schritt 4.
   - Beschreibung: Neue Properties, Validierung und Synchronisation ergaenzen; `currentWorkingDirectory` im Fallback in die TextBox uebernehmen.

9. **Dialog-XAMLs um TextBox-Fallback erweitern**
   - Voraussetzungen: Schritte 7-8.
   - Beschreibung: `ComboBox` und `TextBox` per Visibility gegeneinander schalten; Loading- und Hinweistext ohne Ueberlappung beibehalten.

10. **Tests anpassen und ergaenzen**
   - Voraussetzungen: Schritte 1-9.
   - Beschreibung: Service-, Plugin-, Helper-, ViewModel- und E2E-nahe Tests fuer Erfolgs-, Fehler- und manuellen Eingabemodus ergaenzen; bestehende Root-only-Fehlererwartungen auf manuellen Modus aktualisieren.

11. **Build und Testlauf ausfuehren**
   - Voraussetzungen: Schritte 1-10.
   - Beschreibung: `dotnet build` und relevante `dotnet test`-Suites ausfuehren; bei WPF/E2E nur ausfuehren, wenn die Umgebung GUI-faehig ist.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprueft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `GetDirectoryLoadResultAsync_ShouldReturnSuccess_ForEmptyRepository` | `DirectoryStructureBrowserServiceTests` | Leerer erfolgreicher Plugin-Result bleibt Erfolg und kein manueller Fehlerfallback. |
| `GetDirectoryLoadResultAsync_ShouldReturnFailed_WhenPluginThrows` | `DirectoryStructureBrowserServiceTests` | Nicht-Cancellation-Exception wird als Fehlerstatus gemeldet und nicht gecached. |
| `GetDirectoryLoadResultAsync_ShouldUsePluginPrefixAndMaxDepthInCacheKey` | `DirectoryStructureBrowserServiceTests` | Cache-Kollisionen zwischen Plugins/Depths werden verhindert. |
| `GetRepositoryStructureLoadResultAsync_ShouldReturnFailed_WhenCloudApiFails` | `BitbucketPluginTests_GetRepositoryStructureAsync` | Bitbucket-Cloud-API-Fehler liefert Fehlerstatus. |
| `GetRepositoryStructureLoadResultAsync_ShouldReturnSuccess_WhenCloudRepositoryHasNoDirectories` | `BitbucketPluginTests_GetRepositoryStructureAsync` | Leerer erfolgreicher Cloud-Abruf bleibt Erfolg. |
| `GetRepositoryStructureLoadResultAsync_ShouldReturnFailed_WhenSelfHostedRootBrowseFails` | `BitbucketPluginTests_GetRepositoryStructureAsync` | Self-Hosted-Root-Fehler wird als Fehlerstatus gemeldet. |
| `LoadDirectoryStructureAsync_ShouldEnableManualInput_WhenDirectoryLoadFails` | `RepositoryAssignViewModelTests_WorkingDirectory` | Fehlerfallback setzt `IsWorkingDirectoryManualInput` und initialisiert `WorkingDirectoryInputText`. |
| `LoadDirectoryStructureAsync_ShouldKeepSelectionMode_WhenRepositoryIsEmpty` | `RepositoryAssignViewModelTests_WorkingDirectory` | Erfolgreich leere Struktur zeigt Auswahlmodus mit `"."`. |
| `BestaetigenCommand_ShouldUseManualWorkingDirectoryInput` | `RepositoryAssignViewModelTests_WorkingDirectory` | Manueller Text wird nach `SelectedWorkingDirectory` synchronisiert. |
| `LadenAsync_ShouldShowCurrentWorkingDirectoryInManualInput_WhenLoadFails` | `ArbeitsverzeichnisBearbeitenViewModelTests` | Vorhandener gespeicherter Wert erscheint im Fallback-Textfeld. |
| `BestaetigenCommand_ShouldRejectAbsoluteManualWorkingDirectory` | `ArbeitsverzeichnisBearbeitenViewModelTests` | Absolute Pfade werden nicht akzeptiert. |
| `BestaetigenCommand_ShouldRejectTraversalManualWorkingDirectory` | `ArbeitsverzeichnisBearbeitenViewModelTests` | `..`-Traversal wird nicht akzeptiert. |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `DirectoryStructureBrowserServiceTests.GetDirectoriesAsync_ShouldHandleErrors_Gracefully` | Kompatibilitaetsmethode bleibt leer, aber neuer Result-Test prueft den Fehlerstatus. |
| `RepositoryAssignViewModelTests_WorkingDirectory.LoadDirectoryStructureAsync_ShouldHandleErrors_WithLogging` | Erwartung wechselt von Root-only-ComboBox zu manuellem Eingabemodus mit `"."`. |
| `ArbeitsverzeichnisBearbeitenViewModelTests.LadenAsync_ShouldHandleErrors_Gracefully` | Erwartung wechselt von Root-only-Liste zu manuellem Eingabemodus mit aktuellem Wert. |
| `BitbucketPluginTests_GetRepositoryStructureAsync` | Bestehende direkte Empty-Tests bleiben fuer `GetRepositoryStructureAsync`; neue Result-Tests ergaenzen Fehlerstatus. |
| `GitHubPluginTests_GetRepositoryStructureAsync` | Bestehende direkte Empty-Tests bleiben; neue Result-Tests koennen Erfolg/Fehlerstatus abdecken. |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Repository-Zuweisung mit erfolgreichem Bitbucket-Strukturabruf zeigt Arbeitsverzeichnis-Auswahl mit Unterverzeichnissen | `E2E_WorkingDirectory` oder neue `E2E_BitbucketWorkingDirectory` | 1, 2, 3 |
| Repository-Zuweisung bei fehlgeschlagenem Strukturabruf zeigt TextBox und speichert manuellen relativen Pfad | `E2E_WorkingDirectory` oder neue `E2E_BitbucketWorkingDirectory` | 4, 5, 7 |
| Arbeitsverzeichnis bearbeiten bei fehlgeschlagenem Strukturabruf zeigt gespeicherten manuellen Wert im Textfeld | `E2E_WorkingDirectory` oder neue `E2E_BitbucketWorkingDirectory` | 6, 7 |

Betroffene bestehende E2E-Tests:

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `E2E_WorkingDirectory` | Falls der Test Root-only-Fehlerfallback ueber ComboBox erwartet, muss er auf den neuen manuellen Modus angepasst werden. |

## Offene Punkte

Keine.

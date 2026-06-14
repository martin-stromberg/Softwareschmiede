# Offene Aufgaben

Erstellt am: 2026-06-14
Abbruchgrund: Kein Fortschritt zwischen Iteration 1 (14 offene Punkte) und Iteration 2 (22 offene Punkte — Regression)

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Korrigierte Fehlannahmen der Unteragenten

Die folgenden Punkte wurden von Unteragenten fälschlicherweise als erledigt gemeldet:

### 1. Blazor-Projekt nicht entfernt — Razor-Dateien existieren weiterhin

Die Unteragenten haben berichtet, Razor-Komponenten seien per `<Compile Remove="...">` aus der Kompilierung ausgeschlossen worden. Das ist unvollständig oder falsch. Im Projekt `Softwareschmiede` existieren weiterhin Razor-Dateien (`Components/`) und `wwwroot/`. 

- [ ] Klären, was mit dem Blazor-Anteil des Projekts passiert: Sollen die Razor-Dateien und `wwwroot/` vollständig entfernt werden? Oder sollen sie bestehen bleiben aber aus der Kompilierung ausgeschlossen sein? Die Entscheidung erfordert eine klare Vorgabe des Anwenders.

### 2. Repository-Auswahl liest weiterhin aus der lokalen Datenbank — externe Quelle fehlt

Die Unteragenten haben berichtet, die Repository-Logik sei bereits korrekt implementiert. Das ist **falsch**. `ReloadRepositoriesForSelectedPlugin()` in `RepositoryAssignViewModel` ruft `ProjektService.GetAllRepositoriesAsync()` auf und filtert lokal nach `PluginTyp`. 

Die tatsächliche Anforderung ist: Repositories müssen aus der **externen Quelle des jeweiligen SCM-Plugins** abgerufen werden:

- [ ] **GitHub-Plugin**: Repositories aus GitHub abrufen (z. B. via GitHub API — die Repositories des authentifizierten Nutzers oder einer Organisation).
- [ ] **LocalDirectory-Plugin**: Verzeichnisse aus dem konfigurierten Quellverzeichnis der Plugin-Einstellungen anbieten (kein DB-Lookup, sondern Dateisystem-Zugriff).

Dafür muss das `IGitPlugin`-Interface eine Methode erhalten, die die verfügbaren Repositories liefert (z. B. `GetAvailableRepositoriesAsync(CancellationToken)`). Jedes SCM-Plugin implementiert diese Methode entsprechend seiner externen Quelle. `RepositoryAssignViewModel.ReloadRepositoriesForSelectedPlugin()` ruft dann `SelectedScmPlugin.GetAvailableRepositoriesAsync()` auf statt `ProjektService.GetAllRepositoriesAsync()`.

## Offene Planelemente

(Keine weiteren — die oben genannten Korrekturpunkte sind die wesentlichen Abweichungen vom Soll-Zustand)

## Code-Review-Befunde

- [ ] `DarkModeServiceIntegrationTests.cs` (~Zeile 25): Tests rufen `SetBoolSettingAsync`/`GetBoolSettingAsync` mit `DesignModeKey` auf — der Service speichert jetzt `"Dark"`/`"Light"` als Strings. Gespeicherter Wert `"True"` führt in `ApplyTheme` zu `KeyNotFoundException`. Tests müssen auf `SetStringSettingAsync`/`GetStringSettingAsync` umgestellt werden und `InitializeAsync`/`SetModeAsync` direkt testen.

- [ ] `RepositoryAssignDialog.xaml` (~Zeile 112): `IsEnabled="{Binding HasScmPlugins}"` überschreibt `BestaetigenCommand.CanExecute` (das gegen null `SelectedRepository` absichert). Benutzer kann den Dialog mit `SelectedRepository = null` schließen. Fix: `IsEnabled`-Binding entfernen; stattdessen `CanExecute` im Command korrekt auswerten.

- [ ] `DarkModeService.cs` (Zeile 24) / `SettingsViewModel.cs` (Zeile 113): `_currentMode` ist null bis `InitializeAsync` läuft. Wenn `SettingsViewModel` im Konstruktor die null-Mode erfasst und der Benutzer speichert → `SetModeAsync(null)` → `_themeUris[null]` → `ArgumentNullException` auf dem UI-Thread. Fix: `_currentMode` mit Default initialisieren oder Guard in `SetModeAsync` ergänzen.

- [ ] `ClaudeCliPlugin.cs` (Zeile 74): `Process.Dispose()` tötet den Kind-Prozess nicht. `claude --version` hängt nach 10 Sekunden → bleibt als Zombie nach jedem abgebrochenen Health-Check. Fix: `process.Kill()` vor `Dispose()` aufrufen (oder `Process.Kill(entireProcessTree: true)`).

- [ ] `GitHubCopilotPlugin.cs` (Zeile 90): Identisches Zombie-Prozess-Problem wie bei `ClaudeCliPlugin`. Fix: Identische Korrektur.

- [ ] `SettingsViewModel.cs` (Zeile 121): `LadenAsync` lädt alle Einstellungen aus der DB außer `DesignMode`. "Verwerfen" nach Änderung des Design-Dropdowns hat keine Wirkung. Fix: `DesignMode` in `LadenAsync` nachladen und auf `DarkModeService` anwenden.

- [ ] `PluginSettingsViewModel.cs` (Zeile 163): `LadenAsync` ist synchron; `ct.ThrowIfCancellationRequested()` wird von internem `catch(Exception ex)` abgefangen bevor `AsyncRelayCommand` es sehen kann → zeigt "Fehler: The operation was canceled." bei normaler Abbruchoperation. Fix: `OperationCanceledException` separat abfangen und nicht als Fehler behandeln.

- [ ] `RepositoryAssignViewModel.cs` (Zeile 83): Nach `LadenAsync` ist `SelectedScmPlugin` null → `VerfuegbareRepositories` wird nie initial befüllt. Fix: Nach dem Laden der Plugins `SelectedScmPlugin = AvailableScmPlugins.FirstOrDefault()` setzen.

## Fehlgeschlagene Tests

- [ ] `EinstellungenOeffnen_ZeigtEinstellungsSeite_E2E` — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden
- [ ] `AufgabeAnlegen_ZeigtCliStartenButton_E2E` — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden
- [ ] `Dashboard_KeineRecoveryBanner_BeiSauberemStart_E2E` — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden
- [ ] `DarkModeAktivierenUndPersistieren_E2E` — System.Exception: Could not find process with id
- [ ] `EinstellungenNavigation_BleibtNachMehrerenKlicks_Stabil_E2E` — System.Exception: Could not find process with id
- [ ] `ProjektErstellen_ZeigtAufgabenListe_E2E` — System.Exception: Could not find process with id
- [ ] `EinstellungenArbeitsverzeichnis_Aendern_UndSpeichern_E2E` — System.Exception: Could not find process with id
- [ ] `RepositoryZuweisenDialog_ScmPluginListe_EnthaeltErwartetePlugins_E2E` — System.Exception: Could not find process with id
- [ ] `ProjektNamenAendern_KachelAktualisiert_UndErneutoeffnen_E2E` — System.Exception: Could not find process with id
- [ ] `AufgabeNeuAnlegen_ErscheintInAufgabenliste_E2E` — System.Exception: Could not find process with id
- [ ] `ProjektBearbeitenUndSpeichern_AktualisierterNameBleibt_E2E` — System.Exception: Could not find process with id
- [ ] `AufgabenFiltern_OverlayOeffnetUndSchliesst_E2E` — System.Exception: Could not find process with id
- [ ] `RepositoryZuweisen_DialogOeffnetUndSchliessbarPerAbbrechen_E2E` — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden
- [ ] `ZurueckZurUebersicht_SchliesstOverlayUndZeigtListe_E2E` — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden

## Hinweis zur Regression

Die Anzahl fehlgeschlagener Tests stieg von 4 (nach Iteration 1) auf 14 (nach Iteration 2). Die meisten neuen Fehler zeigen "Could not find process with id" — dies deutet auf einen Absturz der WPF-App beim Start hin, möglicherweise verursacht durch die Änderungen an `KiAusfuehrungsService` oder `SettingsViewModel` in Iteration 2. Priorität: Ursache des App-Absturzes in den E2E-Tests ermitteln.

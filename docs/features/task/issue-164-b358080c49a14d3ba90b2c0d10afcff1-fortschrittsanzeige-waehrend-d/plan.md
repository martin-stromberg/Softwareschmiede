# Umsetzungsplan: Fortschrittsanzeige waehrend des Klonens

## Uebersicht

Beim Starten einer Aufgabe laeuft die Repository-Vorbereitung aktuell vor dem CLI-Start, ohne dass die Fusszeile einen laufenden Zustand anzeigt. Dadurch bleibt `CliStatusText` waehrend laengerer Klon- oder Vorbereitungsablaeufe bei `CLI inaktiv`.

Umgesetzt wird der Mindeststatus aus der Anforderung in der bestehenden Fusszeile der Aufgabendetailansicht: Sobald der Anwender `StartenCommand` ausfuehrt und der kombinierte Startablauf nach Plugin-Auswahl beginnt, setzt `TaskDetailViewModel.StartenAsync()` den mittleren Fusszeilentext auf exakt `Bereit Repository vor...`. Nach erfolgreichem Start uebernimmt die bestehende CLI-Statuslogik wieder den Text. In Fehler- und Abbruchfaellen wird der Vorbereitungsstatus nicht dauerhaft stehen gelassen.

Echte Prozent- oder Detailfortschritte werden in dieser Umsetzung nicht eingefuehrt, weil `IGitPlugin.CloneRepositoryAsync(...)` aktuell keinen Fortschrittskanal anbietet und die Remote-Plugins `git clone` ueber `ICliRunner.RunAsync(...)` erst nach Prozessende auswerten. Es wird kein ungenauer Prozentwert simuliert.

## Designentscheidungen

| Bereich | Gewaehlter Ansatz | Begruendung |
|---|---|---|
| Sichtbarer Status | Bestehende Property `CliStatusText` fuer den Vorbereitungsstatus wiederverwenden | Der gebundene TextBlock in `TaskDetailView.xaml` ist bereits der sichtbare mittlere Fusszeilenstatus. Eine neue globale Statusinfrastruktur waere fuer die aktuelle Anforderung groesser als noetig. |
| Status-String | Konstante im `TaskDetailViewModel`, Wert exakt `Bereit Repository vor...` | Der Text ist Akzeptanzkriterium und soll nicht durch Tippfehler oder spaetere Mehrfachdefinitionen auseinanderlaufen. |
| Zeitpunkt Start | Nach erfolgreicher Plugin-Aufloesung und direkt vor `ProzessStartenUndCliStartenAsync(...)` | Vorher kann noch der Plugin-Auswahldialog offen sein; die Repository-Vorbereitung laeuft erst im kombinierten Prozess. |
| Zeitpunkt Ende bei Erfolg | Nach Rueckkehr aus `ProzessStartenUndCliStartenAsync(...)` durch bestehendes `LadenAsync(...)` und `AttachCliStatusSession(...)` | Nach erfolgreicher Vorbereitung startet die CLI. Danach soll wieder der fachlich passendere CLI-Status angezeigt werden. |
| Zeitpunkt Ende bei Fehler | Im `catch` den Status auf inaktiv zuruecksetzen, bevor bzw. waehrend `FehlerMeldung` gesetzt wird | Fehler duerfen nicht von einem dauerhaft falschen Vorbereitungsstatus ueberdeckt werden. |
| Fortschrittswerte | Keine Prozentanzeige in dieser Iteration | Der Contract und die verwendeten Runner liefern keine belastbaren laufenden Fortschrittswerte. Die Anforderung erlaubt den Mindeststatus, wenn Fortschritt nicht verfuegbar ist. |

## Programmablauf

### Aufgabenstart mit Repository-Vorbereitung

1. Der Anwender startet eine Aufgabe ueber `TaskDetailViewModel.StartenCommand`.
2. `StartenAsync(CancellationToken ct)` prueft wie bisher `_aufgabeId` und `_aufgabe`.
3. `FehlerMeldung` wird zurueckgesetzt.
4. Das Development-Automation-Plugin wird wie bisher ueber `PluginSelectionService` oder Dialog aufgeloest.
5. Wenn kein Plugin gewaehlt wird, endet der Ablauf ohne Statusaenderung.
6. Direkt vor dem Await auf `EntwicklungsprozessService.ProzessStartenUndCliStartenAsync(...)` wird `CliStatusText` auf `Bereit Repository vor...` gesetzt.
7. Waehrend `ProzessStartenUndCliStartenAsync(...)` Clone, Working-Directory-Pruefung, Branch-Erstellung, `issue.md`-Schreiben, Persistenz und CLI-Start ausfuehrt, bleibt dieser Text sichtbar.
8. Nach erfolgreichem Ruecksprung laedt `LadenAsync(ct)` den aktuellen Zustand neu.
9. `SetAktiverCliName(pluginPrefix)` und `AttachCliStatusSession(session)` stellen den bestehenden CLI-Status wieder her.
10. Bei Fehler wird `AktiverCliName` wie bisher geleert, `FehlerMeldung` gesetzt und der Fusszeilentext wieder auf `CLI inaktiv` gesetzt.
11. Bei `OperationCanceledException` wird der Vorbereitungsstatus ebenfalls vor dem erneuten Werfen zurueckgesetzt.

## Neue Klassen

Keine.

## Aenderungen an bestehenden Klassen

### `TaskDetailViewModel`

- Neue private Konstante, z. B. `RepositoryPreparationStatusText = "Bereit Repository vor..."`.
- In `StartenAsync(CancellationToken ct)` wird direkt vor `ProzessStartenUndCliStartenAsync(...)` `CliStatusText = RepositoryPreparationStatusText;` gesetzt.
- Im Fehlerpfad wird der Status ueber die vorhandene Logik wieder auf inaktiv gesetzt, z. B. `AttachCliStatusSession(null)` oder `UpdateCliStatusText(CliRuntimeStatus.Inaktiv)`.
- Im `OperationCanceledException`-Pfad wird der Status ebenfalls zurueckgesetzt, bevor die Exception erneut geworfen wird.
- Erfolgspfad bleibt an die bestehende Session-Statuslogik angebunden; dort soll der neue Status nicht manuell ueberschrieben werden, wenn eine PseudoConsole-Session vorhanden ist.

### `TaskDetailView.xaml`

Keine funktionale Aenderung erforderlich. Der vorhandene TextBlock bindet bereits an `CliStatusText`.

### `EntwicklungsprozessService`

Keine Aenderung erforderlich. Der Service bleibt fuer Clone, Rollback und CLI-Start verantwortlich; die sichtbare UI-Statusanzeige wird im ViewModel gesetzt, das die Fusszeile besitzt.

### `IGitPlugin`, GitHub-, Bitbucket- und LocalDirectory-Plugins

Keine Aenderung erforderlich. Der bestehende Clone-Contract bleibt kompatibel.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsaenderungen

Keine.

## Seiteneffekte und Risiken

- `CliStatusText` wird fachlich breiter genutzt als der Name vermuten laesst. Das ist fuer diese kleine UI-Aenderung akzeptabel, sollte aber bei einer spaeteren generischen Statusleiste in `FooterStatusText` oder einen Statusdienst ueberfuehrt werden.
- Der Status darf nicht vor der Plugin-Auswahl gesetzt werden, weil sonst ein abgebrochener Dialog kurz den Repository-Status anzeigen koennte, obwohl noch keine Vorbereitung laeuft.
- Bei Fehlern darf der Vorbereitungsstatus nicht stehen bleiben. Das muss explizit getestet werden.
- Falls `LadenAsync(ct)` oder `AttachCliStatusSession(...)` nach erfolgreichem Start keine Session findet, muss der Status dennoch nicht dauerhaft auf `Bereit Repository vor...` bleiben. Die bestehende `AttachCliStatusSession(null)`-Logik setzt auf `CLI inaktiv`.
- Prozentfortschritt fuer echte Git-Klons bleibt offen, weil dafuer eine zusaetzliche Contract-/Runner-Erweiterung noetig ist.

## Umsetzungsreihenfolge

1. **Statuskonstante in `TaskDetailViewModel` einfuehren**
   - Datei: `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
   - Beschreibung: Private Konstante fuer `Bereit Repository vor...` nahe bei den bestehenden Statusfeldern anlegen.

2. **Vorbereitungsstatus beim Start setzen**
   - Datei: `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
   - Voraussetzung: Schritt 1.
   - Beschreibung: In `StartenAsync(CancellationToken ct)` nach erfolgreicher Plugin-Aufloesung und direkt vor `ProzessStartenUndCliStartenAsync(...)` `CliStatusText` auf die Konstante setzen.

3. **Status in Fehler- und Abbruchpfaden zuruecksetzen**
   - Datei: `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
   - Voraussetzung: Schritt 2.
   - Beschreibung: Im `catch (OperationCanceledException)` und im allgemeinen `catch (Exception ex)` den Footerstatus auf inaktiv zuruecksetzen, ohne die vorhandene Fehlerbehandlung zu entfernen.

4. **ViewModel-Test fuer laufende Repository-Vorbereitung ergaenzen**
   - Datei: `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`
   - Voraussetzung: Schritte 1-3.
   - Beschreibung: Den Startpfad mit einem kontrolliert blockierenden `IGitPlugin.CloneRepositoryAsync(...)` testen. Waehrend die `TaskCompletionSource` noch nicht freigegeben ist, muss `sut.CliStatusText` exakt `Bereit Repository vor...` sein. Danach wird die Clone-Task freigegeben und der Start abgeschlossen.

5. **ViewModel-Test fuer Fehlerpfad ergaenzen**
   - Datei: `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`
   - Voraussetzung: Schritte 1-3.
   - Beschreibung: `CloneRepositoryAsync(...)` wirft eine Exception. Nach `StartenCommand.ExecuteAsync()` darf `sut.CliStatusText` nicht `Bereit Repository vor...` sein; `FehlerMeldung` muss gesetzt bleiben.

6. **Optional bestehenden Erfolgstest erweitern**
   - Datei: `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`
   - Voraussetzung: Schritte 1-3.
   - Beschreibung: Im bestehenden Erfolgstest pruefen, dass nach erfolgreichem Start nicht mehr der Vorbereitungsstatus angezeigt wird, sondern die bestehende CLI-Statuslogik aktiv ist.

7. **Tests ausfuehren**
   - Beschreibung: Mindestens `dotnet test` fuer das Testprojekt mit den `TaskDetailViewModelTests` ausfuehren. Falls die vorhandene Testdokumentation ConPTY-Ausnahmen verlangt, `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` setzen.

## Tests

### Neue Tests

| Test | Testklasse | Erwartung |
|---|---|---|
| `StartenAsync_ShouldShowRepositoryPreparationStatus_WhileCloneIsRunning` | `TaskDetailViewModelTests` | Bei blockierendem Clone steht `CliStatusText` waehrend des laufenden Starts exakt auf `Bereit Repository vor...`. |
| `StartenAsync_ShouldClearRepositoryPreparationStatus_WhenCloneFails` | `TaskDetailViewModelTests` | Nach Clone-Fehler bleibt `FehlerMeldung` sichtbar und `CliStatusText` ist nicht mehr `Bereit Repository vor...`. |

### Betroffene bestehende Tests

| Test / Testklasse | Erwartete Anpassung |
|---|---|
| `TaskDetailViewModelTests.TestStartenAsync_InvokesCombinedProcess_StartsCliUponSuccess` | Optional um Pruefung erweitern, dass der Vorbereitungsstatus nach erfolgreichem Start durch einen CLI-Status ersetzt wurde. |
| Weitere `TaskDetailViewModelTests` zum Startpfad | Muessen unveraendert gruen bleiben, weil Plugin-Auswahl, Persistenz und PseudoConsole-Session nicht geaendert werden. |

### E2E-Tests

Kein neuer E2E-Test erforderlich. Das geforderte Verhalten ist ein ViewModel-/Binding-Zustand waehrend eines asynchronen Repository-Starts und laesst sich deterministisch durch Unit-Tests mit blockierendem `CloneRepositoryAsync(...)` absichern. Timing-basierte UI-E2E-Tests fuer echte Klons waeren deutlich instabiler und liefern hier keinen zusaetzlichen belastbaren Nachweis.

## Offene Punkte

Keine.

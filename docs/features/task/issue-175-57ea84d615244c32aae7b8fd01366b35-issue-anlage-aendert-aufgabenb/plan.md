# Umsetzungsplan: Issue-Anlage aendert Aufgabenbeschreibung

Hinweis zur Ausfuehrung: Ein Unteragent war in dieser Umgebung nicht direkt aufrufbar; der `/plan`-Schritt wurde lokal nach Lifecycle-Vorgabe ausgefuehrt.

## Zielbild

Der Dialog zur Issue-Anlage erhaelt eine pro Dialoglauf gesetzte Checkbox "Aufgabenbeschreibung nach Issue-Anlage aktualisieren". Die Checkbox ist standardmaessig deaktiviert und wird nicht als Nutzerpraeferenz gespeichert. Ohne aktivierte Checkbox bleibt der bestehende Ablauf unveraendert.

Bei aktivierter Checkbox wird die Beschreibung genau der Aufgabe aktualisiert, fuer die das Issue angelegt wurde. Die Aktualisierung erfolgt erst nach erfolgreicher externer Issue-Anlage und gemeinsam mit der lokalen Issue-Zuordnung ueber den bestehenden Aufgaben-Persistenzpfad. Als Beschreibung wird primaer `createdIssue.Body` verwendet, weil dieser Wert den angelegten Provider-Stand repraesentiert. Ist dieser Body nicht gesetzt oder nur Whitespace, wird der lokal im Dialog verwendete Body als Fallback gespeichert.

Wenn das externe Issue erfolgreich angelegt wurde, die lokale Speicherung von Issue-Referenz und optionaler Aufgabenbeschreibung aber fehlschlaegt, wird der Fehler sichtbar in der Detailansicht gemeldet. Das externe Issue wird nicht zurueckgenommen.

## Betroffene Dateien

| Datei | Geplante Aenderung |
|---|---|
| `src/Softwareschmiede.App/ViewModels/IssueCreateDialogViewModel.cs` | Bool-Property fuer Checkbox-State ergaenzen, Default bei `Initialize` auf `false`, lokalen Submit-Body fuer Fallback verfuegbar machen. |
| `src/Softwareschmiede.App/Views/IssueCreateDialog.xaml` | Checkbox mit Binding und Automation-Name `AufgabenbeschreibungNachIssueAnlageAktualisieren` ergaenzen. |
| `src/Softwareschmiede.App/Services/IDialogService.cs` | Rueckgabe der Issue-Anlage von `Issue?` auf explizites Ergebnisobjekt erweitern. |
| `src/Softwareschmiede.App/Services/WpfDialogService.cs` | Ergebnisobjekt aus Dialog-ViewModel-State aufbauen. |
| `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs` | Neues Dialog-Ergebnis auswerten, Beschreibungstext bestimmen, kombinierte Service-Operation aufrufen und Fehlermeldungen anpassen. |
| `src/Softwareschmiede/Application/Services/AufgabeService.cs` | Kombinierte Methode fuer lokale Issue-Zuordnung und optionale Beschreibungsaenderung ergaenzen. |
| `src/Softwareschmiede.Tests/...` | ViewModel-, Service- und Dialog-Service-Mocks an neues Ergebnisobjekt und neue Faelle anpassen. |

## Umsetzungsschritte

1. Dialog-Ergebnis einfuehren
   - Ein kleines Value Object im App-Service-/ValueObject-Kontext einfuehren, z. B. `IssueCreateDialogResult(Issue Issue, bool UpdateTaskDescription, string? LocalBody)`.
   - `IDialogService.ShowIssueCreateDialogAsync(...)` auf `Task<IssueCreateDialogResult?>` umstellen.
   - `WpfDialogService` gibt bei erfolgreichem Dialogschluss ein Ergebnis mit `viewModel.CreatedIssue`, Checkbox-Wert und lokalem `Body` zurueck; bei Abbruch weiterhin `null`.

2. Dialog-State und UI ergaenzen
   - In `IssueCreateDialogViewModel` eine boolsche Property `UpdateTaskDescriptionAfterCreate` ergaenzen.
   - Die Property in `Initialize(...)` immer auf `false` setzen, damit wiederverwendete ViewModel-Instanzen keinen alten Wert behalten.
   - `ErstellenAsync` und Provider-Request unveraendert lassen; die Checkbox beeinflusst nur den lokalen Nachlauf.
   - In `IssueCreateDialog.xaml` eine Checkbox nahe an der Beschreibung beziehungsweise im unteren Dialogbereich platzieren:
     - Content: `Aufgabenbeschreibung nach Issue-Anlage aktualisieren`
     - Binding: `UpdateTaskDescriptionAfterCreate`
     - AutomationProperties.Name: `AufgabenbeschreibungNachIssueAnlageAktualisieren`

3. Kombinierte Aufgaben-Persistenz ergaenzen
   - In `AufgabeService` eine Methode ergaenzen, z. B.:
     `TryAssignIssueReferenzIfNoneAsync(Guid id, Issue issue, bool updateAnforderungsBeschreibung, string? anforderungsBeschreibung, CancellationToken ct = default)`.
   - Alternativ eine eindeutig benannte neue Methode verwenden, wenn dadurch bestehende Aufrufer lesbarer bleiben, z. B. `TryAssignIssueReferenzAndUpdateDescriptionIfNoneAsync`.
   - Die Methode:
     - prueft wie bisher, ob die Aufgabe existiert,
     - bricht mit `false` ab, wenn bereits eine IssueReferenz existiert,
     - legt die `IssueReferenz` aus dem Issue an,
     - setzt `Aufgabe.AnforderungsBeschreibung` nur bei `updateAnforderungsBeschreibung == true`,
     - speichert IssueReferenz und Beschreibung mit einem einzigen `SaveChangesAsync`,
     - behandelt den bestehenden `DbUpdateException`-Parallelfall weiter als `false`,
     - laesst die Beschreibung unveraendert, wenn parallel bereits eine IssueReferenz gesetzt wurde.
   - Die bestehende einfache Methode kann als Wrapper erhalten bleiben, der die neue Methode mit `false, null` aufruft.

4. TaskDetail-Flow anpassen
   - `TaskDetailViewModel.IssueAnlegenAsync` liest statt `Issue?` das neue `IssueCreateDialogResult?`.
   - Bei `null` unveraendert abbrechen.
   - Vor der lokalen Zuordnung den bestehenden Recheck auf vorhandene IssueReferenz beibehalten.
   - Bei aktivierter Checkbox den Beschreibungstext so bestimmen:
     - `createdIssue.Body`, wenn nicht null/Whitespace,
     - sonst `dialogResult.LocalBody`,
     - sonst `string.Empty`.
   - Die kombinierte Service-Operation mit `updateAnforderungsBeschreibung` und dem ermittelten Beschreibungstext aufrufen.
   - Wenn die Service-Operation `false` liefert, keine Beschreibung aktualisieren und die bestehende sichtbare Meldung fuer parallel zugeordnete Issues verwenden.
   - Bei Exception nach externer Issue-Erstellung eine sichtbare Meldung setzen, die beide lokalen Persistenzteile nennt, z. B. dass das Issue extern erstellt wurde, die lokale Zuordnung oder Aufgabenbeschreibung aber nicht gespeichert werden konnte. Das externe Issue bleibt bestehen.
   - Nach erfolgreicher lokaler Speicherung den vorhandenen `LadenAsync(ct)`-Reload nutzen.

5. Bestehende Aufrufer und Mocks aktualisieren
   - Alle Implementierungen und Test-Doubles von `IDialogService.ShowIssueCreateDialogAsync` auf das neue Ergebnisobjekt umstellen.
   - Bestehende Tests, die nur das angelegte Issue erwarten, mit `UpdateTaskDescription = false` migrieren.

## Testplan

1. `IssueCreateDialogViewModelTests`
   - Default der neuen Checkbox ist nach `Initialize` `false`.
   - Property ist setzbar und feuert `PropertyChanged`.
   - Provider-Erfolg setzt weiterhin `CreatedIssue`; Checkbox veraendert den `IssueCreateRequest` nicht.
   - Nach erneuter Initialisierung ist die Checkbox wieder `false`.

2. `TaskDetailViewModelTests`
   - Erfolgreiche Issue-Anlage mit deaktivierter Option persistiert die IssueReferenz und laesst `AnforderungsBeschreibung` unveraendert.
   - Erfolgreiche Issue-Anlage mit aktivierter Option setzt `AnforderungsBeschreibung` auf `createdIssue.Body`.
   - Wenn `createdIssue.Body` leer/Whitespace ist, wird der lokale Dialog-Body als Fallback verwendet.
   - Abgebrochener Dialog laesst IssueReferenz und Beschreibung unveraendert.
   - Parallele Issue-Zuordnung nach Dialog aktualisiert keine Beschreibung.
   - Lokaler Persistenzfehler nach erfolgreicher externer Issue-Anlage erzeugt eine sichtbare Fehlermeldung und fuehrt keinen externen Rollback aus.

3. `AufgabeServiceTests`
   - Kombinierte Service-Operation setzt IssueReferenz und Beschreibung in einem erfolgreichen Lauf.
   - Bei deaktiviertem Flag bleibt die vorhandene Beschreibung unveraendert.
   - Bei bereits vorhandener IssueReferenz gibt die Methode `false` zurueck und laesst die Beschreibung unveraendert.
   - Bestehende Tests fuer `TryAssignIssueReferenzIfNoneAsync` bleiben erfolgreich.

4. Auszufuehrende Testbefehle
   - `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter IssueCreateDialogViewModelTests`
   - `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter TaskDetailViewModelTests`
   - `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter AufgabeServiceTests`

## Risiken und Gegenmassnahmen

| Risiko | Gegenmassnahme |
|---|---|
| Bestehende Dialog-Service-Tests brechen durch neue Rueckgabe. | Explizites Ergebnisobjekt zentral einfuehren und alle Test-Doubles mechanisch migrieren. |
| Lokale Teilpersistenz hinterlaesst IssueReferenz ohne Beschreibung. | IssueReferenz und optionale Beschreibung in einer Service-Methode mit einem `SaveChangesAsync` speichern. |
| Provider liefert leeren Body zurueck. | Lokal gesendeten Dialog-Body als Fallback verwenden. |
| Parallel zugeordnete IssueReferenz fuehrt zu fremder Beschreibungsaenderung. | Bestehenden Vorab-Recheck und Service-Race-Schutz beibehalten; bei `false` keine Beschreibung setzen. |
| Fehler nach externer Issue-Anlage koennte fuer Nutzer unklar sein. | Sichtbare Fehlermeldung mit externer Issue-Identifikation und lokal fehlgeschlagenem Persistenzteil setzen. |

## Nicht umzusetzen

- Keine Speicherung des Checkbox-Werts als Nutzerpraeferenz.
- Keine Datenbankmigration.
- Keine Aenderung am Provider-Vertrag.
- Keine Synchronisation spaeterer Issue-Aenderungen zur Aufgabe.
- Keine automatische Aktualisierung fuer bereits bestehende Issues.

## Offene Punkte


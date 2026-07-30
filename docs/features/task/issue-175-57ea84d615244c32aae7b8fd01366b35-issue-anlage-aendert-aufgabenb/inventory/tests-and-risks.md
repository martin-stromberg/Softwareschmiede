# Tests und Risiken

## Vorhandene Testabdeckung

### IssueCreateDialogViewModelTests

Vorhandene Tests decken ab:

- Initialisierung von Titel und Body aus der Aufgabe.
- Leere/Whitespace-Beschreibung.
- Template-Zusammensetzung.
- Provider-Erfolg mit `IssueCreateRequest`.
- Kein Provider-Aufruf bei parallel zugeordneter Issue-Referenz.
- Kein Submit bei leerem Titel.
- Provider-Fehler und Exceptions bleiben im Dialog.
- Cancellation setzt Laufzustand zurueck.
- Doppelte Submit-Ausfuehrung waehrend Provider-Aufruf.
- KI-Ausfuellhilfe und Fehlerfaelle.

Neue Tests sollten ergaenzen:

- Neue Checkbox-Property ist initial `false`.
- Checkbox-Property feuert PropertyChanged und bleibt beim Submit erhalten.
- Provider-Erfolg setzt weiterhin `CreatedIssue`; Checkbox beeinflusst den Provider-Request nicht.

### TaskDetailViewModelTests

Ab etwa Zeile 1630 existiert bereits ein Block zur Issue-Anlage:

- `IssueAnlegenCommand_CanExecute_WhenProviderSupportsCreateAndNoReferenceExists`
- `IssueAnlegenCommand_ShouldUseRepositoryUrl_ForProviderCapability`
- `IssueAnlegenCommand_CannotExecute_WhenProviderDoesNotSupportCreate`
- `IssueAnlegenCommand_CannotExecute_WhenProviderHasNoCreateCapability`
- `IssueAnlegenCommand_CannotExecute_WhenProviderCapabilityFails`
- `IssueAnlegenCommand_CannotExecute_WhenIssueReferenceExists`
- `IssueAnlegenAsync_ShouldPersistCreatedIssueAndReloadTask`
- `IssueAnlegenAsync_ShouldShowExternalIssueUrl_WhenLocalPersistenceFails`
- `IssueAnlegenAsync_ShouldNotPersistReference_WhenDialogIsCancelled`
- `IssueAnlegenAsync_ShouldIgnoreSecondExecution_WhileCreateDialogIsOpen`
- `IssueAnlegenAsync_ShouldNotOverwriteReference_WhenIssueWasAssignedAfterDialog`

Neue Tests sollten hier pruefen:

- Bei erfolgreicher Issue-Anlage und deaktivierter Option bleibt `AnforderungsBeschreibung` unveraendert.
- Bei erfolgreicher Issue-Anlage und aktivierter Option wird `AnforderungsBeschreibung` auf den Issue-Body gesetzt.
- Bei abgebrochenem Dialog bleibt `AnforderungsBeschreibung` unveraendert.
- Bei Provider-Fehler bleibt `AnforderungsBeschreibung` unveraendert (wahrscheinlich schon ueber Dialog-Tests plus null-Rueckgabe abgedeckt).
- Bei paralleler Issue-Zuordnung nach Dialog wird keine Aufgabenbeschreibung aktualisiert, damit nicht eine Aufgabe mit fremder/konkurrierender Issue-Zuordnung veraendert wird.
- Bei lokalem Persistenzfehler nach externem Issue wird eine Meldung analog zur bestehenden externen-Issue-URL-Meldung angezeigt.

### AufgabeServiceTests

Vorhanden:

- `TryAssignIssueReferenzIfNoneAsync_ShouldPersistIssueReference_WhenNoneExists`
- `TryAssignIssueReferenzIfNoneAsync_ShouldReturnFalseAndKeepExistingReference_WhenReferenceExists`
- `UpdateIssueReferenzAsync_ShouldOverwriteExistingReference_WhenReferenceExists`
- `UpdateAsync_ShouldUpdateTitelAndAgentenInfos_WhenAufgabeExists`
- `CreateFromIssueAsync` und `CreateFromAlertAsync` setzen bereits Aufgabenbeschreibung aus Issue-Body.

Neue Tests haengen von der geplanten Service-API ab:

- Kombinierte Service-Methode setzt IssueReferenz und Beschreibung gemeinsam.
- Kombinierte Service-Methode laesst Beschreibung unveraendert, wenn Flag false ist.
- Kombinierte Service-Methode gibt false zurueck und laesst Beschreibung unveraendert, wenn bereits eine IssueReferenz existiert.

## Risiken

1. Inkonsistente lokale Persistenz: Wenn IssueReferenz und Aufgabenbeschreibung in zwei separaten Saves gespeichert werden, kann ein lokaler Fehler eine teilweise Aktualisierung hinterlassen.
2. Dialog-Service-Vertrag: Eine Erweiterung von `IDialogService.ShowIssueCreateDialogAsync` betrifft Mocks und WPF-Implementierung. Minimalinvasive Alternative ist moeglich, aber weniger explizit.
3. Quelle des Beschreibungstextes: `createdIssue.Body` kann providerseitig leer oder normalisiert sein. Die Planung muss Fallback-Verhalten festlegen.
4. Parallelitaet: Bestehender Schutz verhindert Ueberschreiben einer parallelen IssueReferenz. Die neue Beschreibungsaenderung darf diesen Schutz nicht umgehen.
5. UI-Zustand: Nach erfolgreicher Persistenz ist `LadenAsync` ausreichend. Ohne Reload muessten `Aufgabe` und `EditAnforderungsBeschreibung` manuell synchronisiert werden.

## Empfohlene Testauswahl fuer Umsetzung

- `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter IssueCreateDialogViewModelTests`
- `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter TaskDetailViewModelTests`
- `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter AufgabeServiceTests`

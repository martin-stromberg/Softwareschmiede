# Umsetzungsplan: Issue-Anlage aus der Aufgabendetailansicht

## Zielbild

Aus der Aufgabendetailansicht kann ein Anwender genau dann ein neues Issue im zum Repository gehörenden Provider anlegen, wenn der Aufgabe noch keine `IssueReferenz` zugeordnet ist. Ein Dialog übernimmt zunächst die Anforderungsbeschreibung, erlaubt die Bearbeitung, optional die Auswahl eines Provider-Templates und optional eine KI-gestützte Ausfüllhilfe. Erst nach erfolgreicher Provider-Anlage wird die lokale Referenz gespeichert und die Detailansicht neu geladen.

Die bestehende Funktion zum Zuordnen eines bereits vorhandenen Issues bleibt unverändert. Die neue Anlagefunktion erhält eine eigene Capability-/CanExecute-Prüfung, damit ihre Geschäftsregel nicht versehentlich die bestehende Zuordnung verändert.

## Umsetzungsschritte

### 1. Fachliche Datenmodelle und Provider-Verträge

- Im gemeinsamen Contract-Projekt ein providerunabhängiges Create-Modell für mindestens `Title` und `Body` sowie ein Ergebnis-/Template-Modell definieren.
- Die bestehende `Issue`-Referenzstruktur wiederverwenden, soweit sie Nummer, Titel, Body, Labels, Milestone und URL abbilden kann. Provider-spezifische Pflichtfelder nicht in das allgemeine Dialogmodell aufnehmen, sondern über ein klar begrenztes Optionsmodell oder Provider-Mapping behandeln.
- Ein separates Capability-Interface für Issue-Anlage und optionales Template-Laden vorsehen, statt alle `IGitPlugin`-Implementierungen zu einer Template-Unterstützung zu zwingen. Fehlende Fähigkeiten müssen unterscheidbar von einem echten Providerfehler sein.
- Verträge mit `CancellationToken` und einer Fehlersemantik definieren, bei der ein fehlgeschlagener Create-Aufruf keine leere, scheinbar erfolgreiche Referenz liefert.
- `GitPluginBase<TPlugin>` und alle betroffenen Test-Doubles/Mocks an den neuen Vertrag anpassen.

### 2. Provider-Implementierungen

- GitHub im bestehenden CLI-/Credential-Muster um Issue-Erstellung erweitern und den Provider-Identifier weiterhin als `owner/repo` verwenden.
- Für GitHub den vorhandenen Repository-Template-Mechanismus untersuchen und ein belastbares Template-Ergebnis auf das gemeinsame Modell abbilden. Nicht lesbare oder nicht vorhandene Templates als „keine Templates verfügbar“ behandeln, echte Authentifizierungs-/Netzwerkfehler aber an den Dialog melden.
- Bitbucket/Jira getrennt bewerten und die tatsächlich nutzbare Jira-Create-Schnittstelle mit den erforderlichen Feldern `project`, `summary`, `issuetype` und Beschreibung in Atlassian Document Format umsetzen. Die vorhandene `RenderAdf`-Richtung darf dabei nicht blind als Create-Payload wiederverwendet werden.
- Für Bitbucket/Jira nur dann Templates anbieten, wenn eine unterstützte Schnittstelle nachgewiesen und implementiert ist; ansonsten bleibt der No-Template-Pfad verfügbar.
- `LocalDirectory` und andere Provider ohne Issue-Erstellung liefern eine eindeutige Nichtunterstützt-Semantik. Die Anlageaktion wird für sie nicht angeboten.
- Providerfehler einschließlich fehlender Berechtigung, fehlender Verbindung, ungültigem Repository-/Projekt-Identifier und ungültigen Pflichtfeldern in verständliche, nicht erfolgreiche Ergebnisse überführen.

### 3. Anwendungspfad und Persistenzreihenfolge

- Im Aufgaben-/Anwendungsservice einen fokussierten Ablauf für „Issue erstellen und zuordnen“ ergänzen oder den Orchestrierungsanteil im `TaskDetailViewModel` auf bestehende Servicegrenzen verteilen.
- Vor dem Öffnen und unmittelbar vor dem Absenden prüfen, dass die Aufgabe noch keine Referenz besitzt. Die zweite Prüfung verhindert doppelte Anlagen bei veralteter UI oder parallelen Aktionen.
- Provider-Issue zuerst erstellen, danach `UpdateIssueReferenzAsync` mit der vollständigen Rückgabe aufrufen. Bei Abbruch, Providerfehler oder Validierungsfehler darf keine lokale Zuordnung entstehen.
- Den Fall „Provideranlage erfolgreich, lokale Speicherung fehlgeschlagen“ ausdrücklich als Fehler anzeigen und die externe Issue-Referenz (URL/Nummer) in der Meldung ausgeben. Da kein verlässliches allgemeines Rollback existiert, wird kein erneuter Create-Versuch automatisch ausgelöst.
- Nach erfolgreicher Speicherung `LadenAsync` ausführen, sodass `CurrentIssueReferenz` und die Ribbon-Zustände aus der Datenbank aktualisiert werden.

### 4. Dialog und ViewModel

- Nach dem Muster von `IssueSelectionDialog`/`IssueSelectionDialogViewModel` ein `IssueCreateDialog` mit eigenem ViewModel und `IDialogService`-/`WpfDialogService`-Eintrag ergänzen.
- Editierbare Felder für Titel und Beschreibung, Provider-Template-Auswahl, KI-Provider-Auswahl, Lade-/Submit-Zustand, Abbruch und Fehleranzeige vorsehen. Bestehende Eingaben bleiben bei einem Fehler erhalten.
- Im Dialog eine Liste der verfügbaren KI-Provider anzeigen und den Anwender den Provider für die Ausfüllaktion auswählen lassen. Einen konfigurierten Standardprovider vorselektieren, sofern vorhanden; die Auswahl gilt nur für die aktuelle Dialogaktion und ändert keine globale Konfiguration.
- Die ursprüngliche `AnforderungsBeschreibung` beim Öffnen als Body übernehmen; `null` oder whitespace-only wird ohne Fehler als leerer Ausgangswert behandelt.
- Bei Template-Auswahl deterministisch zusammensetzen: Template-Inhalt, Trennlinie, `Originalanforderung:` und nur bei vorhandener Beschreibung deren Inhalt. Das Ergebnis bleibt vollständig editierbar; erneute Template-Auswahl muss den Zustand nachvollziehbar aktualisieren und darf die Originalanforderung nicht verlieren.
- Submit deaktivieren bei fehlenden Pflichtfeldern, laufender Operation oder bereits bestehender Zuordnung. Abbruch schließt ohne Provider- oder Persistenzaufruf.
- Template-Laden optional und fehlertolerant gestalten: Nichtunterstützung bzw. keine Treffer blendet die Auswahl aus; ein echter Ladefehler wird verständlich angezeigt, ohne die Anlage ohne Template zu blockieren, sofern die Provideranlage selbst möglich ist.

### 5. KI-Ausfüllhilfe

- Einen rückgabefähigen, einmaligen Textgenerierungsvertrag für den Dialog ergänzen oder einen bestehenden KI-Dienst entsprechend erweitern. Den langlebigen CLI-Aufgabenprozess nicht direkt als synchronen Textgenerator verwenden.
- Den im Dialog ausgewählten KI-Provider mit dem ausgewählten Template und der Originalanforderung als Mindestinput aufrufen; das Ergebnis als editierbaren Body zurückschreiben.
- KI-Ausführung abbrechbar machen und Fehler im Dialog anzeigen, ohne den bisherigen Body zu verlieren. Die Anlage ohne KI bleibt vollständig unabhängig nutzbar.
- Die vorhandene Plugin-Auswahl für die verfügbaren KI-Provider und den Standardwert nutzen, sofern die notwendige Textgenerierungsfähigkeit unterstützt wird. Ist kein geeigneter Provider verfügbar, bleibt die KI-Aktion deaktiviert und die Anlage ohne KI nutzbar.

### 6. Detailansicht und Ribbon

- In `TaskDetailView.xaml` in der vorhandenen Issue-Gruppe einen neuen Button mit passendem Icon und Command ergänzen.
- Im `TaskDetailViewModel` Provider, Aufgabe und `IssueReferenz == null` in `CanCreateIssue`/`CanExecute` berücksichtigen. Kein Button bei fehlendem Repository, nicht unterstütztem Provider, bestehender Referenz, laufender Operation oder fehlender Issue-Anlagefähigkeit.
- Dialog öffnen, Ergebnis validieren, Create-/Persistenzablauf ausführen, Fehlerbanner bzw. bestehende Dialog-Fehlermechanik verwenden und anschließend neu laden.
- Bestehende `IssueZuweisenCommand`-Logik nicht durch die neue Einmaligkeitsprüfung verändern, sofern dies nicht ausdrücklich fachlich für beide Aktionen gewünscht ist.

### 7. Tests und Verifikation

- Contract-/Base-Tests für Capability, Nichtunterstützung, Cancellation und Fehler gegenüber Erfolg ergänzen.
- GitHub- und Bitbucket/Jira-Provider-Tests für Payload-Mapping, Pflichtfelder, Antwortmapping und Providerfehler ergänzen; insbesondere Jira-ADF und GitHub-Template-Auswertung testen.
- Dialog-ViewModel-Tests für Initialwerte, leere Beschreibung, Template-Komposition, vollständige Bearbeitung, Abbruch, KI-Erfolg/-fehler, Laden ohne Templates und Submit-Zustände ergänzen.
- `TaskDetailViewModelTests` um Sichtbarkeit/CanExecute bei vorhandener bzw. fehlender Referenz, nicht unterstütztem Provider, erfolgreicher Anlage, Providerfehler und Persistenzfehler erweitern.
- Integrationsnahe Tests für die Reihenfolge „Create vor `UpdateIssueReferenzAsync`“ und für das Verhindern doppelter Submit-Vorgänge ergänzen.
- Bestehende Test-Suites ausführen und prüfen, dass die Erweiterung von `IGitPlugin` keine unbeabsichtigten Brüche in Plugin-Implementierungen und Mocks verursacht.

## Betroffene Bereiche

- `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/GitPluginBase.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/` für Issue-/KI-Verträge und neue Modelle
- `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs`
- `plugins/Softwareschmiede.Plugin.BitBucket/BitBucketPlugin.cs`
- `plugins/Softwareschmiede.Plugin.LocalDirectory/LocalDirectoryPlugin.cs`
- `src/Softwareschmiede.Application/Services/AufgabeService.cs`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml` sowie neuer Issue-Create-Dialog
- `src/Softwareschmiede.App/Services/IDialogService.cs` und `WpfDialogService.cs`
- `src/Softwareschmiede.Tests/` für ViewModel-, Dialog-, Contract- und Provider-Tests

## Freigegebene Planentscheidungen

Die fachlichen Antworten liegen vor. Die folgenden Festlegungen gelten für die Implementierung; sie konkretisieren die im bisherigen Plan dokumentierten vorläufigen Annahmen.

| Punkt | Freigegebene Planentscheidung |
|---|---|---|
| Issue-Felder | Providerunabhängig werden Titel und Beschreibung unterstützt. Labels, Milestone und Status werden nur übernommen, wenn der jeweilige Provider sie ohne neue Fachanforderung sicher abbildet. |
| Titel | Der Aufgabentitel ist der editierbare Initialwert. Ein leerer Titel wird vor dem Absenden validiert, wenn der Provider ihn verlangt. |
| Bitbucket/Jira | Jira bleibt der tatsächlich genutzte Issue-Backendpfad und wird getrennt vom GitHub-Pfad umgesetzt. Projektauflösung, Issue-Typ, erforderliche Felder und ADF-Payload folgen der vorhandenen Jira-Konfiguration. Templates werden dort nur angeboten, wenn eine unterstützte Schnittstelle nachgewiesen und implementiert ist. |
| KI | Die Ausfüllhilfe verwendet ein einmaliges rückgabefähiges Textresultat. Der Anwender wählt den KI-Provider im Dialog; ein vorhandener Standardprovider wird nur vorselektiert. Der langlebige CLI-Aufgabenprozess wird nicht direkt als synchroner Textgenerator verwendet. |
| Platzhalter | Nicht unterstützte Template-Variablen bleiben im editierbaren Body unverändert und werden nicht stillschweigend entfernt. |
| Berechtigungen und Fehler | Providerfehler werden von „nicht unterstützt“ unterschieden und verständlich im Dialog angezeigt. Bei Fehlern entsteht keine lokale Zuordnung; bei erfolgreicher externer Anlage und fehlgeschlagener lokaler Speicherung werden URL/Nummer und die fehlende Transaktionsgarantie angezeigt bzw. protokolliert. |

Damit sind keine blockierenden fachlichen Entscheidungen mehr offen. Die konkrete technische Wahl zwischen einem neuen Textgenerierungsvertrag und einer einmaligen CLI-Ausführung ist eine Implementierungsentscheidung und muss die festgelegte Providerauswahl, Cancellation und ein rückgabefähiges Ergebnis erfüllen.

## Offene Punkte

Keine blockierenden offenen Punkte.

### Verbleibende nicht-blockierende Risiken

- Die verfügbaren KI-Provider können je nach Installation unterschiedliche Fähigkeiten, Laufzeiten oder Berechtigungen haben. Der Dialog muss ungeeignete Provider ausblenden oder die KI-Aktion deaktivieren und die Anlage ohne KI erlauben.
- Für Jira-/Bitbucket-Templates kann sich bei der Implementierung ergeben, dass keine unterstützte Schnittstelle verfügbar ist. In diesem Fall bleibt der No-Template-Pfad aktiv.
- Ein extern erfolgreich erstelltes Issue kann bei anschließendem Datenbankfehler verwaisen; ein providerübergreifendes Rollback ist nicht zugesichert.

## Risiken und Abgrenzungen

- Ein extern erfolgreich erstelltes Issue kann bei anschließendem Datenbankfehler verwaisen. Der Plan verhindert eine falsche lokale Zuordnung, kann aber ohne providerübergreifendes Rollback keine vollständige Transaktion herstellen.
- GitHub-Issue-Templates und Jira-/Bitbucket-Templates haben unterschiedliche Formate; ein gemeinsames Rohtextmodell darf keine providerabhängigen Formularfelder vortäuschen.
- Eine Erweiterung des verpflichtenden `IGitPlugin`-Vertrags kann alle Plugins und Test-Doubles brechen. Capability-Interfaces oder Default-Nichtunterstützung sind deshalb bevorzugt.
- Die KI-Unterstützung ist ausdrücklich optional und darf weder die Dialogöffnung noch die Issue-Anlage ohne Template blockieren.
- Nicht Bestandteil sind Bearbeiten/Löschen vorhandener Issues, Zuordnung vorhandener Issues über den neuen Dialog sowie Verwaltung von Repository-Templates.

## Abnahmekriterien für die Umsetzung

- Alle elf Akzeptanzkriterien aus `requirement.md` sind durch Tests oder nachvollziehbare Provider-/UI-Verifikation abgedeckt.
- Kein Create- oder Persistenzaufruf bei Abbruch; keine lokale Referenz bei Provider- oder Persistenzfehler.
- Nach erfolgreicher Anlage wird die Referenz angezeigt und die neue Anlageaktion ist nicht mehr verfügbar.
- Provider ohne Issue-Create-Unterstützung bieten die Aktion nicht an; fehlende Template-Unterstützung verhindert den No-Template-Pfad nicht.
- Die freigegebenen Planentscheidungen und die nicht-blockierenden Risiken sind im Plan dokumentiert; es verbleiben keine blockierenden offenen Punkte.

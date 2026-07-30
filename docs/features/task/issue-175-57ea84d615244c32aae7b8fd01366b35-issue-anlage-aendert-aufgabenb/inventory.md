# Bestandsaufnahme: Issue-Anlage aendert Aufgabenbeschreibung

Hinweis zur Ausfuehrung: Ein Unteragent war in dieser Umgebung nicht direkt aufrufbar; der `/inventory`-Schritt wurde lokal nach Lifecycle-Vorgabe ausgefuehrt.

## Zusammenfassung

Die Issue-Anlage ist bereits in `TaskDetailViewModel` integriert. Der Dialog `IssueCreateDialogViewModel` erzeugt beim Provider ein Issue und liefert nach erfolgreichem Dialogabschluss nur das erstellte `Issue` zurueck. Danach persistiert `TaskDetailViewModel.IssueAnlegenAsync` die lokale `IssueReferenz` ueber `AufgabeService.TryAssignIssueReferenzIfNoneAsync` und laedt die Aufgabe neu.

Die Aufgabenbeschreibung liegt im Domainmodell als `Aufgabe.AnforderungsBeschreibung` und wird in der Detailansicht ueber `EditAnforderungsBeschreibung` sowie `Aufgabe.AnforderungsBeschreibung` angezeigt. Es gibt bereits `AufgabeService.UpdateAsync`, das Titel, Beschreibung und KI-Plugin speichert. Eine atomare Service-Operation, die Issue-Referenz und optionale Aufgabenbeschreibung gemeinsam setzt, existiert noch nicht.

## Detaildokumente

- [UI- und Dialogfluss](inventory/ui-dialog.md)
- [Service, Domain und Persistenz](inventory/service-domain-persistence.md)
- [Provider-Vertraege und Rueckgabedaten](inventory/provider-contracts.md)
- [Tests und Risiken](inventory/tests-and-risks.md)

## Relevante Einstiegspunkte

| Bereich | Datei | Bedeutung |
|---|---|---|
| Issue-Dialog ViewModel | `src/Softwareschmiede.App/ViewModels/IssueCreateDialogViewModel.cs` | Haelt Titel, Body, Provider-Aufruf und `CreatedIssue`. |
| Issue-Dialog UI | `src/Softwareschmiede.App/Views/IssueCreateDialog.xaml` | Enthaelt Eingaben fuer Titel, Template/KI und Beschreibung; hier passt die neue Checkbox. |
| Aufgaben-Detailfluss | `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs` | Oeffnet den Dialog, verarbeitet das erstellte Issue und persistiert die lokale Zuordnung. |
| Dialog-Abstraktion | `src/Softwareschmiede.App/Services/IDialogService.cs` und `WpfDialogService.cs` | Rueckgabe fuer Issue-Anlage ist aktuell `Issue?`; kein Ergebnisobjekt mit Checkbox-State. |
| Aufgabenservice | `src/Softwareschmiede/Application/Services/AufgabeService.cs` | Speichert Aufgabenbeschreibung und Issue-Referenz ueber getrennte Methoden. |
| Domainmodell | `src/Softwareschmiede/Domain/Entities/Aufgabe.cs` und `IssueReferenz.cs` | Persistierte Beschreibung liegt an Aufgabe, Issue-Body separat an IssueReferenz. |
| Provider-Vertrag | `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IIssueCreateProvider.cs` | Provider liefert `IssueCreateResult` mit angelegtem `Issue`. |

## Bestand gegen Anforderungen

- Dialogoption: Noch nicht vorhanden.
- Aktivier-/deaktivierbarer Wert vor Bestaetigung: Noch nicht vorhanden, aber `IssueCreateDialogViewModel` ist der passende State-Ort.
- Bestehendes Verhalten ohne Option: Kann erhalten bleiben, wenn Default `false` ist und Persistenzpfad nur bei aktivierter Option laeuft.
- Aktualisierung nur bei erfolgreicher Issue-Anlage: Der Dialog setzt `CreatedIssue` nur bei `IssueCreateResult.Success`; der anschliessende TaskDetail-Pfad ist daher der richtige Hook.
- Persistenz: `AufgabeService.UpdateAsync` speichert Beschreibung; fuer weniger Race- und Fehlerflaeche sollte eine gezielte neue Service-Methode geprueft werden.
- UI-Aktualisierung: `TaskDetailViewModel` ruft nach erfolgreicher lokaler Zuordnung `LadenAsync` auf; damit wuerde auch eine geaenderte Beschreibung neu angezeigt.
- Fehlerbehandlung nach extern erfolgreichem Issue: Fuer lokale Zuordnungsfehler existiert bereits eine sichtbare Fehlermeldung mit externer Issue-URL; dieser Stil sollte fuer fehlgeschlagene Beschreibungsspeicherung wiederverwendet werden.

## Offene Punkte fuer die Planung

1. Quelle fuer die neue Aufgabenbeschreibung: Bestand spricht fuer `createdIssue.Body`, weil `IssueCreateResult` das tatsaechlich angelegte Issue repraesentiert. Wenn der Provider keinen Body zurueckliefert, sollte der lokal gesendete Dialog-Body als Fallback in Betracht kommen.
2. Rueckgabeform des Dialogs: Entweder `IssueCreateDialogViewModel` nach `ShowIssueCreateDialogAsync` auslesen oder ein explizites Ergebnisobjekt fuer `Issue?` plus `UpdateTaskDescription` einfuehren.
3. Persistenzsemantik: Getrennte Service-Aufrufe sind einfacher, eine kombinierte Methode `TryAssignIssueReferenzIfNoneAsync(..., updateDescription, description)` verhindert teilweise inkonsistente lokale Zustaende besser.
4. Fehlerfall "Issue erstellt, Beschreibung nicht gespeichert": Bestehende Meldungen fuer lokale Zuordnungsfehler sollten um die Beschreibungskomponente erweitert werden, ohne das externe Issue rueckgaengig zu machen.

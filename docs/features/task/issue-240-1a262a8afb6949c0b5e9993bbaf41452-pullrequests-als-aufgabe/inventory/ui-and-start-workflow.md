# UI und Aufgabenstart

## Vorschlagsliste

- `src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs:176-202` stellt `OffeneAnforderungen`, `IssueVorschlaege` und die Ladefähigkeit bereit.
- `ProjectDetailViewModel.cs:863-918` leert die Vorschläge, lädt Issues via `GetIssuesAsync` und filtert bereits über `IssueReferenz.IssueNummer` verknüpfte Aufgaben. Alerts werden separat ergänzt.
- `ProjectDetailViewModel.cs:926-977` dispatcht und erstellt aktuell nur Issue-Aufgaben; der Alert-Pfad folgt ab `:979`.
- `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmRequirement.cs:4-46` modelliert die gemeinsame Liste, aber nur `Issue` und `Alert`. `ScmRequirementKind` hat entsprechend nur diese beiden Werte.
- `src/Softwareschmiede.App/Views/ProjectDetailView.xaml:422-489` rendert die Liste und bindet den Double-Click-Handler. Die Kennzeichnung eines PR muss in diesem gemeinsamen Item-Template oder über typabhängige Anzeigeeigenschaften erfolgen.

## Aufgabenstart

- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs:1690-1725` startet über `EntwicklungsprozessService.ProzessStartenUndCliStartenAsync`.
- `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs:87-111,501-552` entscheidet aktuell anhand eines optionalen `basisBranchName`: bei vorhandenem Wert wird `CheckoutRemoteBranchAsync` genutzt, sonst wird ein neuer Branch angelegt.
- `EntwicklungsprozessService.cs:562-571` persistiert das Ergebnis über `AufgabeService.StartenAsync`. Der PR-Quell-Branch muss vor bzw. innerhalb dieses Pfads aus der Aufgabe geladen und als existierender Branch übergeben werden.
- Aufgaben ohne PR-Referenz müssen weiterhin den bestehenden Default-Branch-/Feature-Branch-Pfad durchlaufen.

## Bestehende PR-Anzeige

`TaskDetailViewModel.cs:263,852-871` und `src/Softwareschmiede.App/Views/TaskDetailView.xaml:509-518` zeigen bereits zu einer Aufgabe gespeicherte Pullrequests. Die neue Funktion sollte diese Anzeige wiederverwenden, statt einen zweiten PR-Datentyp für die Aufgabendetailansicht einzuführen.

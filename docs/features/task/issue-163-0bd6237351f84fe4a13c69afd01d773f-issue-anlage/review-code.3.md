# Code-Review

Status: Befunde vorhanden

## Befunde

1. **Mittel - Die Einmaligkeitspruefung fuer lokale Issue-Zuordnung ist nicht atomar und kann parallele Zuordnungen ueberschreiben.**  
   `IssueAnlegenAsync` prueft nach erfolgreicher externer Issue-Anlage noch einmal den aktuellen Aufgabenstand ([TaskDetailViewModel.cs](../../../../src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs):1059) und ruft danach separat `UpdateIssueReferenzAsync` auf ([TaskDetailViewModel.cs](../../../../src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs):1067). Zwischen diesen beiden awaits kann eine andere UI-Aktion oder Instanz der Aufgabe bereits ein Issue zuordnen. Der Service schuetzt diesen Fall nicht, sondern aktualisiert eine vorhandene Referenz aktiv ([AufgabeService.cs](../../../../src/Softwareschmiede/Application/Services/AufgabeService.cs):246). Damit kann die neue Anlage ein parallel zugeordnetes Issue ueberschreiben, obwohl die Geschaeftsregel pro Aufgabe hoechstens ein Issue erlaubt. Der vorhandene Test `IssueAnlegenAsync_ShouldNotOverwriteReference_WhenIssueWasAssignedAfterDialog` deckt nur den Fall ab, dass die Parallelzuordnung vor der zweiten ViewModel-Pruefung passiert; das eigentliche Rennen zwischen Pruefung und Persistenz bleibt offen. Sinnvoll waere eine atomare Service-Operation wie `TryAssignIssueReferenzIfNoneAsync` bzw. ein transaktionaler/konkurrenzsicherer Write, der bei bereits vorhandener Referenz fehlschlaegt statt zu ueberschreiben.

## Verifikation

- Code-Review der aktuellen uncommitted Aenderungen durchgefuehrt.
- Archivierte Befunde aus `review-code.2.md` geprueft: Codex/Claude implementieren jetzt `IIssueTemplateTextGenerator`; GitHub laedt nur noch Markdown-Templates und filtert YAML-Issue-Forms sowie `config.yml` aus.
- Keine Tests ausgefuehrt, da der Auftrag auf Code-Review und Artefakt-Erstellung beschraenkt war.

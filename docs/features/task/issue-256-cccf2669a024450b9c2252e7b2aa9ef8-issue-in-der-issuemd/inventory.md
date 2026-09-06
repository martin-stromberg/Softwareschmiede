# Bestandsaufnahme: Issue in der issue.md

Analysiert wurde der Bereich rund um `EntwicklungsprozessService.CreateIssueFileAsync()` sowie die beteiligten Datenmodellklassen `Aufgabe` und `IssueReferenz`, bezogen auf die Anforderung, die `issue.md` beim Aufgabenstart um einen optionalen Issue-Abschnitt zu erweitern.

## Zusammenfassung

- **`IssueReferenz`-Entität ist vollständig vorhanden** mit allen relevanten Eigenschaften: `IssueNummer` (`int?`), `Titel` (`string`), `IssueUrl` (`string?`), `Body` (`string?`), `LabelsJson`, `Milestone`.
- **`Aufgabe.IssueReferenz`-Navigationseigenschaft ist vorhanden** und wird von `AufgabeService.GetDetailAsync()` bereits per Eager Loading geladen — kein zusätzlicher Ladeaufwand notwendig.
- **`CreateIssueFileAsync` ist vorhanden**, erzeugt aber noch keinen Issue-Abschnitt. Der aktuelle Markdown-Inhalt beschränkt sich auf Titel, Aufgaben-ID, Branch und Anforderungsbeschreibung.
- **Tests für `CreateIssueFileAsync` sind vorhanden** (Standardinhalt, Fallback-Text, Fehlerbehandlung, Arbeitsverzeichnis-Konfiguration), aber es fehlen dedizierte Tests für das Verhalten mit und ohne gesetzte `IssueReferenz`.
- Es sind **keine Änderungen am Datenmodell, an Interfaces oder an der Datenbankstruktur** notwendig — alle benötigten Felder und Beziehungen sind bereits persistiert.

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Tests](inventory/tests.md)

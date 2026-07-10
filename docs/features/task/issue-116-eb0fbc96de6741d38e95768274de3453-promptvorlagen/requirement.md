# Anforderung: Promptvorlagen

## Ziel

In der Anwendung sollen Promptvorlagen konfiguriert und in der Aufgabendetailansicht verwendet werden können, um vordefinierte Prompts an die ausgeführte CLI zu senden.

## Fachlicher Kontext

Benutzer arbeiten in Projekten und Aufgaben mit einer CLI-Ausführung. Wiederkehrende Prompts sollen nicht jedes Mal manuell eingegeben werden müssen, sondern zentral in den Einstellungen gepflegt und in der Aufgabendetailansicht ausgewählt werden können.

## Funktionale Anforderungen

### Promptvorlagen verwalten

- In den Einstellungen muss es eine Möglichkeit geben, Promptvorlagen einzurichten.
- Jede Promptvorlage besitzt die folgenden Eigenschaften:
  - Name
  - Prompttext
- Der Name dient als Anzeige- und Auswahltext für die Vorlage.
- Der Prompttext enthält den an die CLI zu sendenden Text.

### Promptvorlagen in der Aufgabendetailansicht verwenden

- In der Aufgabendetailansicht muss im Ribbon-Menü eine Auswahlbox für Promptvorlagen verfügbar sein.
- Die Auswahlbox zeigt die eingerichteten Promptvorlagen an.
- Wenn eine Promptvorlage ausgewählt wird, wird der zugehörige Prompttext an die ausgeführte CLI abgesendet.
- Vor dem Absenden müssen unterstützte Platzhalter im Prompttext automatisch aufgelöst werden.

### Platzhalter auflösen

Der Prompttext kann Platzhalter enthalten. Vor dem Absenden an die CLI müssen diese wie folgt ersetzt werden:

| Platzhalter | Ersetzung |
|-------------|-----------|
| `%ProjectName%` | Name des aktuellen Projekts |
| `%TaskName%` | Name der aktuellen Aufgabe |
| `%RepositoryUrl%` | URL des dem Projekt zugewiesenen Repositories |

## Initiale Daten

Beim Programmstart sollen einmalig die folgenden Promptvorlagen eingerichtet werden:

| Name | Prompttext |
|------|------------|
| `Initialanforderung senden` | `Die Anforderung zum Thema '%TaskName%' ist in issue.md beschrieben.` |
| `Weitermachen` | `Mach bitte weiter` |
| `Pullrequest` | `Push nun alle Commits und erstelle einen PR` |

Die initialen Promptvorlagen dürfen nur einmalig angelegt werden und sollen bestehende Benutzeränderungen oder bereits vorhandene Vorlagen nicht überschreiben.

## Akzeptanzkriterien

- In den Einstellungen können Promptvorlagen mit Name und Prompttext eingerichtet werden.
- In der Aufgabendetailansicht ist im Ribbon-Menü eine Auswahlbox für Promptvorlagen sichtbar.
- Die Auswahlbox enthält die eingerichteten Promptvorlagen.
- Bei Auswahl einer Vorlage wird der zugehörige Prompttext an die aktuell ausgeführte CLI gesendet.
- Vor dem Senden werden `%ProjectName%`, `%TaskName%` und `%RepositoryUrl%` durch die Werte des aktuellen Kontexts ersetzt.
- Beim ersten Programmstart werden die drei initialen Promptvorlagen angelegt.
- Bereits vorhandene Promptvorlagen oder nachträglich geänderte initiale Vorlagen werden beim späteren Programmstart nicht erneut überschrieben.

## Nicht-funktionale Anforderungen

- Die Promptvorlagen müssen dauerhaft gespeichert werden.
- Die Platzhalterersetzung muss deterministisch und vor dem CLI-Versand erfolgen.
- Fehlende Kontextwerte sollen die Anwendung nicht abstürzen lassen.

## Offene Punkte

- Wie soll die Anwendung fehlende Werte für `%RepositoryUrl%` behandeln, wenn dem Projekt kein Repository zugewiesen ist?
- Soll die Auswahl einer Promptvorlage den Prompt sofort absenden oder zunächst in ein Eingabefeld übernehmen?

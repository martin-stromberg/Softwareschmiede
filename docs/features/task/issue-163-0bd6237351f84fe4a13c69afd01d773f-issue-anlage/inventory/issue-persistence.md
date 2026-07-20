# Issue-Domain, Persistenz und Serviceablauf

## Domain-Modelle

`src/Softwareschmiede/Domain/Entities/Aufgabe.cs:18-21` enthält Titel und optionale `AnforderungsBeschreibung`. Die Aufgabe referenziert optional `GitRepository` und `IssueReferenz` (`Aufgabe.cs:74-81`).

`IssueReferenz` (`src/Softwareschmiede/Domain/Entities/IssueReferenz.cs`) speichert:

- `IssueNummer` als nullable `int`,
- `Titel`, `Body`, `LabelsJson`, `Milestone` und `IssueUrl`,
- eine 1:1-Verknüpfung über `AufgabeId`.

Das Modell hat kein Feld für Provider-Typ, externe stabile ID, Erstellungsstatus oder einen Issue-Titel getrennt von der lokalen Referenz. Für GitHub-/Jira-Nummern ist die vorhandene Struktur unterschiedlich belastbar: Jira wird aktuell mit `Nummer: 0` eingelesen und kodiert den Schlüssel im Titel.

## Datenbankbeziehung

`SoftwareschmiededDbContext` konfiguriert die 1:1-Beziehung von Aufgabe und `IssueReferenz` mit `IssueReferenz.AufgabeId` als Fremdschlüssel. Eine neue Persistenzstruktur ist für die reine Neuanlage nicht zwingend erforderlich, sofern der Provider ein kompatibles `Issue`-Ergebnis liefert.

## Bestehende Servicepfade

`AufgabeService.GetDetailAsync` lädt `IssueReferenz`, `GitRepository`, Projekt und Protokolle mit EF-Core-Includes.

`CreateFromIssueAsync` erzeugt eine Aufgabe aus einem bereits vorhandenen Provider-Issue und legt dabei direkt eine `IssueReferenz` an.

`UpdateIssueReferenzAsync` (`AufgabeService.cs:228-276`) lädt die Aufgabe samt Referenz, legt bei fehlender Referenz eine neue Entität an oder aktualisiert eine vorhandene und ruft anschließend `SaveChangesAsync` auf. Bei `issue == null` wird die Referenz gelöscht. Diese Methode ist für die Neuanlage nur nach erfolgreicher Provider-Erstellung geeignet; sie erstellt selbst kein externes Issue und verhindert bei parallelen Aufrufen nicht zuverlässig die doppelte Anlage.

## Erforderliche Ablaufgrenze

Der gewünschte Ablauf sollte fachlich so getrennt bleiben:

1. Aktuelle Aufgabe laden und vor dem Öffnen prüfen, dass keine `IssueReferenz` vorhanden ist.
2. Titel/Body/Template/KI nur im Dialog bearbeiten.
3. Provider-Issue erstellen und dessen vollständige Referenz erhalten.
4. Erst danach lokale `IssueReferenz` speichern.
5. Detailansicht neu laden und Anlage-Aktion deaktivieren/ausblenden.

Provider- oder Persistenzfehler dürfen nicht als Erfolg signalisiert werden. Für den Fall „Provider erfolgreich, lokale Speicherung fehlgeschlagen“ braucht der Plan eine explizite Fehlerstrategie, da eine verwaiste externe Anlage nicht automatisch zurückgerollt werden kann.

# Umsetzungsplan: Promptvorlagen

## Zielbild

Promptvorlagen werden dauerhaft in der Anwendung verwaltet, beim ersten Start mit drei Standardvorlagen angelegt und in der Aufgabendetailansicht im Ribbon angeboten. Waehlt der Benutzer eine Vorlage aus, wird ihr Prompttext unmittelbar mit aufgeloesten Kontextplatzhaltern an die aktuell laufende CLI-Session gesendet.

## Designentscheidungen

- Persistenz erfolgt als eigenes Domain-Modell `PromptVorlage` mit eigener Tabelle statt als serialisierter `AppEinstellung`-Wert. Die Vorlagen sind mehrzeilige, editierbare Datensaetze und sollen stabil erweiterbar bleiben.
- Initialdaten werden idempotent beim App-Start ueber einen Seed-Service angelegt. Wenn bereits mindestens eine Promptvorlage existiert, wird nicht erneut geseedet; bestehende Benutzeranpassungen werden dadurch nicht ueberschrieben.
- Die Auswahl in der Aufgabendetailansicht ist ein Sofortversand. Die Anforderung formuliert, dass durch die Auswahl eben dieser Prompt in der ausgefuehrten CLI abgesendet wird; es wird kein vorgeschaltetes Eingabefeld eingefuehrt.
- Fehlende Kontextwerte werden deterministisch zu leerem Text aufgeloest. Das gilt insbesondere fuer `%RepositoryUrl%`, wenn die Aufgabe kein Repository besitzt. Die Anwendung stuerzt dadurch nicht ab und versendet den verbleibenden Prompttext.
- Der Versand an die CLI erfolgt ueber eine ViewModel-/Service-Methode, nicht ueber Tastaturereignisse des `TerminalControl`. Dadurch koennen Promptvorlagen getestet und unabhaengig von UI-Fokus verwendet werden.
- Der gesendete Prompt wird mit Zeilenende abgeschlossen, damit er in der CLI tatsaechlich ausgefuehrt wird.

## Datenmodell und Persistenz

1. Neue Entity `PromptVorlage` in `src/Softwareschmiede/Domain/Entities/PromptVorlage.cs` anlegen.
   - `Guid Id`
   - `string Name`
   - `string Prompttext`
   - `int Sortierung`
   - `DateTimeOffset ErstelltAm`
   - `DateTimeOffset AktualisiertAm`
2. `SoftwareschmiededDbContext` um `DbSet<PromptVorlage> PromptVorlagen` und Mapping erweitern.
   - `Name` und `Prompttext` sind Pflichtfelder.
   - `Name` erhaelt eine sinnvolle Maximallaenge.
   - `Sortierung` wird indiziert, um die Anzeige stabil sortieren zu koennen.
3. EF-Core-Migration fuer die neue Tabelle erstellen.
4. Einen `PromptVorlagenService` in `src/Softwareschmiede/Application/Services/` einfuehren.
   - `GetAllAsync(CancellationToken)`
   - `CreateAsync(string name, string prompttext, CancellationToken)`
   - `UpdateAsync(Guid id, string name, string prompttext, CancellationToken)`
   - `DeleteAsync(Guid id, CancellationToken)`
   - `EnsureInitialPromptVorlagenAsync(CancellationToken)`
5. Initiale Vorlagen im Seed-Service exakt anlegen:
   - `Initialanforderung senden`: `Die Anforderung zum Thema '%TaskName%' ist in issue.md beschrieben.`
   - `Weitermachen`: `Mach bitte weiter`
   - `Pullrequest`: `Push nun alle Commits und erstelle einen PR`

## Platzhalterauflosung

1. Einen kleinen Resolver einfuehren, z. B. `PromptVorlagenPlatzhalterService`.
2. Unterstuetzte Platzhalter exakt ersetzen:
   - `%ProjectName%` durch `Aufgabe.Projekt.Name`
   - `%TaskName%` durch `Aufgabe.Titel`
   - `%RepositoryUrl%` durch `Aufgabe.GitRepository.RepositoryUrl`
3. Fehlende Werte werden mit `string.Empty` ersetzt.
4. Unbekannte Platzhalter bleiben unveraendert, damit spaetere Erweiterungen keine bestehenden Vorlagen zerstoeren.
5. Die Ersetzung erfolgt unmittelbar vor dem Versand im `TaskDetailViewModel`.

## CLI-Versand

1. In `TaskDetailViewModel` eine Command-Logik fuer `PromptVorlageAuswaehlenCommand` ergaenzen.
2. Beim Auswaehlen:
   - aktuelle Vorlage pruefen,
   - aktuelle Aufgabe mit Kontext verwenden,
   - Prompttext aufloesen,
   - aktive `PseudoConsoleSession` ueber vorhandene Session-Mechanik ermitteln,
   - Text plus Zeilenende UTF-8-kodiert in `InputStream` schreiben,
   - `MarkInputActivity()` auf der Session aufrufen.
3. Wenn keine CLI-Session laeuft oder kein Prompttext vorhanden ist, wird nicht gesendet und die UI bleibt stabil. Optional kann eine bestehende Fehlermeldungs-/Statusleistenmechanik genutzt werden, falls vorhanden.
4. Die vorhandene Terminal-Tastatureingabe bleibt unveraendert.

## Settings-UI

1. `SettingsViewModel` um eine editierbare Collection fuer Promptvorlagen erweitern.
2. Laden:
   - vorhandene Promptvorlagen ueber `PromptVorlagenService` laden,
   - in UI-Modelle mit Name und Prompttext ueberfuehren.
3. Speichern:
   - neue, geaenderte und geloeschte Eintraege persistieren,
   - leere Namen oder leere Prompttexte validieren und wie vorhandene Pflichtfeldvalidierung behandeln.
4. Verwerfen:
   - Promptvorlagen erneut aus der Persistenz laden.
5. `SettingsView.xaml` um einen Tab `Promptvorlagen` ergaenzen.
   - Liste der Vorlagen,
   - Eingaben fuer Name und mehrzeiligen Prompttext,
   - Aktionen zum Hinzufuegen und Loeschen.
6. Bestehende Tabs und Plugin-Settings bleiben funktional unveraendert.

## Task-Detail-UI

1. `TaskDetailViewModel` laedt beim Oeffnen die verfuegbaren Promptvorlagen.
2. `TaskDetailView.xaml` erhaelt im Ribbon eine Auswahlbox fuer Promptvorlagen, vorzugsweise in der bestehenden CLI-Gruppe.
3. Angezeigt wird der Vorlagenname.
4. Die Auswahlbox wird deaktiviert, wenn keine Vorlagen vorhanden sind oder keine CLI-Session laeuft.
5. Nach erfolgreichem Versand wird die Auswahl wieder geleert, damit dieselbe Vorlage erneut ausgewaehlt und erneut gesendet werden kann.

## Dependency Injection und Startablauf

1. `PromptVorlagenService` und Platzhalterresolver in `App.xaml.cs` registrieren.
2. Nach `db.Database.MigrateAsync()` im Startup `EnsureInitialPromptVorlagenAsync()` ausfuehren.
3. Service-Abhaengigkeiten in `SettingsViewModel` und `TaskDetailViewModel` ergaenzen und Tests/Fabriken entsprechend anpassen.

## Tests

1. Service-Tests fuer Initialdaten:
   - legt drei Vorlagen an, wenn keine vorhanden sind,
   - legt keine zweite Kopie an, wenn bereits Vorlagen existieren,
   - ueberschreibt geaenderte Vorlagen nicht.
2. Resolver-Tests:
   - ersetzt alle drei bekannten Platzhalter,
   - ersetzt fehlende RepositoryUrl durch leeren Text,
   - laesst unbekannte Platzhalter unveraendert.
3. Settings-ViewModel-Tests:
   - laedt Promptvorlagen,
   - speichert neue/geaenderte/geloeschte Vorlagen,
   - validiert leere Pflichtfelder,
   - verwirft ungespeicherte Aenderungen.
4. TaskDetailViewModel-Tests:
   - laedt Promptvorlagen fuer die Auswahl,
   - sendet ausgewaehlte Vorlage sofort an die laufende Session,
   - sendet aufgeloeste Platzhalterwerte,
   - stuerzt ohne Repository oder ohne laufende Session nicht ab.
5. Optionaler UI-/XAML-Test oder manuelle Sichtpruefung fuer Ribbon-Auswahl und Settings-Tab.

## Akzeptanzabdeckung

| Akzeptanzkriterium | Umsetzung |
|--------------------|-----------|
| Promptvorlagen mit Name und Prompttext einrichten | Settings-Tab mit persistiertem `PromptVorlage`-Modell |
| Auswahlbox in der Aufgabendetailansicht sichtbar | Ribbon-ComboBox in `TaskDetailView.xaml` |
| Auswahlbox enthaelt eingerichtete Vorlagen | `TaskDetailViewModel` laedt `PromptVorlagenService.GetAllAsync()` |
| Auswahl sendet Prompt an aktuelle CLI | `PromptVorlageAuswaehlenCommand` sendet sofort an `PseudoConsoleSession.InputStream` |
| Platzhalter werden vor dem Senden ersetzt | `PromptVorlagenPlatzhalterService` |
| Initiale drei Vorlagen beim ersten Programmstart | idempotenter Seed nach Migration |
| Bestehende/geaenderte Vorlagen werden nicht ueberschrieben | Seed nur bei leerer Vorlagentabelle |
| Fehlende Kontextwerte stuerzen nicht ab | fehlende Werte werden zu leerem Text |

## Offene Punkte

Keine.

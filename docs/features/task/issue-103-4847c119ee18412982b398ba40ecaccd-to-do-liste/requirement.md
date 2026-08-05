# Anforderung

## Fachliche Zusammenfassung

Jede Aufgabe erhält eine To-Do-Liste zur Unterstützung der Aufgabengliederung und des Fortschritts. To-Do-Elemente können in der Aufgabendetailansicht angelegt und mit einem Haken als erledigt gekennzeichnet werden. Das System verhindert den Abschluss einer Aufgabe (Status: `Beendet`), solange noch offene To-Dos vorhanden sind. Die Aufgabenliste im Ribbon-Menü zeigt — sofern möglich — die Anzahl der noch ausstehenden To-Dos an.

## Betroffene Klassen und Komponenten

### Domänenmodell und Persistenz

- **Neue Entity `Todo`:** Modeliert ein einzelnes To-Do-Element mit folgenden Eigenschaften:
  - `Id` (Guid): Eindeutige Kennung
  - `AufgabeId` (Guid): Fremdschlüssel zur Aufgabe
  - `Beschreibung` (string): Text des To-Dos
  - `ErledaltAm` (DateTimeOffset?): Zeitstempel der Fertigstellung (null = offen)
  - `ErstellungsDatum` (DateTimeOffset): Zeitstempel der Erstellung
  - Navigationseigenschaft: `Aufgabe` (zurück zur Aufgabe)

- **Erweiterung der Entity `Aufgabe`:**
  - Navigationseigenschaft `List<Todo> Todos` für die Sammlung der To-Dos dieser Aufgabe (analog zu `PullRequests` oder `Protokolleintraege`)

- **Entity Framework DbContext:**
  - Migration erforderlich für neue `Todo`-Tabelle
  - Konfiguration der 1:n-Beziehung zwischen `Aufgabe` und `Todo`

### Geschäftslogik und Services

- **Service zur Validierung von Aufgabenabschluss:**
  - Methode zum Prüfen, ob offene To-Dos existieren (z. B. `HatAufgabeLetzteZuAbschliessendeTodos()` oder ähnlich)
  - Wird vor dem Statusübergang zu `Beendet` aufgerufen

- **Service zur Verwaltung von To-Dos:**
  - Todo erstellen
  - Todo als erledigt markieren
  - Todo löschen
  - Abfrage der offenen To-Dos pro Aufgabe

### Benutzeroberfläche

- **Neue Inhaltsbereich/"Tab" in der Aufgabendetailansicht:** 
  - Anzeige der To-Do-Liste (z. B. neben `Info`, `CLI`, `Dateien`)
  - Eingabefeld zum Hinzufügen neuer To-Dos
  - Liste mit To-Do-Einträgen, jeder mit Checkbox für Erledigungsstatus
  - Option zum Löschen von To-Dos

- **Erweiterung des Ribbon-Menüs (`TaskDetailView.xaml`):**
  - Badge/Label an einer geeigneten Stelle (z. B. bei der Gruppe "Aufgabe") zur Anzeige der Anzahl offener To-Dos
  - Beispiel: "Beenden (⚠ 3 offene Todos)" oder ähnliche Darstellung

- **ViewModel `TaskDetailViewModel`:**
  - Property `List<TodoViewModel> Todos` für Bindung
  - Property `int OffeneTodoCount` für Badge-Anzeige
  - Command `AufgabeBeendenCommand` muss erweitert werden, um Validierung zu prüfen
  - Command `TodoHinzufuegenCommand`
  - Command `TodoAlsErledeltMarkierenCommand`
  - Command `TodoLoeschenCommand`
  - Fehlerbehandlung: Falls Benutzer versucht, mit offenen To-Dos zu beenden, hilfreiche Fehlermeldung (z. B. "Diese Aufgabe kann nicht beendet werden, solange noch X offene To-Dos vorhanden sind.")

### Tests

- Unit Tests für die Validierungslogik (offene To-Dos verhindern Abschluss)
- Unit Tests für To-Do-CRUD-Operationen
- E2E Tests für das Erstellen, Aktualisieren und Löschen von To-Dos in der UI
- E2E Test für den Versuch, eine Aufgabe mit offenen To-Dos zu beenden (sollte fehlschlagen)

## Implementierungsansatz

### 1. Datenbankmodell

- **neue Entity `Todo`** mit EF-Konfiguration erstellen (DbSet, foreign key, cascade delete)
- **Migration** generieren und anwenden
- **Aufgabe.Todos** Navigationseigenschaft hinzufügen

### 2. Service-Schicht

- **Neuer Service (z. B. `TodoService`)** mit Methoden:
  - `CreateTodoAsync(Guid aufgabeId, string beschreibung)`
  - `MarkTodoAsCompletedAsync(Guid todoId)`
  - `DeleteTodoAsync(Guid todoId)`
  - `GetOpenTodosAsync(Guid aufgabeId)`
  - `GetTodoCountAsync(Guid aufgabeId)`

- **Erweiterung eines bestehenden Services** (z. B. AufgabeService oder AufgabenAbschlussService):
  - Validierungsmethode `CanCompleteTaskAsync(Guid aufgabeId)` die prüft, ob offene To-Dos existieren
  - Diese wird vom Command für "Beenden" aufgerufen, bevor die Statusänderung committed wird

### 3. Benutzeroberfläche

- **Neuer UserControl `TodoListView.xaml`** oder Integration in `TaskDetailView.xaml`:
  - WPF-Komponente mit ItemsControl oder ListBox für die To-Do-Liste
  - TextBox für neue To-Dos
  - CheckBox für Erledigt-Status
  - Button zum Löschen

- **Ribbon-Erweiterung `TaskDetailView.xaml`:**
  - Badge/Label mit Binding zu `OffeneTodoCount`
  - Visuelle Hervorhebung bei offenen To-Dos (z. B. rote Markierung)

- **ViewModel `TodoViewModel`:**
  - Properties: `Id`, `Beschreibung`, `IstErledigt`, `ErstellungsDatum`
  - Commands für Löschen und Statusänderung
  - Kann durch `INotifyPropertyChanged` aktualisiert werden

- **Erweiterung `TaskDetailViewModel`:**
  - Initialisierung von `Todos` beim Laden der Aufgabe
  - Reaktive Aktualisierung des `OffeneTodoCount` wenn sich To-Do-Status ändert
  - Fehlerbehandlung bei Validierung im `AufgabeBeendenCommand`

### 4. Workflow

1. **Aufgabe öffnen** → To-Do-Liste wird geladen und angezeigt
2. **To-Do erstellen** → TodoService wird aufgerufen, To-Do wird der Liste hinzugefügt
3. **To-Do abhaken** → TodoService markiert als `ErledaltAm = DateTime.UtcNow`
4. **Aufgabe beenden** → Validierungsprüfung lädt `GetOpenTodosAsync()`, falls Ergebnis != 0, UI zeigt Fehler und verhindert Status-Übergang
5. **Alle To-Dos erledigt** → Badge zeigt "0", Beenden ist erlaubt

## Konfiguration

Für diese Anforderung ist keine spezifische Konfiguration erforderlich. Verhaltensaspekte sind implizit in der Geschäftslogik codiert (z. B. "Aufgabe kann nicht beendet werden, wenn To-Dos offen sind").

Falls zukünftig gewünscht, könnte eine Option "To-Do-Validierung erzwingen: ja/nein" auf Projekt- oder Anwendungsebene hinzugefügt werden, ist aber nicht Teil dieser Anforderung.

## Offene Fragen

1. **Priorität von To-Dos:** Sollen To-Do-Elemente eine Priorität oder Sortierfolge haben, oder ist eine einfache Liste nach Erstellungsdatum ausreichend?

2. **To-Do-Kategorien/Beschriftungen:** Sollen To-Dos kategorisierbar sein (z. B. mit Tags wie "Design", "Test", "Doku"), oder nur einfache Text-To-Dos?

3. **Löschverhalten:** Wenn ein beendetes To-Do gelöscht wird, soll das nur über die UI möglich sein oder auch über die API, wenn die Aufgabe bereits beendet ist? (D. h.: Können Benutzer To-Dos aus beendeten Aufgaben nachträglich entfernen?)

4. **To-Do-Details:** Sollen To-Dos nur eine Beschreibung haben, oder auch weitere Felder wie Fälligkeitsdatum, zugeordnete Person, oder Anhänge?

5. **UI-Position:** Sollen To-Dos als separater Tab/Inhaltsbereich oder inline im `Info`-Tab angezeigt werden?

6. **Fehlermeldung bei Beendungsversuch:** Sollte die Fehlermeldung die konkreten offenen To-Dos aufzählen, oder nur die Anzahl nennen?

7. **Ribbon-Badge-Design:** Sollte das Badge nur die Zahl zeigen (z. B. "3") oder aussagekräftiger sein (z. B. "⚠ 3 Todos ausstehend")?

8. **Zu-Do-Verlauf:** Sollen erledigte To-Dos archiviert oder dauerhaft gelöscht werden? Soll der Verlauf erledigt gemachter To-Dos einsehbar sein?

9. **Massenverwaltung:** Sollen alle To-Dos auf einmal abhakbar sein (z. B. "Alle abhaken"-Button), oder nur einzeln?

10. **Export/Reporting:** Sollen To-Do-Listen exportierbar sein (z. B. als CSV/PDF), oder ist die In-App-Anzeige ausreichend?

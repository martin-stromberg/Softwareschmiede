# Anforderung — Separate Aufgabendetailansicht

## Fachliche Zusammenfassung

Die Aufgabendetailansicht soll aus der eingebetteten Inline-Position in der `ProjectDetailView` in eine vollständige, fensterumfassende Ansicht ausgelagert werden. Klickt der Anwender in der Projektdetailansicht auf eine Aufgabe aus der Aufgabenliste, navigiert die Anwendung zur `TaskDetailView` — die `ProjectDetailView` wird nicht mehr angezeigt. Mit einem „Zurück"-Button (oder „Abbrechen" bei Neuanlage) navigiert der Benutzer zurück zur Projektdetailansicht. Diese Navigation soll auch beim Erstellen neuer Aufgaben gelten: Die `TaskDetailView` öffnet sich mit leerem Bearbeitungsformular; nach dem Speichern wird die neue Aufgabe mit Status „Neu" persistiert und der Benutzer kehrt zur Projektdetailansicht zurück.

## Betroffene Klassen und Komponenten

### Datenmodellklassen
- `Aufgabe` (bestehend; Status-Property für Statusübergänge relevant)
- `Projekt` (bestehend; Zusammenhang mit Aufgaben bleibt erhalten)

### Logikklassen / Services
- `AufgabeService` — erweitern um Methoden zur Neuanlage und zum Speichern einzelner Aufgaben (falls nicht vorhanden)
- `ProjektService` — bestehende Funktionalität bleibt erhalten

### ViewModels
- `ProjectDetailViewModel` — Navigation zu Aufgabe und von Aufgabe zurück implementieren; `SelectedTaskViewModel` wird nicht mehr als Inline-Binding verwendet
- `TaskDetailViewModel` — bestehend; wird weiterhin für Aufgabenbearbeitung und Anzeige genutzt, aber in separater View
- Neues ViewModel (optional): `NavigationViewModel` oder `RootViewModel` zur zentralen Steuerung des Navigationszustands (welche View aktuell sichtbar ist)

### Interfaces
- `INavigationService` oder ähnlich — falls nicht vorhanden, zur Abstrahlung der Navigation zwischen Views (z.B. zu Aufgabe, zurück zu Projekt)

### UI-Komponenten / Views
- `ProjectDetailView.xaml` / `ProjectDetailView.xaml.cs` — Entfernen der Inline-`TaskDetailView` Bindung; Event-Handler für Aufgaben-Klick zum Auslösen der Navigation
- `TaskDetailView.xaml` / `TaskDetailView.xaml.cs` — bleibt weitgehend unverändert, wird aber in separater Ansicht gerendert
- Evtl. `MainWindow.xaml` oder Container-View — anpassen, um zwischen `ProjectDetailView` und `TaskDetailView` zu wechseln (Content-Switching)

### Tests
- E2E-Tests für Aufgaben-Klick in Projektdetail → Navigation zu Aufgabendetail
- E2E-Tests für Zurück-Navigation von Aufgabendetail zu Projektdetail
- E2E-Tests für Neuanlage einer Aufgabe → Aufgabendetail öffnet sich → nach Speichern zurück zu Projektdetail mit Status „Neu"
- Unit-Tests für Navigation-Logik (falls neues ViewModel)

## Implementierungsansatz

### Navigation und View-Switching
1. **Navigationsmechanismus:** Ein zentraler `NavigationService` oder ein Root-ViewModel verwaltet, welche View aktuell sichtbar ist. Dies kann über ein `CurrentView` Property oder einen `State` implementiert werden.
2. **ProjectDetailViewModel erweitern:**
   - Neues Event oder Command: `AufgabeAusgewaehlter()` — wird ausgelöst, wenn Benutzer in der Aufgabenliste auf eine Aufgabe klickt
   - Property: `SelectedTaskId` oder `SelectedTaskViewModel` — speichert die aktuell gewählte Aufgabe (für Serialisierung der Navigation)
   - Methode: Navigation-Callback (z.B. `NavigateToTaskAsync(taskId)`) — wird nach Auswahl aufgerufen und signalisiert dem Navigation-Service, dass zur Aufgabendetail-View gewechselt werden soll
3. **TaskDetailViewModel erweitern:**
   - Property oder Command: `ZurueckCallback` oder `NavigateBackCommand` — wird ausgelöst, wenn Benutzer den „Zurück"- oder „Abbrechen"-Button klickt
   - Bei Neuanlage: Flag `IsNewTask` oder Property `IsNeuanlage` — bestimmt, ob Button als „Abbrechen" oder „Zurück" beschriftet ist
   - Nach erfolgreichem Speichern bei Neuanlage: `ZurueckCallback()` aufrufen, um zur Projektdetailansicht zu navigieren
4. **ProjectDetailView.xaml anpassen:**
   - Binding für `TaskDetailView` entfernen (aktuell `Visibility="{Binding SelectedTaskViewModel, Converter=...}"`)
   - Event-Handler für Aufgabenlisten-Klick (z.B. `ListBox.SelectionChanged` oder `ListBox.MouseDoubleClick`) → `ProjectDetailViewModel.AufgabeAusgewaehlterCommand` aufrufen
5. **Container (z.B. MainWindow oder neue RootView):**
   - Ein `ContentControl` oder `Grid` mit Content-Switching zwischen `ProjectDetailView` und `TaskDetailView`
   - Binding zu Navigation-State: `CurrentView` oder ähnlich
   - ValueConverter (z.B. `EnumToViewConverter` oder `TypeToViewConverter`) zur Umwandlung des Navigation-States in die richtige View-Komponente

### Datenbindung und Events
- `ProjectDetailViewModel` hat Property `NavigationState` (z.B. Enum: `Projekt`, `Aufgabe`)
- Bei Klick auf Aufgabe: `NavigationState = NavigationState.Aufgabe` → Parent-Container wechselt die View
- Bei Klick auf „Zurück" in `TaskDetailView`: `NavigationState = NavigationState.Projekt` → Parent-Container wechselt zurück
- Abhängigkeiten: Navigation-Service oder Parent-ViewModel wird injiziert und über `INotifyPropertyChanged` oder Events benachrichtigt

### Neuanlage von Aufgaben
- Button oder Kommando in `ProjectDetailView` (z.B. in Ribbon-Menü oder über die Aufgaben-Kachel): „Neue Aufgabe"
- Aufruf: `ProjectDetailViewModel.NeueAufgabeCommand` → erstellt eine neue `Aufgabe`-Instanz mit leeren Werten
- Setzt `TaskDetailViewModel` mit der neuen Aufgabe und Flag `IsNeuanlage = true`
- `TaskDetailView` zeigt „Abbrechen"-Button statt „Zurück"
- Nach Speichern: neue Aufgabe wird persistiert (Status: „Neu"), und die View navigiert zurück zur Projektdetail

## Konfiguration

Keine Konfiguration erforderlich. Navigation ist ein fest implementiertes Systemverhalten.

## Offene Fragen

1. **Navigation-Service-Architektur:** Soll ein zentraler `INavigationService` implementiert werden, oder wird die Navigation über Callbacks/Events zwischen ViewModels geregelt?
2. **View-Container:** Wo werden `ProjectDetailView` und `TaskDetailView` gehostet — direkt in `MainWindow.xaml`, oder in einer neuen Container-View (z.B. `AppContainerView`)?
3. **State-Persistierung:** Soll der Navigationszustand (z.B. „zuletzt sichtbar: Aufgabe mit ID xyz") über Anwendungs-Sessions hinweg erhalten bleiben, oder wird bei App-Start immer zur Projektliste navigiert?
4. **Tastatur-Navigation:** Soll die Escape-Taste die Aufgabendetailansicht schließen und zur Projektdetail zurückkehren (wie in Dialogen üblich)?
5. **Animation/Übergang:** Sollen View-Wechsel animiert sein (z.B. Slide, Fade), oder einfache sofortige Umschaltung?
6. **Zurück-Navigation bei Fehler:** Wenn das Speichern einer neuen Aufgabe fehlschlägt, bleibt die View offen und zeigt Fehler, oder wird sofort zur Projektdetail zurück navigiert?

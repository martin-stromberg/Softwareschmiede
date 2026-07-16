# Bestandsaufnahme: Programmupdate-Fehler beheben

Dieser Bericht dokumentiert die Ist-Situation der am Programmupdate-Feature beteiligten Klassen und Komponenten. Der Fehler betrifft die Anzeige des `UpdateProgressDialog`, der beim Laden wegen eines WPF-Databinding-Fehlers mit `InvalidOperationException` scheitert.

## Zusammenfassung

**Betroffene Bereiche:**
- **ViewModel:** `UpdateProgressViewModel` mit 7 Properties (alle mit `private set`, davon `Percent` als kritisch)
- **Service:** `WpfUpdateProgressDialogService` zeigt Dialog via `Show()`-Methode an (Zeile 26 — dort tritt der Fehler auf)
- **UI:** `UpdateProgressDialog.xaml` mit OneWay-Binding auf `Percent` (Zeile 35)
- **Tests:** 4 bestehende Unit-Tests für ViewModel; **keine UI-/Dialog-Tests vorhanden**

**Problemdiagnose:**
- `UpdateProgressViewModel.Percent` ist als `public double { get; private set; }` definiert
- WPF-Databinding-Engine prüft beim Laden die Setter-Sichtbarkeit, auch bei OneWay-Bindungen
- Fehlgeschlagene Setter-Prüfung führt zu `InvalidOperationException` beim Anzeigen des Dialogs

**Vorhanden:**
- Properties sind korrekt implementiert (verwenden `SetProperty()` aus `ViewModelBase`)
- Methoden `Apply()`, `SetError()`, `MarkUpdaterStarting()` setzen Properties intern korrekt
- Tests verifizieren interne Zustandsänderungen
- Service nutzt `Dispatcher.Invoke()` für UI-Thread-Sicherheit
- Dialog hat `OnClosing()`-Guard gegen Schließen bei `CanClose = false`

**Nicht vorhanden / offene Punkte:**
- Keine automatisierten Tests für Dialog-Anzeige selbst
- Keine Tests für `WpfUpdateProgressDialogService`
- `Percent`-Setter ist nicht öffentlich zugänglich → WPF-Fehler
- Keine Dokumentation/Annotation (z. B. `[EditorBrowsable]`), die anzeigt, dass Setter nur intern genutzt werden sollte

## Details

- [Datenmodelle: Properties und Felder](inventory/models.md)
- [Logik und Services: Methoden und Dialog-Anzeige](inventory/logic.md)
- [Tests: Bestehende Unit-Tests](inventory/tests.md)

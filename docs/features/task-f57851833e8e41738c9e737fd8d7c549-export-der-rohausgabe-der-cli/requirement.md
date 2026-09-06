### Fachliche Zusammenfassung
Die Aufgabendetailfunktion wird um einen Export der bisher angefallenen CLI-Rohdaten erweitert. Der Anwender kann den Export aktiv auslösen und wählt den endgültigen Dateinamen über einen Speichern-Dialog; das Dateiformat ist `*.raw`. Exportiert werden die in der Aufgabe bereits protokollierten CLI-Ausgabezeilen (fachlich: `ProtokollTyp.CliOutput`) in ihrer bisherigen Reihenfolge. Das Feature ergänzt die bestehende Protokoll-/CLI-Funktionalität, ohne den Aufgabenstatus oder den CLI-Lifecycle zu verändern.

### Betroffene Klassen und Komponenten
- **Datenmodellklassen**
  - Keine neue Datenmodellklasse zwingend erforderlich (bestehende `Protokolleintrag`-Datenbasis wird wiederverwendet).
- **Logikklassen / Services**
  - `ProtokollService` (voraussichtliche Erweiterung um einen gezielten Abrufpfad für CLI-Rohdaten einer Aufgabe, falls nicht ausschließlich aus bereits geladenen Einträgen exportiert wird).
  - Optional neue Export-Logik als dedizierter Service (Annahme), damit Dateischreiben nicht direkt in `TaskDetailViewModel` erfolgt.
- **Interfaces**
  - `IDialogService` (Erweiterung um Speichern-Dialog für Zielpfad/-datei).
- **Enums**
  - Kein neues Enum erforderlich; Nutzung von `ProtokollTyp.CliOutput`.
- **UI-Komponenten / Controller**
  - `TaskDetailView.xaml` (neue Benutzeraktion/Schaltfläche für Rohdaten-Export, voraussichtlich in der CLI- oder Aufgaben-bezogenen Ribbon-Gruppe).
  - `TaskDetailViewModel` (neuer Command inkl. CanExecute-Logik und Orchestrierung des Exports).
  - `WpfDialogService` (konkrete WPF-Implementierung des Save-Dialogs).
- **Tests**
  - `TaskDetailViewModelTests` (Command-Verfügbarkeit, Filterung auf `CliOutput`, Dialog-Abbruch, Erfolgs-/Fehlerpfade).
  - UI-nahe Tests wie `TaskDetailViewTests`/E2E-Tests für Sichtbarkeit und Auslösung der Exportaktion.

### Implementierungsansatz
- Relevanter Erweiterungspunkt ist die bestehende Aufgaben-Detailorchestrierung in `TaskDetailViewModel` mit Command-Bindings nach `TaskDetailView.xaml`.
- Die Exportaktion wird als neuer Command in `TaskDetailViewModel` ergänzt und über das bestehende MVVM-Binding in der View verfügbar gemacht.
- Für den Dateinamen/-pfad wird über `IDialogService` ein Save-Dialog abstrahiert; `WpfDialogService` implementiert die WPF-spezifische `SaveFileDialog`-Interaktion.
- Die zu exportierenden Daten werden aus den CLI-Protokolldaten der Aufgabe gebildet (Filter auf `ProtokollTyp.CliOutput`, chronologische Reihenfolge beibehalten).
- Annahme: Der Export enthält ausschließlich Rohausgabezeilen (`Inhalt`) und keine zusätzlichen Metadaten-/Formatierungsanteile.
- Abhängigkeiten bestehen zur bestehenden Protokollpersistenz (`ProtokollService`, `Protokolleintrag`, `ProtokollTyp`) sowie zur Task-Detail-UI (`TaskDetailViewModel`, `TaskDetailView.xaml`).

### Konfiguration
Für die Anforderung ist keine zusätzliche Laufzeitkonfiguration zwingend erforderlich. Das Verhalten ist benutzerinitiiert (on-demand Export pro Aufgabe), der Dateiname/-pfad wird pro Exportvorgang im Dialog festgelegt.

### Offene Fragen
- Soll der Export nur `ProtokollTyp.CliOutput` enthalten oder zusätzlich systemnahe CLI-bezogene Einträge (z. B. `ProtokollTyp.RateLimit`/`ProtokollTyp.SystemMeldung`)?
- Soll der Export exakt den aktuellen Persistenzstand aus der Datenbank enthalten oder nur die im UI derzeit geladenen `Protokolleintraege`?
- Muss der Export auch während laufender CLI-Sitzung konsistent unterstützt werden (inkl. gleichzeitiger neuer Ausgaben)?
- Welche Zeichencodierung ist verbindlich (z. B. UTF-8 ohne BOM), damit `*.raw`-Dateien erwartungskonform weiterverarbeitet werden können?
- Soll für den Dateinamen ein Vorschlagsname (z. B. auf Basis von Aufgabe/Timestamp) vorbelegt werden?

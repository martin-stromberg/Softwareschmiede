# ProjectDetailView.xaml.cs

Datei: `src/Softwareschmiede.App/Views/ProjectDetailView.xaml.cs`

Code-behind der `ProjectDetailView` (`UserControl`).

## Methoden

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| Konstruktor | public | `InitializeComponent()`, registriert `Loaded`-Handler für Fokus |
| `AufgabeDoubleClick(object, MouseButtonEventArgs)` | private | Ruft `vm.AufgabeOeffnenCommand.Execute(aufgabe.Id)` synchron auf — kein Fire-and-Forget |
| `IssueDoubleClick(object, MouseButtonEventArgs)` | private (Zeile 28–35) | Ruft `_ = vm.AufgabeAusIssueErstellenCommand.ExecuteAsync(issue);` (Zeile 33) — **Fire-and-Forget ungeschützt, kein try-catch (F18)**. Kein `ILogger`-Feld in der Klasse vorhanden. |

## Kritische Stellen (Bezug zur Anforderung)

- **F18:** Zeile 33 — `_ = vm.AufgabeAusIssueErstellenCommand.ExecuteAsync(issue);` in `IssueDoubleClick` ohne try-catch und ohne Logger. `AufgabeAusIssueErstellenCommand` ist ein `AsyncRelayCommand<Issue>`, dessen Zielmethode (`ProjectDetailViewModel.AufgabeAusIssueErstellenAsync`) bereits eigenes try-catch besitzt — das Risiko besteht in Exceptions außerhalb dieses Bereichs (z. B. im Command-Ausführungsmechanismus selbst).

Kein `ILogger`-Feld vorhanden — bei Umsetzung von F18 müsste eine Logger-Instanz zusätzlich injiziert werden (Code-behind wird derzeit nicht per DI konstruiert, sondern von WPF/XAML instanziiert).

# Anforderung: Programmupdate-Fehler beheben

## Fachliche Zusammenfassung

Das Programmupdate-Feature zeigt beim Betätigen des Update-Buttons einen Fortschrittsdialog an. Der Dialog kann nicht angezeigt werden, da die WPF-Databinding-Engine versucht, auf die schreibgeschützte Eigenschaft `Percent` des `UpdateProgressViewModel` zu schreiben oder zuzugreifen, und dies mit einer `InvalidOperationException` fehlschlägt. Zur Behebung muss die Eigenschaft `Percent` von `private set` auf einen öffentlich beschreibbaren Setter angepasst werden, sodass die Binding-Engine ungehindert auf den Setter zugreifen kann.

## Betroffene Klassen und Komponenten

### ViewModels
- `Softwareschmiede.App.ViewModels.UpdateProgressViewModel`
  - Eigenschaft `Percent` (double): Schreibgeschützt via `private set`; muss öffentlich schreibbar werden
  - Methoden `Apply(UpdatePreparationProgress)` und `SetError(string)` setzen `Percent` bereits durch Property-Setter
  - Der Setter muss intern (d. h. von der Klasse selbst) weiterhin steuerbar bleiben; externe Änderungen sollten vermieden werden

### Services
- `Softwareschmiede.App.Services.WpfUpdateProgressDialogService`
  - Zeigt den Dialog mit `dialog.Show()` (Zeile 26)
  - Fehler tritt bei der Dialog-Anzeige auf; der Service selbst braucht keine Änderungen

### UI-Komponenten
- `Softwareschmiede.App.Views.UpdateProgressDialog.xaml`
  - ProgressBar mit Bindung `Value="{Binding Percent}"` (Zeile 35)
  - Bindung ist OneWay (Standard für ProgressBar.Value); dennoch prüft das Binding-System den Setter

### Tests
- Bestehende Unit-Tests für `UpdateProgressViewModel` müssen verifizieren, dass `Percent` weiterhin nur durch interne Methoden (`Apply`, `SetError`) geändert wird und keine externen Schreibvorgänge erfolgen

## Implementierungsansatz

### Technische Beschreibung
Das WPF-Databinding-System überprüft den Setter einer Eigenschaft, selbst wenn die Bindung als OneWay konfiguriert ist. Der `private set` auf `Percent` schlägt diese Überprüfung fehl. Die Lösung ist, den Setter öffentlich zu machen, aber weiterhin sicherzustellen, dass nur interne Methoden die Eigenschaft setzen.

### Mögliche Lösungsvarianten

**Variante 1 (bevorzugt):** Property mit öffentlichem Setter und interner Steuerung  
Ändern Sie die `Percent`-Eigenschaft von:
```csharp
public double Percent
{
    get => _percent;
    private set => SetProperty(ref _percent, value);
}
```
zu:
```csharp
public double Percent
{
    get => _percent;
    set => SetProperty(ref _percent, value);
}
```
Diese Variante ist die einfachste und ermöglicht der Binding-Engine den Zugriff. Wenn Sicherheitsbedenken bestehen, dass externe Code die Eigenschaft unsachgemäß ändern könnte, muss dies durch Code-Review oder Design-Pattern (z. B. `[EditorBrowsable(EditorBrowsableState.Never)]`) adressiert werden.

**Variante 2:** Nur Getter öffentlich machen, privaten Setter für interne Verwendung  
Dies erfordert einen zusätzlichen internen Setter, ist aber aufwendiger.

### Betroffene Methoden innerhalb der Klasse
- `Apply(UpdatePreparationProgress progress)`: Setzt `Percent` (Zeile 89)
- `SetError(string message)`: Setzt `IsIndeterminate = false` (Zeile 99); `Percent` wird nicht direkt gesetzt, aber auf 0 durch `private set` initialisiert

### Abhängigkeiten
- Keine zusätzlichen Abhängigkeiten nötig
- Das `ViewModelBase.SetProperty<T>(ref T, T, ...)`-Pattern bleibt unverändert
- Keine Breaking Changes für existierende Code, da es sich um eine interne Fehlerkorrektur handelt

## Konfiguration

Keine Konfiguration erforderlich. Dies ist eine reine Bugfix.

## Offene Fragen

1. **Sicherheitsbedenken:** Sollte die `Percent`-Eigenschaft durch ein Attribut (z. B. `[EditorBrowsable]`) oder Dokumentation markiert werden, um zu signalisieren, dass sie nur intern gesetzt werden sollte?

2. **Testabdeckung:** Gibt es bestehende Unit-Tests, die den Fortschrittsdialog oder das `UpdateProgressViewModel` testen? Falls nicht, sollte ein Regressionstest für die korrekte Anzeige des Dialogs mit Fortschrittsanzeige hinzugefügt werden.

3. **Andere Read-Only-Eigenschaften:** Sollten alle anderen `private set`-Eigenschaften im `UpdateProgressViewModel` (z. B. `PhaseText`, `Message`, `IsIndeterminate`, `HasError`, `CanClose`, `CanCancel`) einer ähnlichen Überprüfung unterzogen werden, um zukünftige Binding-Fehler auszuschließen? Dies ist wahrscheinlich nicht nötig, da sie nicht direkt in kritischen Binding-Szenarien verwendet werden, aber es ist eine Überlegung wert.

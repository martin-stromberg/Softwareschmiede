# Bestandsaufnahme: Converter und UI-Utilities

## Converter (für bedingte Sichtbarkeit)
Datei: `src/Softwareschmiede.App/Converters/AppConverters.cs`

### `BoolToVisibilityConverter`
Konvertiert `bool` zu `Visibility` (true = Visible, false = Collapsed).

- **Verwendung:** Kann für `Visibility="{Binding HasScmPlugins, Converter={StaticResource BoolToVisibilityConverter}}"` verwendet werden
- **Implementiert:** `IValueConverter`
- **Convert:** `value is true ? Visibility.Visible : Visibility.Collapsed`
- **ConvertBack:** `value is Visibility.Visible`

### `InverseBoolToVisibilityConverter`
Konvertiert inversen `bool` zu `Visibility` (false = Visible, true = Collapsed).

- **Verwendung:** Inverse Logik für Hilfe-Panel (zeige wenn HasScmPlugins = false)
- **Convert:** `value is false ? Visibility.Visible : Visibility.Collapsed`
- **ConvertBack:** `value is not Visibility.Visible`

### Weitere verfügbare Converter
- `EnumToBoolConverter` — Konvertiert Enum-Wert für RadioButton-Bindungen
- `BoolToWidthConverter` — Konvertiert bool zu Breite (für Expand/Collapse)
- `NullOrEmptyToVisibilityConverter` — Zeigt Collapsed für null/leere Werte

## ViewModelBase
Datei: `src/Softwareschmiede.App/ViewModels/ViewModelBase.cs`

### Basisklasse für alle ViewModels
Implementiert `INotifyPropertyChanged` mit Hilfsmethoden:

| Methode | Beschreibung |
|---------|-------------|
| `OnPropertyChanged(string? propertyName)` | Löst PropertyChangedEvent aus |
| `SetProperty<T>(ref T field, T value, string? propertyName)` | Setzt Feld und triggert PropertyChanged wenn geändert |
| `SetProperty<T>(ref T field, T value, Action onChanged, string? propertyName)` | Setzt Feld und führt Action aus wenn geändert |
| `SetFehler(ref string? fehlerMeldungField, string propertyName, Exception ex)` | Setzt Fehlertext und triggert PropertyChanged |

## RelayCommand-Klassen
Datei: `src/Softwareschmiede.App/ViewModels/ViewModelBase.cs`

### `RelayCommand`
Einfacher Command ohne Parameter.

- **Konstruktor:** `RelayCommand(Action execute, Func<bool>? canExecute = null)`
- **CanExecute:** Wertet `_canExecute?.Invoke()` aus
- **Execute:** Ruft `_execute()` auf
- **Event:** `CanExecuteChanged` wird über `CommandManager.RequerySuggested` verwaltet
- **Refresh:** `RelayCommand.Refresh()` erzwingt Neuauswertung von CanExecute

**Verwendung in RepositoryAssignViewModel:**
```csharp
BestaetigenCommand = new RelayCommand(
    () => CloseRequested?.Invoke(this, true),
    () => _selectedRepository != null);
```

### `RelayCommand<T>`
Relay-Command mit Parameter vom Typ `T`.

- **Konstruktor:** `RelayCommand<T>(Action<T?> execute, Func<T?, bool>? canExecute = null)`
- **Execute:** `_execute((T?)parameter)`

### `AsyncRelayCommand`
Asynchroner Command ohne Parameter mit Ausführungs-Status.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `IsExecuting` | `bool` | Gibt an, ob Command gerade läuft |
| `OnError` | `Action<Exception>?` | Optional: Fehler-Callback |

| Methode | Beschreibung |
|---------|-------------|
| `Execute(object? parameter)` | Async Execute |
| `ExecuteAsync(object? parameter)` | Gibt Task zurück für Tests |
| `Cancel()` | Bricht laufende Ausführung ab |

- **CanExecute:** `_isExecuting == 0 && (_canExecute?.Invoke() ?? true)`
- **Fehlerbehandlung:** Unbehandelte Exceptions werden geloggt oder via `OnError`-Callback behandelt

## XAML-Ressourcen-Registrierung
Die Converter sind in `RepositoryAssignDialog.xaml` via `StaticResource` erreichbar, müssen aber in einer Ressourcen-Datei oder App.xaml registriert sein (z.B.):
```xaml
<converters:BoolToVisibilityConverter x:Key="BoolToVisibilityConverter" />
<converters:InverseBoolToVisibilityConverter x:Key="InverseBoolToVisibilityConverter" />
```

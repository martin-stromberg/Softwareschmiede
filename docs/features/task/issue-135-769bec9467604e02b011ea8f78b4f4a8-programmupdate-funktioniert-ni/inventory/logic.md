# Logik und Services

## `UpdateProgressViewModel`

Datei: `src/Softwareschmiede.App/ViewModels/UpdateProgressViewModel.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `.ctor(Action?)` | public | Konstruktor; akzeptiert optional eine `cancelAction` (Callback für Abbruch) |
| `Apply(UpdatePreparationProgress)` | public | Übernimmt eine Fortschrittsmeldung: aktualisiert `PhaseText` basierend auf Phase-Enum, setzt `Message`, `IsIndeterminate` und `Percent` |
| `SetError(string)` | public | Zeigt einen Fehler an: setzt `HasError = true`, `CanClose = true`, `CanCancel = false`, aktualisiert `Message`, `IsIndeterminate = false` |
| `MarkUpdaterStarting()` | public | Bereitet Dialog auf externen Updater-Start vor: setzt `CanCancel = false`, `CanClose = true`, `Message` und `IsIndeterminate = true` |
| `RequestCancel()` | private | Wird durch `CancelCommand` aufgerufen; prüft `CanCancel`, deaktiviert Abbruch, aktualisiert `Message`, ruft `_cancelAction?.Invoke()` auf |

**Abhängigkeiten:**
- Verwendet `UpdatePreparationProgress` und `UpdatePreparationPhase` aus `Softwareschmiede.Application.Services.Updates`
- Implementiert `ICommand` über `RelayCommand` für `CancelCommand`

---

## `WpfUpdateProgressDialogService`

Datei: `src/Softwareschmiede.App/Services/WpfUpdateProgressDialogService.cs`

Implementiert `IUpdateProgressDialogService`.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Show(UpdateProgressViewModel)` | public | Zeigt den Fortschrittsdialog an; nutzt Dispatcher.Invoke() für UI-Thread-Sicherheit; erstellt neue `UpdateProgressDialog`, setzt DataContext und Owner, speichert Dialog in Dictionary, registriert Closed-Event-Handler |
| `Close(UpdateProgressViewModel)` | public | Schließt einen zuvor geöffneten Dialog; nutzt Dispatcher.Invoke() für UI-Thread-Sicherheit |

**Interne Struktur:**
- Dictionary `_dialogs` zum Tracking geöffneter Dialoge (ViewModel → Dialog-Instanz)
- Event-Handler auf `dialog.Closed` zum Entfernen aus Dictionary

**Fehlerherkunft:**
- Zeile 26: `dialog.Show()` schlägt fehl, da `UpdateProgressViewModel.Percent` einen `private set` hat und die WPF-Databinding-Engine beim Erstellen des Bindings den Setter nicht finden kann → `InvalidOperationException`

---

## `IUpdateProgressDialogService`

Datei: `src/Softwareschmiede.App/Services/IUpdateProgressDialogService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `Show()` | `UpdateProgressViewModel viewModel` | `void` | Öffnet den Fortschrittsdialog nicht blockierend |
| `Close()` | `UpdateProgressViewModel viewModel` | `void` | Schließt einen zuvor geöffneten Dialog |

# Logic/Services

## `AutonomAufgabeStartService`

Datei: `src/Softwareschmiede.App/Services/AutonomAufgabeStartService.cs`

**Verantwortung:** Orchestriert den Ablauf "Autonome Aufgabe initialisieren" — öffnet Initialisierungsdialog, lädt aktualisierte Aufgabe, zeigt Detail-Ansicht an.

### Abhängigkeiten

| Abhängigkeit | Typ | Zweck |
|--------------|-----|-------|
| `_serviceProvider` | `IServiceProvider` | Erzeugt ViewModels per DI |
| `_dialogService` | `IDialogService` | Öffnet Dialoge (Initialisierung, Detail-Ansicht) |
| `_aufgabeService` | `AufgabeService` | Lädt aktuelle Aufgabe |
| `_logger` | `ILogger<AutonomAufgabeStartService>` | Logging |

### Methoden

| Methode | Sichtbarkeit | Beschreibung |
|---------|-------------|-------------|
| `StarteAsync(Aufgabe aufgabe, CancellationToken ct)` | Public | **Hauptmethode:** Zeigt Initialisierungsdialog an, lädt bei Erfolg die aktualisierte Aufgabe, erstellt `AutonomAufgabeDetailViewModel` und ruft `_dialogService.ShowAutonomAufgabeDetailAsync()` auf. Gibt `AutonomAufgabeStartResult?` zurück (null bei Abbruch). |

### Ablauf in `StarteAsync()` (Zeilen 31–73)

1. Lädt aktuellsten Stand der Aufgabe
2. Erzeugt `AutonomAufgabeInitialisierungsDialogViewModel` per DI
3. Initialisiert DialogVM und lädt Daten
4. Zeigt Initialisierungsdialog via `_dialogService.ShowAutonomAufgabeInitialisierungsDialogAsync()` — gibt `AutonomAufgabeKonfiguration?` zurück
5. Falls Abbruch: Gibt `null` zurück
6. Ansonsten:
   - Lädt aktuellsten Stand der Aufgabe nochmals
   - Erzeugt `AutonomAufgabeDetailViewModel` mit geladenem Stand und Konfiguration
   - Ruft `_dialogService.ShowAutonomAufgabeDetailAsync(detailVm, ct)` auf — **zeigt aktuell separaten Dialog**
   - Gibt `AutonomAufgabeStartResult` zurück

### Rückgabewert: `AutonomAufgabeStartResult`

(Klassenname nicht gesehen, aber Struktur aus Zeile 60/69:)
- `AktualisierteAufgabe`: Die aktualisierte Aufgabe
- `FehlerMeldung`: Fehlertext (null bei Erfolg)

**Kritisch für die Anforderung:** Zeile 59 ruft `_dialogService.ShowAutonomAufgabeDetailAsync()` auf, was einen separaten Dialog öffnet. Nach der Integration muss dieser Aufruf durch einen Mechanismus ersetzt werden, der `TaskDetailViewModel` mitteilt, die neue Automatisierung-Ansicht anzuzeigen.

---

## `WpfDialogService`

Datei: `src/Softwareschmiede.App/Services/WpfDialogService.cs`

Implementierung von `IDialogService` für WPF-basierte Dialoge.

### Methode: `ShowAutonomAufgabeDetailAsync`

| Parameter | Typ | Zweck |
|-----------|-----|-------|
| `viewModel` | `AutonomAufgabeDetailViewModel` | Das ViewModel für die Detail-Ansicht |
| `ct` | `CancellationToken` | Abbruchtoken |

| Rückgabe | Beschreibung |
|----------|-------------|
| `Task` | Asynchrone Operation (modal) |

### Implementierung (Zeilen 140–148)

```csharp
public Task ShowAutonomAufgabeDetailAsync(
    AutonomAufgabeDetailViewModel viewModel,
    CancellationToken ct = default)
{
    ct.ThrowIfCancellationRequested();
    return ShowDialogAsync(
        () => new AutonomAufgabeDetailDialog(viewModel),
        () => (object?)null);
}
```

**Mechanik:**
1. Validiert CancellationToken
2. Ruft `ShowDialogAsync()` auf mit:
   - `dialogFactory`: Erzeugt `AutonomAufgabeDetailDialog` mit ViewModel
   - `resultSelector`: Gibt `null` zurück (Modal-Dialog mit `ShowDialog()`, kein Rückgabewert)
3. `ShowDialogAsync()` rendert Dialog modal auf Dispatcher-Thread, setzt Owner auf MainWindow, ruft `ShowDialog()` auf

**Status nach Integration:** Diese Methode kann deprecated sein oder als Fallback erhalten bleiben (für spätere Szenarien).

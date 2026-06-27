# Logik und UI-Kontrollkomponenten

## `StatusIndicatorControl`
Datei: `src/Softwareschmiede.App/Controls/StatusIndicatorControl.xaml.cs`

Visuelle Anzeige des Aufgaben-Status mit farbiger Statusmarkierung.

### Dependency Properties

| Property | Typ | Beschreibung | Initialisierung |
|---|---|---|---|
| `StatusProperty` | `DependencyProperty` | Registrierte DP für `Status` | `AufgabeStatus.Neu` (Default) |
| `StatusTextProperty` | `DependencyProperty` | Registrierte DP für `StatusText` | `string.Empty` (Default) |
| `StatusColorProperty` | `DependencyProperty` | Registrierte DP für `StatusColor` | `Brushes.Gray` (Default) |

### Public Properties

| Eigenschaft | Typ | Get/Set | Beschreibung |
|---|---|---|---|
| `Status` | `AufgabeStatus` | Get/Set | Aufgaben-Status; setzt `StatusProperty` |
| `StatusText` | `string` | Get/Private Set | Anzeigetext für den Status; wird durch `AktualisierenFuerStatus` gesetzt |
| `StatusColor` | `Brush` | Get/Private Set | Farbe des Status-Indikators; wird durch `AktualisierenFuerStatus` gesetzt |

### Methoden

| Methode | Sichtbarkeit | Beschreibung |
|---|---|---|
| `StatusIndicatorControl()` | Public | Konstruktor; ruft `InitializeComponent()` auf |
| `OnStatusChanged(DependencyObject, DependencyPropertyChangedEventArgs)` | Private Static | Callback für Status-Property-Änderungen; ruft `AktualisierenFuerStatus` auf |
| `AktualisierenFuerStatus(AufgabeStatus)` | Private | Switch-Expression, die basierend auf Status `StatusText` und `StatusColor` setzt |

### Status-Mapping

In `AktualisierenFuerStatus`:
- `AufgabeStatus.Neu` → "Neu", `Brushes.Gray`
- `AufgabeStatus.Gestartet` → "Gestartet", `Brushes.DodgerBlue`
- `AufgabeStatus.Wartend` → "Wartend", `Brushes.Goldenrod`
- `AufgabeStatus.Beendet` → "Beendet", `Brushes.SeaGreen`
- `AufgabeStatus.Archiviert` → "Archiviert", `Brushes.DimGray`
- `_` → `status.ToString()`, `Brushes.Gray`

**Wichtig:** Es existiert KEINE Dependency Property für `BranchName`, und das XAML-Template zeigt nur `StatusDot` (Ellipse) und `StatusText` (TextBlock).

---

## `TaskDetailViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`

ViewModel für die Aufgabendetailansicht. Verwaltet Aufgaben-Daten, Status, Protokoll, CLI-Prozessstart und UI-State.

### Relevante Klassenmember für diese Anforderung

| Eigenschaft | Typ | Get/Set | Beschreibung |
|---|---|---|---|
| `Aufgabe` | `Aufgabe?` | Get/Private Set | Geladene Aufgabe; triggert mehrere `OnPropertyChanged`-Aufrufe beim Setzen |
| `AufgabeTitel` | `string` | Get only | Convenience-Property: `Aufgabe?.Titel ?? "(wird geladen…)"` |
| `AufgabeStatus` | `AufgabeStatus` | Get only | Convenience-Property: `Aufgabe?.Status ?? Domain.Enums.AufgabeStatus.Neu` |

### Aufgabe Property-Setter (Zeile 69–87)

Beim Setzen der `Aufgabe`-Property werden folgende `OnPropertyChanged`-Aufrufe getriggert:
```
OnPropertyChanged(nameof(AufgabeTitel));
OnPropertyChanged(nameof(AufgabeStatus));
OnPropertyChanged(nameof(KannCliStoppen));
OnPropertyChanged(nameof(KannCliNeuStarten));
OnPropertyChanged(nameof(ShowEditPanel));
OnPropertyChanged(nameof(ShowCliPanel));
OnPropertyChanged(nameof(ShowDiffPanel));
OnPropertyChanged(nameof(KannSpeichern));
OnPropertyChanged(nameof(KannLoeschen));
OnPropertyChanged(nameof(CanAssignIssue));
OnPropertyChanged(nameof(CurrentIssueReferenz));
```

**Wichtig:** Es fehlt `OnPropertyChanged(nameof(AufgabeBranchName))`, und es existiert KEINE Convenience-Property `AufgabeBranchName`.

### Weitere Methoden (Auszug)

- `LadenAsync(CancellationToken)` — Lädt die Aufgabe und ihre Daten
- `SpeichernAsync(CancellationToken)` — Speichert Titel und AnforderungsBeschreibung
- `StartenAsync(CancellationToken)` — Startet die Aufgabe
- `CliNeustartenAsync(CancellationToken)` — Startet die CLI neu
- `CliStoppenAsync(CancellationToken)` — Stoppt den CLI-Prozess
- Weitere: `PluginWechselAsync`, `AufgabeAbschliessenAsync`, `LoeschenAsync`, `IssueZuweisenAsync`, etc.

---

## XAML-Views

### `StatusIndicatorControl.xaml`
Datei: `src/Softwareschmiede.App/Controls/StatusIndicatorControl.xaml`

Template zeigt:
- `StatusDot` (Ellipse mit Status-Farbe)
- `StatusText` (TextBlock mit Status-Text)

**Wichtig:** Keine Elemente für Branch-Name.

### `TaskDetailView.xaml`
Datei: `src/Softwareschmiede.App/Views/TaskDetailView.xaml`

Zeile 317: Verwendung von `StatusIndicatorControl`:
```xaml
<controls:StatusIndicatorControl Status="{Binding AufgabeStatus, Mode=OneWay}" />
```

**Wichtig:** Binding nur für `Status`, kein Binding für `BranchName`.

---

## Zusammenfassung der Mängel für die Anforderung

1. **`StatusIndicatorControl`:** Keine Dependency Property `BranchName`, XAML-Template zeigt keinen Branch-Text.
2. **`TaskDetailViewModel`:** Keine Property `AufgabeBranchName`, kein `OnPropertyChanged` für Branch-Name im `Aufgabe`-Setter.
3. **`TaskDetailView.xaml`:** Kein Binding für Branch-Name an `StatusIndicatorControl`.

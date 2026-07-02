# Bestandsaufnahme: Value Converter

Datei: `src/Softwareschmiede.App/Converters/AppConverters.cs`

Alle Converter sind als `IValueConverter` implementiert und in `App.xaml` als StaticResource registriert.

## `EnumToBoolConverter`
Konvertiert einen Enum-Wert auf `bool` für RadioButton-Bindungen.

| Parameter | Beschreibung |
|-----------|-------------|
| Input | Enum-Wert (`object`) |
| Output | `bool` (true wenn Enum-Wert == ConverterParameter) |
| ConverterParameter | Enum-Wert zum Vergleich (als String) |

## `BoolToVisibilityConverter`
**[VORHANDEN]** Konvertiert einen Boolean-Wert in `Visibility`.

| Parameter | Beschreibung |
|-----------|-------------|
| Input | `bool` |
| Output | `Visibility` (Visible wenn true, sonst Collapsed) |
| ConverterParameter | Nicht verwendet |

**Verwendung:** Im MainWindow.xaml zur Steuerung der Navigation-Text-Sichtbarkeit
```xaml
Visibility="{Binding IsNavigationExpanded, Converter={StaticResource BoolToVisibilityConverter}}"
```

## `BoolToWidthConverter`
Konvertiert einen Boolean-Wert in eine Breite (für die Seitenleiste).

| Parameter | Beschreibung |
|-----------|-------------|
| Input | `bool` |
| Output | `double` (ExpandedWidth wenn true, CollapsedWidth wenn false) |
| ExpandedWidth | Breite im aufgeklappten Zustand (Default: 240.0) |
| CollapsedWidth | Breite im eingeklappten Zustand (Default: 48.0) |

**Verwendung:** Im MainWindow.xaml zur Seitenleisten-Breitenkontrolle
```xaml
Width="{Binding IsNavigationExpanded, Converter={StaticResource BoolToWidthConverter}}"
```

## `InverseBoolToVisibilityConverter`
**[VORHANDEN]** Konvertiert einen inversen Boolean-Wert in `Visibility`.

| Parameter | Beschreibung |
|-----------|-------------|
| Input | `bool` |
| Output | `Visibility` (Visible wenn false, sonst Collapsed) |
| ConverterParameter | Nicht verwendet |

**Verwendung:** Für die Anforderung, um die "Aktive Aufgaben"-Sektion auszublenden, wenn das Dashboard sichtbar ist
```xaml
Visibility="{Binding IsDashboardVisible, Converter={StaticResource InvertedBoolToVisibilityConverter}}"
```

## `NullOrEmptyToVisibilityConverter`
Konvertiert einen Wert in `Visibility` (Collapsed wenn null, leer oder Leerstring).

| Parameter | Beschreibung |
|-----------|-------------|
| Input | `object` oder `string` |
| Output | `Visibility` (Collapsed wenn null/leer, sonst Visible) |
| ConverterParameter | Nicht verwendet |

**Verwendung:** Im DashboardView.xaml für Fehlermeldungen
```xaml
Visibility="{Binding FehlerMeldung, Converter={StaticResource NullOrEmptyToVisibilityConverter}}"
```

---

## FEHLEND (gemäß Anforderung)

### `KiAusfuehrungsStatusConverter`
**[NICHT VORHANDEN]** Neuer Converter erforderlich für die Anforderung.

| Parameter | Beschreibung |
|-----------|-------------|
| Input | `Aufgabe` |
| Output | `string` (z.B. "▶ Läuft", "⏸ Wartet", "⚠ Fehler") |
| ConverterParameter | Nicht verwendet |

**Logik (gemäß Anforderung):**
- Wenn `AktiveRunId` vorhanden und `LastHeartbeatUtc` < 5 Minuten: "▶ Läuft"
- Wenn Status = `Wartend`: "⏸ Wartet"
- Fallback: Status anzeigen oder "Inaktiv"

**Verwendung:** In der Aufgabenkachel zur Anzeige des KI-Ausführungsstatus
```xaml
Text="{Binding ., Converter={StaticResource KiAusfuehrungsStatusConverter}}"
```

---

## Registrierung in App.xaml

Die Converter werden in der Application-Resources registriert:
```xaml
<Application.Resources>
    <converters:EnumToBoolConverter x:Key="EnumToBoolConverter" />
    <converters:BoolToVisibilityConverter x:Key="BoolToVisibilityConverter" />
    <converters:BoolToWidthConverter x:Key="BoolToWidthConverter" />
    <converters:InverseBoolToVisibilityConverter x:Key="InvertedBoolToVisibilityConverter" />
    <converters:NullOrEmptyToVisibilityConverter x:Key="NullOrEmptyToVisibilityConverter" />
    <!-- KiAusfuehrungsStatusConverter muss hier registriert werden -->
</Application.Resources>
```

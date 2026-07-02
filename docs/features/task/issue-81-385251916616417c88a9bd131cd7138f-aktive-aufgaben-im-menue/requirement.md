# Anforderung: Aktive Aufgaben im Menü

**Aufgaben-ID:** 38525191-6616-417c-88a9-bd131cd7138f  
**Feature-Branch:** task/issue-81-385251916616417c88a9bd131cd7138f-aktive-aufgaben-im-menue  
**Erstellt:** 2026-06-29

---

## Fachliche Zusammenfassung

Die WPF-Desktopanwendung wird um die Anzeige aktuell aktiver Aufgaben im Navigationsmenü (Seitenleiste) erweitert. Diese neue Sektion zeigt Aufgaben mit Status `Gestartet` oder `Wartend` als gerahmte Kacheln unterhalb der bestehenden Navigationseinträge (Dashboard, Projekte, Einstellungen) an. Jede Kachel displays den Aufgabentitel und den aktuellen KI-Ausführungsstatus (abgeleitet aus `AktiveRunId` und `LastHeartbeatUtc`). Ein Navigationsbutton ermöglicht den direkten Zugriff auf die Aufgabendetailansicht. Das Dashboard zeigt dieselbe Aufgabenliste an; ist das Dashboard aktiv, wird die Liste im Menü automatisch ausgeblendet, um Redundanz zu vermeiden.

---

## Betroffene Klassen und Komponenten

### Domain & Entities

- **`Aufgabe`** (bestehend)
  - Besteht bereits mit Status-Property (`AufgabeStatus` mit Werten `Gestartet`, `Wartend`, etc.)
  - Properties `AktiveRunId` und `LastHeartbeatUtc` für Laufzeit-Status-Ermittlung
  - Keine neuen Properties erforderlich

### Application Services

- **`AufgabeService`** (erweitern)
  - Neue Methode: `GetAktiveAufgabenAsync(CancellationToken ct)` → `Task<List<Aufgabe>>`
    - Filtert Aufgaben mit Status `Gestartet` oder `Wartend`
    - Sortierung nach letzter Aktivität (z.B. `LastHeartbeatUtc` oder `ErstellungsDatum` absteigend)
    - Optional: Limit für maximale Anzahl (z.B. 20)

### ViewModels

- **`MainWindowViewModel`** (erweitern)
  - Neue Property: `AktiveAufgaben` : `ObservableCollection<Aufgabe>` (read-only)
  - Neue Property: `IsDashboardVisible` : `bool` (computed, `true` wenn `CurrentView is DashboardViewModel`)
  - Neue Methode: `AktiveAufgabenAktualisierenAsync(CancellationToken ct)` : `Task`
    - Ruft `AufgabeService.GetAktiveAufgabenAsync()` auf
    - Aktualisiert `AktiveAufgaben` ObservableCollection
    - Wird regelmäßig aufgerufen (z.B. bei Navigation oder via Timer)
  - Neuer Command: `NavigateZuAufgabeCommand` : `ICommand`
    - Parameter: `Guid aufgabeId`
    - Setzt `CurrentView` auf neue `TaskDetailViewModel`-Instanz mit Aufgaben-ID

- **`DashboardViewModel`** (erweitern)
  - Neue Property: `AktiveAufgaben` : `ObservableCollection<Aufgabe>` (read-only)
  - Erweiterung der Methode `LadenAsync(CancellationToken ct)`:
    - Ruft `AufgabeService.GetAktiveAufgabenAsync()` auf
    - Befüllt `AktiveAufgaben` ObservableCollection

### Views & UI-Komponenten

- **`MainWindow.xaml`** (erweitern)
  - Seitenleiste (linke Spalte): Neue Sektion für aktive Aufgaben
    - Platzbedarf: unterhalb der bestehenden Navigationseinträge
    - Visueller Separator (z.B. `Border` mit `BorderThickness="0,1,0,0"`)
    - `ItemsControl` mit `AktiveAufgaben` als ItemsSource
    - Sichtbarkeit: `Visibility="{Binding IsDashboardVisible, Converter={StaticResource BoolToVisibilityConverter}}"` (inverse Logik)
    - DataTemplate für Aufgabenkacheln (siehe unten)

- **`DashboardView.xaml`** (erweitern)
  - Neue Sektion für aktive Aufgaben (identisch zur Menü-Anzeige)
  - Platzierung: z.B. unterhalb der Project-Cards oder in separater Panel

- **Neue Komponente: `AktiveAufgabeKachel.xaml` oder `AufgabeListItemControl.xaml`** (optional, für Wiederverwendung)
  - UserControl für konsistente Darstellung einer aktiven Aufgabe
  - Properties:
    - `Aufgabe` : `Aufgabe` (Binding)
    - `NavigiereCommand` : `ICommand` (Binding zu `MainWindowViewModel.NavigateZuAufgabeCommand`)
  - Visuelle Struktur:
    - `Border` mit abgerundeten Ecken, Rahmen und Hintergrund
    - StackPanel/Grid mit:
      - Titel-TextBlock (Aufgaben-Name, FontWeight.Bold)
      - Status-TextBlock (KI-Ausführungsstatus, z.B. "▶ Läuft")
      - Pfeil-Button rechts (navigiert zu Aufgabendetail, Icon: →, Command: `NavigiereCommand` mit Aufgaben-ID)

### Value Converter & Helper

- **Optionaler neuer Converter: `KiAusfuehrungsStatusConverter`**
  - Input: `Aufgabe`
  - Output: `string` (z.B. "▶ Läuft", "⏸ Wartet", "⚠ Fehler")
  - Logik:
    - Wenn `AktiveRunId` vorhanden und `LastHeartbeatUtc` < 5 Minuten: "▶ Läuft"
    - Wenn Status = `Wartend`: "⏸ Wartet"
    - Fallback: Status anzeigen oder "Inaktiv"

---

## Implementierungsansatz

### 1. Service-Layer (`AufgabeService`)

Neue Methode zum Abrufen aktiver Aufgaben:

```csharp
public async Task<List<Aufgabe>> GetAktiveAufgabenAsync(CancellationToken ct = default)
{
    return await _context.Aufgaben
        .AsNoTracking()
        .Where(a => a.Status == AufgabeStatus.Gestartet || a.Status == AufgabeStatus.Wartend)
        .OrderByDescending(a => a.LastHeartbeatUtc ?? a.ErstellungsDatum)
        .ToListAsync(ct);
}
```

### 2. ViewModel-Layer

**`MainWindowViewModel` Erweiterung:**
- Bei jeder View-Navigation `IsDashboardVisible` neu berechnen (via `OnPropertyChanged`)
- `AktiveAufgaben` nach Navigation zu Dashboard/ProjectList/Settings aktualisieren (optional: regelmäßiger Timer alle 5-10 Sekunden)
- `NavigateZuAufgabeCommand` implementieren mit Aufgaben-ID-Parameter

**`DashboardViewModel` Erweiterung:**
- Im `LadenAsync()` auch `AufgabeService.GetAktiveAufgabenAsync()` aufrufen und `AktiveAufgaben` befüllen

### 3. UI-Layer (XAML)

**`MainWindow.xaml` Seitenleiste:**

Neue Sektion zwischen bestehenden Navigationseinträgen und Einstellungen:

```xaml
<!-- Spacer / Separator -->
<Border Height="1" Background="{DynamicResource BorderBrush}" Margin="8,8" />

<!-- Sektion: Aktive Aufgaben -->
<TextBlock Text="Aktive Aufgaben"
           FontSize="11"
           Foreground="{DynamicResource SecondaryTextBrush}"
           Margin="8,8,8,4"
           Visibility="{Binding IsDashboardVisible, Converter={StaticResource InvertedBoolToVisibilityConverter}}" />

<ScrollViewer MaxHeight="300"
              VerticalScrollBarVisibility="Auto"
              Visibility="{Binding IsDashboardVisible, Converter={StaticResource InvertedBoolToVisibilityConverter}}">
    <ItemsControl ItemsSource="{Binding AktiveAufgaben}">
        <ItemsControl.ItemTemplate>
            <DataTemplate>
                <!-- Aufgabenkachel inline oder via UserControl -->
                <Border Background="{DynamicResource SurfaceBrush}"
                        BorderBrush="{DynamicResource BorderBrush}"
                        BorderThickness="1"
                        CornerRadius="8"
                        Margin="4,4"
                        Padding="8">
                    <Grid>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="*" />
                            <ColumnDefinition Width="Auto" />
                        </Grid.ColumnDefinitions>
                        
                        <StackPanel Grid.Column="0">
                            <TextBlock Text="{Binding Titel}"
                                       FontSize="12"
                                       FontWeight="SemiBold"
                                       Foreground="{DynamicResource PrimaryTextBrush}"
                                       TextTrimming="CharacterEllipsis" />
                            <TextBlock Text="{Binding ., Converter={StaticResource KiAusfuehrungsStatusConverter}}"
                                       FontSize="10"
                                       Foreground="{DynamicResource SecondaryTextBrush}"
                                       Margin="0,4,0,0" />
                        </StackPanel>
                        
                        <Button Grid.Column="1"
                                Content="→"
                                Command="{Binding DataContext.NavigateZuAufgabeCommand, RelativeSource={RelativeSource AncestorType=Window}}"
                                CommandParameter="{Binding Id}"
                                Padding="4,2"
                                Background="Transparent"
                                BorderThickness="0"
                                FontSize="12"
                                VerticalAlignment="Center" />
                    </Grid>
                </Border>
            </DataTemplate>
        </ItemsControl.ItemTemplate>
    </ItemsControl>
</ScrollViewer>
```

**`DashboardView.xaml` Erweiterung:**
- Ähnliches Layout (ohne Einschränkung auf Seitenleisten-Breite)
- Vollständige Aufgabenliste ohne ScrollViewer-Höhen-Limit (oder größeres Limit)

---

## Konfiguration

### Optionale AppSettings-Einträge:

- **`ActiveTasksRefreshInterval`** (Millisekunden, Default: 5000)
  - Intervall zur automatischen Aktualisierung der aktiven Aufgaben im Menü
  - Kann in Einstellungen für Benutzerfreundlichkeit konfigurierbar sein

- **`ActiveTasksMaxDisplayCount`** (Integer, Default: unbegrenzt oder 20)
  - Maximale Anzahl anzuzeigender aktiver Aufgaben im Menü
  - Begrenzt visuelle Überlastung bei vielen parallelen Aufgaben

---

## Offene Fragen

1. **KI-Ausführungsstatus Präzision:**
   - Aus dem README ist ersichtlich, dass in der CLI der Status "Ausführung läuft" / "Wartet auf Eingabe" angezeigt wird (Zeile 77)
   - Soll der Status im Menü dieselbe Granularität haben oder genügt "Gestartet" / "Wartend" (basierend auf `AufgabeStatus`)?
   - Sollen weitere Status wie "Rate-Limited" oder "Fehler" angezeigt werden?

2. **Refresh-Verhalten:**
   - Soll die Aufgabenliste im Menü **kontinuierlich aktualisiert** werden (z.B. alle 5 Sekunden), oder nur bei Navigation?
   - Kann dies zu Performance-Problemen bei vielen Projekten führen?

3. **Scrolling & Limit:**
   - Bei vielen aktiven Aufgaben: Sollen alle angezeigt werden (mit ScrollViewer in der Seitenleiste) oder auf eine maximale Anzahl begrenzt?
   - Empfehlung: Limit auf z.B. 10-20 Aufgaben mit ScrollViewer

4. **Navigation zu Aufgabendetail:**
   - Soll die `ProjectDetailView` beim Navigieren zur Aufgabendetail erhalten bleiben, oder wird diese durch `TaskDetailView` ersetzt?
   - Annahme: `TaskDetailView` ersetzt den `CurrentView` (analog zu bestehender Architektur aus TaskDetailViewModel Beschreibung)

5. **Definition "aktive Aufgaben":**
   - Sollen nur Aufgaben mit Status `Gestartet` oder `Wartend` als "aktiv" gelten?
   - Oder auch `Neu`-Status, wenn diese gerade bearbeitet werden?
   - Annahme: Nur `Gestartet` und `Wartend`

6. **Status "Beendet" / "Archiviert":**
   - Sollen diese aus der Liste ausgeblendet werden, oder optional angezeigt?
   - Annahme: Ausgeblendet (nur aktive, laufende Aufgaben im Menü)

7. **Darstellung der Aufgabenkachel:**
   - Sollen zusätzlich Projekt-Kontext (z.B. Projekt-Name oder Icon) angezeigt werden?
   - Oder nur Aufgabentitel + KI-Status (compact)?
   - Annahme: Nur Titel + KI-Status (kompakt)

8. **Fehlerbehandlung:**
   - Wenn das Laden aktiver Aufgaben fehlschlägt (z.B. Datenbank-Fehler), soll ein Fehler-Banner angezeigt werden?
   - Oder silent fallback auf leere Liste?

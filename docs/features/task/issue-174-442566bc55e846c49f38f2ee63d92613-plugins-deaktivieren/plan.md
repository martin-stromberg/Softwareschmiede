# Umsetzungsplan: Issue 174 — Plugins-Einstellungen UI-Korrekturen

## Übersicht

Am bereits fertigen und committeten Plugins-Register der Einstellungsseite werden vier gezielte UI-Korrekturen vorgenommen: (1) Textkontrast der beiden Plugin-Auswahllisten reparieren, (2) die Aktivierungs-CheckBoxen aus den Listen entfernen (reine Selektionslisten), (3) das Aktivieren/Deaktivieren des ausgewählten Plugins in den rechten Einstellungsbereich verlagern und (4) den Plugin-Namen als Kopfzeile im rechten Bereich anzeigen. Betroffen sind ausschließlich `SettingsView.xaml`, `SettingsView.xaml.cs`, die beiden Theme-Dateien für das Listen-Styling sowie der E2E-Test `E2E_PluginAktivierung`. Es gibt keine Änderungen an Datenmodell, Services, Persistenzformat oder der Validierungslogik.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|------------------|------------|
| Kontrast-Fix der Aktivierungslisten | Lokal (scoped) auf die beiden `ListBox`en in `SettingsView.xaml` angewendetes `Background`/`Foreground` plus `ItemContainerStyle` mit themed Hintergrund und Selektions-Triggern — **keine** globale `ListBox`/`ListBoxItem`-Default-Style in den Theme-Dateien | Ein globaler Default-Style würde alle übrigen `ListBox`en der App (Dashboard, Aufgabenlisten, Dialoge) mitverändern und unkalkulierbare Seiteneffekte erzeugen. Der Fehler betrifft nur diese zwei Listen; die Analogie zum vorhandenen `ComboBoxItem`-Style (Selektion = `AccentBrush`/Weiß, Hover = `AccentHoverBrush`) bleibt gewahrt. |
| Aktivierungs-Steuerelement im rechten Bereich | `CheckBox` mit Beschriftung „Plugin aktiviert", TwoWay an `SelectedPlugin.IsEnabled` gebunden | Antwort auf offene Frage: `CheckBox` ist konsistent mit bestehenden UI-Mustern (z. B. „Allgemein"-Tab) und mit dem globalen `CheckBox`-Style der Themes; ein `ToggleButton` würde ein neues, ungestyltes Muster einführen. |
| Sichtbarkeit von Kopfzeile + Aktivierungs-CheckBox | Nur sichtbar, wenn `SelectedPlugin` nicht `null` ist; Steuerung über den vorhandenen `NullOrEmptyToVisibilityConverter`, gebunden an `SelectedPlugin` | Antwort auf offene Frage: Bei leerer Auswahl bleibt der rechte Bereich wie bisher leer. Der Converter liefert für `null` `Collapsed`, sonst `Visible` — kein neuer Converter nötig. |
| Kopfzeile mit Plugin-Namen | `TextBlock`, gebunden an `SelectedPlugin.PluginName`, oberhalb der Aktivierungs-CheckBox | Folgt bestehendem Muster (Gruppen-Header im rechten Bereich nutzen ebenfalls `TextBlock` mit `PrimaryTextBrush`, `FontWeight="Bold"`). |
| Item-Selektion nach CheckBox-Entfall | Wegfall des `EventSetter`/Handlers `OnPluginActivationItemPreviewMouseLeftButtonDown`; normale `ListBoxItem`-Selektion über `SelectionChanged` genügt | Ohne CheckBox erreicht der Klick wieder die reguläre Selektionslogik der `ListBoxItem`; die Sonderbehandlung der Tunneling-Phase war ausschließlich wegen der CheckBox nötig. |

## Programmabläufe

### Plugin auswählen (unverändert im Ablauf, nur Auslöser geändert)

1. Nutzer klickt einen Eintrag in einer der Aktivierungslisten (`SourceCodeManagementPlugins` bzw. `DevelopmentAutomationPlugins`).
2. Da keine CheckBox mehr das `MouseLeftButtonDown` abfängt, selektiert die `ListBox` den Eintrag direkt und löst `SelectionChanged` aus.
3. `OnPluginSelectionChanged` (Code-Behind) erkennt den `PluginActivationEntry` und führt `PluginSelectedCommand` aus.
4. `PluginSelectedCommand` ruft `LoadSelectedPluginSettings(entry)` auf: setzt `SelectedPlugin = entry` und lädt die Einstellungsgruppen in `SelectedPluginSettings`.
5. Der rechte Bereich zeigt nun Kopfzeile (`SelectedPlugin.PluginName`), die Aktivierungs-CheckBox (`SelectedPlugin.IsEnabled`) und die Einstellungsgruppen.

Beteiligte Klassen/Komponenten: `SettingsView.xaml`, `SettingsView.xaml.cs` (`OnPluginSelectionChanged`), `SettingsViewModel` (`PluginSelectedCommand`, `LoadSelectedPluginSettings`), `PluginActivationEntry`.

### Plugin aktivieren/deaktivieren (neuer Auslöser für vorhandene Logik)

1. Bei ausgewähltem Plugin schaltet der Nutzer die Aktivierungs-CheckBox „Plugin aktiviert" im rechten Bereich um.
2. Das TwoWay-Binding schreibt den Wert direkt in `SelectedPlugin.IsEnabled` — dies ist dasselbe `PluginActivationEntry`-Objekt, das in der jeweiligen `ObservableCollection` liegt.
3. Beim Speichern (`SpeichernCommand` → `SpeichernAsync`) prüft `ValidierePflichtfelder` → `ValidierePluginAktivierung`, dass pro Kategorie mindestens ein Eintrag `IsEnabled` ist; andernfalls wird `FehlerMeldung` gesetzt und der Speichervorgang abgebrochen.
4. Bei erfolgreicher Validierung persistiert `SpeichernAsync` für jeden Eintrag `entry.IsEnabled` via `PluginActivationService.SetPluginEnabledAsync(entry.PluginPrefix, entry.IsEnabled, ct)`.

Beteiligte Klassen/Komponenten: `SettingsView.xaml`, `SettingsViewModel` (`SpeichernAsync`, `ValidierePluginAktivierung`), `PluginActivationEntry.IsEnabled`, `PluginActivationService`.

## Neue Klassen

Keine.

## Änderungen an bestehenden Klassen

### `SettingsView.xaml` (WPF-View)

- **Aktivierungslisten (SCM + KI):**
  - `<CheckBox IsChecked="{Binding IsEnabled, Mode=TwoWay}" ...>` aus beiden `ListBox.ItemTemplate` entfernen; das `StackPanel` enthält danach nur noch den Namens-`TextBlock`.
  - Der Namens-`TextBlock` behält `Text="{Binding PluginName}"`, `Foreground="{DynamicResource PrimaryTextBrush}"` und insbesondere `AutomationProperties.Name="{Binding PluginPrefix, StringFormat={}{0}.Eintrag}"` (wird von E2E-Helpern zur Item-Selektion benötigt).
  - Kontrast-Fix: Beiden `ListBox`en einen themed `Background` (`{DynamicResource SurfaceBrush}`) und `Foreground` (`{DynamicResource PrimaryTextBrush}`) geben.
  - `ItemContainerStyle` beider `ListBox`en: den `EventSetter` für `PreviewMouseLeftButtonDown` entfernen; stattdessen einen `ListBoxItem`-Style analog zum vorhandenen `ComboBoxItem`-Style (themed `Background`/`Foreground`, Trigger `IsSelected` → `AccentBrush`/Weiß, `IsMouseOver` → `AccentHoverBrush`) für ausreichende Selektions-/Hover-Kontraste.
- **Rechter Bereich (`ScrollViewer Grid.Column="2"`):**
  - Inhalt von „nur `ItemsControl`" auf ein `StackPanel` umstellen, das enthält: (a) Kopfzeilen-`TextBlock` gebunden an `SelectedPlugin.PluginName` (Bold, `PrimaryTextBrush`), (b) Aktivierungs-`CheckBox` „Plugin aktiviert" mit `IsChecked="{Binding SelectedPlugin.IsEnabled, Mode=TwoWay}"` und `AutomationProperties.Name="PluginAktiviert"`, (c) das bestehende `ItemsControl` für `SelectedPluginSettings`.
  - Kopfzeile und CheckBox erhalten `Visibility="{Binding SelectedPlugin, Converter={StaticResource NullOrEmptyToVisibilityConverter}}"`.

### `SettingsView.xaml.cs` (Code-Behind)

- **Geänderte/entfernte Handler:** `OnPluginActivationItemPreviewMouseLeftButtonDown` entfernen (nicht mehr referenziert, da der zugehörige `EventSetter` wegfällt). `OnPluginSelectionChanged` bleibt unverändert.

### Theme-Dateien (`DarkTheme.xaml`, `LightTheme.xaml`)

- Nur betroffen, falls der gewählte `ListBoxItem`-Style als benannter (`x:Key`) Style zentral abgelegt werden soll. Gemäß Designentscheidung wird der Style **lokal in `SettingsView.xaml`** definiert; die Theme-Dateien werden dann **nicht** geändert. Sie sind hier nur als bewusst nicht angefasste Dateien vermerkt, um eine globale `ListBox`-Regression auszuschließen.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine neuen Regeln. Die bestehende Regel bleibt unverändert erhalten:

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `SourceCodeManagementPlugins` | Mindestens ein Eintrag mit `IsEnabled == true` | `FehlerMeldung = "Mindestens ein Quellcodeverwaltungs-Plugin muss aktiv bleiben."`, Speichern bricht ab |
| `DevelopmentAutomationPlugins` | Mindestens ein Eintrag mit `IsEnabled == true` | `FehlerMeldung = "Mindestens ein KI-Plugin muss aktiv bleiben."`, Speichern bricht ab |

Der Auslöser wandert von der Listen-CheckBox zur rechten Aktivierungs-CheckBox; die Logik in `ValidierePluginAktivierung` ist davon unberührt, da sie unverändert über `IsEnabled` der Collection-Einträge arbeitet.

## Konfigurationsänderungen

Keine. Persistenzschlüssel (`plugins.enabled.{PluginPrefix}`) und Services bleiben unverändert.

## Seiteneffekte und Risiken

- **E2E-Test `E2E_PluginAktivierung`:** Der Test toggelt aktuell Plugins über die Listen-CheckBoxen (`ByName(PluginPrefix)` → `AsCheckBox().IsChecked = false`). Nach Entfall dieser CheckBoxen bricht der Test und muss auf den neuen Ablauf (Listeneintrag selektieren → rechte CheckBox „PluginAktiviert" umschalten) umgestellt werden. Siehe Abschnitt Tests.
- **Shared-Helper `WpfTestBase.ConfigureLocalDirectoryPlugin` (Zeile 567):** Selektiert den Listeneintrag über `ByName("LocalDirectoryPlugin.Eintrag")` (den Namens-`TextBlock`). Bleibt funktionsfähig, **sofern** die `AutomationProperties.Name`-Belegung `{PluginPrefix}.Eintrag` auf dem `TextBlock` erhalten bleibt — daher ist deren Beibehaltung verpflichtend.
- **Standard-Plugin-ComboBoxen und deren E2E-Nutzung (`E2E_SettingsKiPluginPersistence`, `E2E_SettingsCommandLineParameters`):** Nicht betroffen; sie nutzen `DefaultKiPlugin`/`DefaultScmPlugin` und die `.Eintrag`-Selektion, nicht die entfernten CheckBoxen.
- **Unit-Tests `SettingsViewModelTests`:** Setzen `IsEnabled` direkt auf den Entry-Objekten (nicht über UI). Unverändert lauffähig, kein Bruch.
- **Kontrast-Änderung:** Durch die lokale Beschränkung des Stylings auf die beiden Listen bestehen keine Auswirkungen auf andere `ListBox`en der App.

## Umsetzungsreihenfolge

1. **Aktivierungslisten in `SettingsView.xaml` umbauen (Kontrast + CheckBox-Entfall)**
   - Voraussetzungen: Keine (`SurfaceBrush`, `PrimaryTextBrush`, `AccentBrush`, `AccentHoverBrush`, `BorderBrush` existieren in beiden Themes).
   - Beschreibung: In beiden `ListBox`en die Aktivierungs-`CheckBox` aus dem `ItemTemplate` entfernen; themed `Background`/`Foreground` setzen; `ItemContainerStyle` von reinem `EventSetter` auf einen `ListBoxItem`-Style mit themed Hintergrund + Selektions-/Hover-Triggern umstellen; Namens-`TextBlock` inkl. `AutomationProperties.Name="{...}.Eintrag"` unverändert belassen.

2. **Rechten Bereich in `SettingsView.xaml` erweitern (Kopfzeile + Aktivierungs-CheckBox)**
   - Voraussetzungen: `NullOrEmptyToVisibilityConverter` (global in `App.xaml` registriert, vorhanden); Property `SelectedPlugin` mit `PluginName` und `IsEnabled` (vorhanden).
   - Beschreibung: Den `ScrollViewer`-Inhalt in ein `StackPanel` fassen mit Kopfzeilen-`TextBlock` (`SelectedPlugin.PluginName`), Aktivierungs-`CheckBox` „Plugin aktiviert" (`AutomationProperties.Name="PluginAktiviert"`, TwoWay an `SelectedPlugin.IsEnabled`) und dem bestehenden `ItemsControl`; Kopfzeile und CheckBox über den Converter an `SelectedPlugin` sichtbar schalten.

3. **Code-Behind `SettingsView.xaml.cs` bereinigen**
   - Voraussetzungen: Schritt 1 abgeschlossen (kein `EventSetter` referenziert den Handler mehr).
   - Beschreibung: Methode `OnPluginActivationItemPreviewMouseLeftButtonDown` entfernen.

4. **E2E-Test `E2E_PluginAktivierung` an neuen Ablauf anpassen**
   - Voraussetzungen: Schritte 1–2 abgeschlossen (neue Automation-Namen `{prefix}.Eintrag` für Selektion, `PluginAktiviert` für die CheckBox stehen bereit).
   - Beschreibung: `DeaktivierePlugin` und die SCM-Deaktivierungs-/Reload-Schritte so umstellen, dass zuerst der Listeneintrag über `ByName("{prefix}.Eintrag")` selektiert und anschließend die CheckBox `ByName("PluginAktiviert")` umgeschaltet bzw. deren `IsChecked` geprüft wird.

5. **Voller Build + Verifikation**
   - Voraussetzungen: Schritte 1–4 abgeschlossen.
   - Beschreibung: Voller Build; anschließend `SettingsViewModelTests`/`PluginActivationServiceTests` (reguläre Lane) und der angepasste E2E-Test (`Category=OsInterface`) ausführen.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| — | — | Keine neuen Unit-Test-Methoden erforderlich; die bestehenden Unit-Tests decken Aktivierung/Validierung/Persistenz auf ViewModel-Ebene bereits ab (die Änderungen sind rein an der View/am Auslöser). |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| Keine (Unit-Ebene) | `SettingsViewModelTests` setzt `IsEnabled` direkt auf den Entries und ist von der UI-Umstellung unberührt. |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|--------------------------------|
| Letztes SCM-Plugin über rechte CheckBox „PluginAktiviert" deaktivieren → Validierungsfehler | `E2E_PluginAktivierung` (`DeaktivierenDesLetztenScmPlugins_...`) | Validierungsregel „mind. ein Plugin je Kategorie" bleibt beim verlagerten Toggle erhalten |
| KI-Plugins über rechte CheckBox deaktivieren, speichern, neu öffnen → Status persistiert; Item-Selektion + rechte CheckBox spiegeln Status | `E2E_PluginAktivierung` (`DeaktivierenVonDreiKiPlugins_...`) | Aktivierung wandert in den rechten Bereich; Persistenz bleibt erhalten |

### Betroffene bestehende E2E-Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `E2E_PluginAktivierung` | Statt CheckBox in der Liste (`ByName(PluginPrefix)`) muss der Ablauf Listeneintrag (`ByName("{prefix}.Eintrag")`) selektieren und die rechte CheckBox (`ByName("PluginAktiviert")`) umschalten/prüfen. `DeaktivierePlugin`-Helper und die SCM-Phase sowie die Reload-Assertion entsprechend umbauen. |
| `WpfTestBase.ConfigureLocalDirectoryPlugin` | Kein Bruch, aber Prüfpflicht: Verlässt sich auf `ByName("LocalDirectoryPlugin.Eintrag")`; funktioniert weiter, solange die `.Eintrag`-Automation-Namen erhalten bleiben. |

## Offene Punkte

Keine.

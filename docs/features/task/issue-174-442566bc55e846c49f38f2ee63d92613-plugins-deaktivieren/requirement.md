# Anforderungsübersetzung: Issue 174 — Plugins-Einstellungen UI-Korrekturen

## Fachliche Zusammenfassung

Das Feature "Plugins deaktivieren" (Issue 174) wurde mit einem Plugins-Register in den Anwendungseinstellungen umgesetzt. Der Anwender hat nach Tests folgende UI-Korrektionen angefordert:

1. **Kontrast-Fehler**: Die Plugin-Listen auf der linken Seite des Plugins-Tabs haben unzureichenden Kontrast (weißer Hintergrund, hellgrauer Text), sodass die Plugin-Namen nicht lesbar sind.
2. **UI-Struktur-Umgestaltung**: Die Aktivierung/Deaktivierung von Plugins soll nicht mehr über CheckBoxen direkt in der linken Auswahlliste erfolgen, sondern über einen Toggle-Schalter oben im rechten Einstellungsbereich.
3. **Fehlendes Label**: Der rechte Bereich (Einstellungsgruppen des ausgewählten Plugins) benötigt eine Kopfzeile mit dem Plugin-Namen zur besseren Orientierung.

Das Validierungssystem (Minimum ein aktives Plugin pro Kategorie: SCM und KI) muss erhalten bleiben.

---

## Betroffene Klassen und Komponenten

### UI-Komponenten (XAML/Code-Behind)

- **`SettingsView.xaml`** (Lines 285–337)
  - `ListBox` für `SourceCodeManagementPlugins` (Kategorie "Quellcodeverwaltung")
  - `ListBox` für `DevelopmentAutomationPlugins` (Kategorie "KI")
  - Itemtemplate mit CheckBox + TextBlock (aktuell Lines 296–305 und 325–334)
  - `ItemsControl` für `SelectedPluginSettings` (rechter Bereich, Lines 343–373)
  - Scrollviewer Grid.Column=2 für Einstellungen des ausgewählten Plugins

- **`SettingsView.xaml.cs`**
  - Event-Handler `OnPluginSelectionChanged` (Lines 32–49) — selektiert das Plugin in der Liste
  - Event-Handler `OnPluginActivationItemPreviewMouseLeftButtonDown` (Lines 62–66) — für ListBoxItem-Selektion

### ViewModels

- **`SettingsViewModel.cs`**
  - Property `SelectedPlugin` (Lines 120–124) — aktuell ausgewählter Eintrag
  - Property `SelectedPluginSettings` (Lines 127–131) — Einstellungsgruppen des ausgewählten Plugins
  - Methode `LoadSelectedPluginSettings` (Lines 304–308) — lädt die Einstellungen eines ausgewählten Plugins
  - Methode `ValidierePluginAktivierung` (Lines 374–389) — prüft, dass mind. ein Plugin pro Kategorie aktiv ist
  - Methode `SpeichernAsync` (Lines 249–297) — speichert `entry.IsEnabled` für alle Einträge (Line 260)
  - ObservableCollections: `SourceCodeManagementPlugins` (Line 114), `DevelopmentAutomationPlugins` (Line 117)

- **`PluginActivationEntry.cs`**
  - Property `IsEnabled` (Lines 31–35) — bindbar, Aktivierungsstatus des Plugins
  - Wird **nicht geändert**; bleibt als Model-Klasse bestehen, nur die UI-Bindung wird umgestellt

---

## Implementierungsansatz

### 1. **Kontrast korrigieren** (Änderung: XAML)

In `SettingsView.xaml`, Zeilen 285–337: Die `Foreground`-Eigenschaft der `TextBlock`-Elemente in den ListBox-ItemTemplates überprüfen und auf `{DynamicResource PrimaryTextBrush}` setzen (bereits vorhanden in Zeile 302, aber möglicherweise nicht aktiv wegen Hintergrund-Styling oder Vererbung). Falls notwendig, explizites Styling auf der `ListBox` überprüfen und korrigieren.

**Aktion:**
- Stelle sicher, dass alle `TextBlock`-Elemente in den ListBox-ItemTemplates (Zeilen 300–304 und 329–333) `Foreground="{DynamicResource PrimaryTextBrush}"` verwenden.
- Überprüfe die ListBox-Style-Vererbung auf problematische Foreground-Override-Werte.

### 2. **CheckBox aus den Listen entfernen** (Änderung: XAML + Code-Behind)

In `SettingsView.xaml`, Zeilen 294–307 und 323–336: Die CheckBox-Elemente aus den ItemTemplates entfernen. Die `StackPanel` sollte nur noch das `TextBlock` mit dem Plugin-Namen enthalten.

**Aktion:**
- Entferne die `<CheckBox>`-Elemente aus beiden ListBox-ItemTemplates (aktuell Zeilen 297–299 und 326–328).
- Behalte das `TextBlock` mit `Text="{Binding PluginName}"`.
- Die ListBox fungiert nun als reine Auswahlliste (Selection nur, kein Checkbox-Toggle).

**Konsequenz für Code-Behind:**
- Der Handler `OnPluginActivationItemPreviewMouseLeftButtonDown` (Zeilen 62–66) wird dann nicht mehr benötigt, da ohne CheckBox die normale ListBoxItem-Selektion funktioniert. Kann beibehalten oder entfernt werden.

### 3. **Toggle-Schalter im rechten Bereich hinzufügen** (Änderung: XAML + ViewModel)

Im rechten Bereich (`ScrollViewer Grid.Column="2"`, Zeilen 342–374) einen neuen **Toggle/Schalter "Plugin aktiviert"** oberhalb der Einstellungsgruppen einfügen.

**XAML-Änderung:**
- Neuer Header oberhalb des `ItemsControl` mit:
  - Plugin-Name als `TextBlock` (Überschrift, gebunden an `SelectedPlugin.PluginName`)
  - Darunter ein `CheckBox` oder `ToggleButton` mit Label "Plugin aktiviert", gebunden an `SelectedPlugin.IsEnabled` (Mode=TwoWay)

**ViewModel-Änderung (optional, je nach Validierungsbedarf):**
- Die Validierung in `ValidierePluginAktivierung` bleibt unverändert und prüft weiterhin, dass `SourceCodeManagementPlugins.All(entry => !entry.IsEnabled)` falsch ist, etc.
- Die `SpeichernAsync`-Methode (Line 260) bleibt unverändert: `await _pluginActivationService.SetPluginEnabledAsync(entry.PluginPrefix, entry.IsEnabled, ct);`
- Das Binding von `SelectedPlugin.IsEnabled` zur Checkbox/Toggle erfolgt zweiseitig (TwoWay), sodass Änderungen direkt in `PluginActivationEntry.IsEnabled` reflektiert werden und bei Speichern übernommen werden.

### 4. **Plugin-Namen als Kopfzeile im rechten Bereich** (Änderung: XAML)

Ein `TextBlock` oder `Label` mit dem Plugin-Namen oben im rechten Bereich (`ScrollViewer Grid.Column="2"`) einfügen, gebunden an `SelectedPlugin.PluginName`. Dies geschieht als Teil von Schritt 3.

---

## Konfiguration

Keine zusätzliche Konfiguration erforderlich. Das Feature nutzt weiterhin:
- `AppEinstellungService` für persistente App-Einstellungen
- `PluginActivationService` für Plugin-Aktivierungsstatus (persistiert)
- Validierungsregeln in `SettingsViewModel.ValidierePluginAktivierung()`

---

## Offene Fragen und Annahmen

1. **Soll der Toggle immer sichtbar sein oder nur wenn ein Plugin ausgewählt ist?**
   - *Annahme:* Der Toggle wird nur sichtbar, wenn `SelectedPlugin` nicht null ist. Wenn kein Plugin ausgewählt ist, bleibt der rechte Bereich leer (wie aktuell bei leerer `SelectedPluginSettings`-Liste).

2. **Welcher Toggle-Stil wird verwendet: CheckBox oder ToggleButton?**
   - *Annahme:* Eine `CheckBox` mit Beschriftung "Plugin aktiviert" ist konsistent mit bestehenden UI-Mustern in der App (z. B. Line 220 in "Allgemein"-Tab).

3. **Soll die UI-Änderung mit Tests abgedeckt werden (E2E-Tests)?**
   - *Annahme:* Bestehende E2E-Tests sollten auf die neue Struktur (kein Checkbox-Klick auf der Liste, stattdessen Toggle im rechten Bereich) angepasst werden, falls vorhanden. Die `OnPluginActivationItemPreviewMouseLeftButtonDown`-Logik wird dann nicht mehr benötigt.

4. **Soll die Fehlermeldung "Mindestens ein X-Plugin muss aktiv bleiben" unverändert bleiben?**
   - *Annahme:* Ja, die bestehende Validierungslogik und deren Fehlermeldungen (Lines 378, 384 in `SettingsViewModel.cs`) bleiben unverändert.

5. **Soll das Standard-SCM/KI-Plugin-ComboBox-Verhalten (Zeilen 249–277 in SettingsView.xaml) beibehalten werden?**
   - *Annahme:* Ja, die ComboBoxen für Standard-Plugin-Auswahl bleiben unverändert; nur die Aktivierungslisten werden umgestaltet.

# Bestandsaufnahme: Issue 174 — Plugins-Einstellungen UI-Korrektionen

Analyse der bestehenden Code-Struktur für die fachliche Anforderung "Plugins deaktivieren — UI-Korrektionen". Fokus auf die Plugins-Register-Komponente in der Einstellungsseite, die bereits implementiert ist und deren UI verbessert werden soll.

---

## Zusammenfassung

Das Feature "Plugins deaktivieren" mit einem Plugins-Register ist bereits umgesetzt. Der Code zeigt folgende Befunde:

**Vorhanden:**
- ✓ Datenmodell `PluginActivationEntry` für Plugin-Auswahl-Listeneinträge mit `IsEnabled`-Property (bindbar)
- ✓ XAML UI-Struktur mit zwei ListBoxen (SCM und KI) und CheckBox-ItemTemplate
- ✓ Code-Behind Handler für Plugin-Auswahl (`OnPluginSelectionChanged`)
- ✓ Handler `OnPluginActivationItemPreviewMouseLeftButtonDown` zur CheckBox-Selektion-Koordination
- ✓ ViewModel-Kommandos für Plugin-Auswahl (`PluginSelectedCommand`, `ScmPluginSelectedCommand`, `KiPluginSelectedCommand`)
- ✓ Methode `LoadSelectedPluginSettings()` lädt Einstellungen des ausgewählten Plugins in rechte Spalte
- ✓ Validierungsmethode `ValidierePluginAktivierung()` prüft Minimum-ein-Plugin-pro-Kategorie-Regel
- ✓ Services für Aktivierungsstatus-Persistierung (`PluginActivationService`)
- ✓ Services für Einstellungswert-Persistierung (`PluginSettingsService`)
- ✓ Unit-Tests für SettingsViewModel und PluginActivationService
- ✓ Rechte Spalte mit `ScrollViewer` und `ItemsControl` für `SelectedPluginSettings`

**Lücken (gemäß Anforderung):**
- ✗ Kontrastfehler: Textblöcke in ListBox-ItemTemplates möglicherweise nicht optimal lesbar
- ✗ Keine Kopfzeile mit Plugin-Namen im rechten Bereich
- ✗ Keine Toggle/CheckBox für Plugin-Aktivierung im rechten Bereich — Aktivierung läuft aktuell nur über CheckBoxen in den Listen
- ✗ CheckBoxen sollen aus den Listen entfernt werden

**Ausgelagerte Komponenten (nicht direkt betroffen):**
- Standard-Plugin-ComboBoxen oben links (bleiben unverändert)
- Promptvorlagen-Tab (unverändert)
- Allgemein-Tab (unverändert)

---

## Details

- [Datenmodelle](inventory/models.md) — `PluginActivationEntry`, `PromptVorlageEntry`
- [UI-Komponenten](inventory/ui.md) — `SettingsView.xaml`, `SettingsView.xaml.cs`, Struktur und Handler
- [ViewModel und Logik](inventory/viewmodel.md) — `SettingsViewModel`, Kommandos, Methoden für Plugin-Management
- [Services](inventory/services.md) — `PluginActivationService`, `PluginSettingsService`
- [Tests](inventory/tests.md) — Unit-Tests, Integration-Tests, Test-Infrastruktur

---

## Technische Notizen

### Binding und TwoWay-Kommunikation
- `PluginActivationEntry.IsEnabled` ist eine bindbare Property (nutzt `SetProperty`)
- In ListBox-ItemTemplate: `CheckBox.IsChecked="{Binding IsEnabled, Mode=TwoWay}"`
- Änderungen in der UI reflektieren sofort in `PluginActivationEntry`
- Bei Speichern: `SpeichernAsync` iteriert über alle Einträge und persistiert `entry.IsEnabled` via `PluginActivationService.SetPluginEnabledAsync()`

### Auswahl-Koordination
- `OnPluginActivationItemPreviewMouseLeftButtonDown` sorgt dafür, dass Klick auf CheckBox auch die ListBoxItem-Auswahl triggert
- Dies ist notwendig, weil ohne diesen Handler ein CheckBox-Klick nicht `SelectionChanged` auf der ListBox auslöst
- Nach der Auswahl wird `OnPluginSelectionChanged` ausgelöst, die wiederum `PluginSelectedCommand` ausführt

### Validierung und Speicherung
- `ValidierePluginAktivierung()` ist Teil von `ValidierePflichtfelder()`, wird vor Speichern geprüft
- Fehlermeldungen werden in `FehlerMeldung` Property gepuffert
- Speichern schlägt fehl, wenn Validierung fehlschlägt

### Collections und Observability
- `SourceCodeManagementPlugins` und `DevelopmentAutomationPlugins` sind `ObservableCollection<PluginActivationEntry>`
- Sie werden während `LadenAsync` (über `LadePluginAktivierungAsync`) mit aktuellen Status-Einträgen befüllt
- `SelectedPlugin` speichert den aktuellen Eintrag (aus `PluginSelectedCommand` oder Standard-Plugin-Auswahl)
- `SelectedPluginSettings` wird durch `LoadSelectedPluginSettings()` aktualisiert

---

## Abhängigkeitsübersicht

```
SettingsView.xaml
├─ SettingsViewModel
│  ├─ PluginActivationService
│  │  └─ AppEinstellungService
│  ├─ PluginSettingsService
│  │  └─ ICredentialStore
│  ├─ IPluginManager
│  ├─ PromptVorlagenService
│  └─ (weitere Services für Arbeitsverzeichnis, Design, etc.)
│
└─ SettingsView.xaml.cs (Code-Behind)
   └─ SettingsViewModel (über Bindings und Commands)

Tests
├─ SettingsViewModelTests
│  └─ (Mocks für alle Services)
└─ PluginActivationServiceTests
   └─ (Mocks für Services)
```

---

## Implementierungs-Relevant

### Einstellungsspeicherung
Aktivierungsstatus wird gespeichert unter Schlüssel:
```
plugins.enabled.{PluginPrefix}
```
Beispiel: `plugins.enabled.github`, `plugins.enabled.claude`

Wert: `"true"` oder `"false"` (als String)

### Plugin-Einstellungsspeicherung
Einstellungswerte werden gespeichert unter Schlüssel:
```
{PluginPrefix}.{FieldKey}
```
Beispiel: `github.api_key`, `claude.model`

### PluginActivationEntry-Struktur
- `PluginActivationEntry` ist immutable bis auf die `IsEnabled`-Property
- `Plugin`, `PluginName`, `PluginPrefix` sind read-only Konstruktor-Parameter
- Wird über Constructor mit `IPlugin` und `bool isEnabled` initialisiert

### XAML-Binding-Struktur für Plugins-Tab
```
Grid (3-spaltig)
├─ Column 0: ScrollViewer mit Standard-Plugins und Aktivierungslisten
│  ├─ Standard SCM-Plugin (ComboBox)
│  ├─ Standard KI-Plugin (ComboBox)
│  ├─ "Quellcodeverwaltung" (Header + ListBox für SourceCodeManagementPlugins)
│  └─ "KI" (Header + ListBox für DevelopmentAutomationPlugins)
│
└─ Column 2: ScrollViewer mit SelectedPluginSettings
   └─ ItemsControl für SelectedPluginSettings (Setting-Groups und Felder)
```

---

## Offene Prüfpunkte

1. **Kontrasttest:** Visuelles Testen der TextBlock-Farben in den ListBox-Items erforderlich
2. **E2E-Test-Anpassung:** Falls `OnPluginActivationItemPreviewMouseLeftButtonDown` geändert wird, müssen relevante E2E-Tests überprüft werden
3. **Rechtsspaltentiefe:** Überprüfung, wie tief die Einstellungsgruppen im rechten Bereich scrollbar sind (max-width/Layout)

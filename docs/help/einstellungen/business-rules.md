← [Zurück zur Übersicht](index.md)

# Einstellungen — Business Rules

## Standard-Plugin-Verwaltung

**Beschreibung:** Die Anwendung verwaltet Standard-Plugins für SCM- und KI-Operationen separat. Diese Plugins werden bei der Erstellung neuer Aufgaben oder bei automatisierten Workflows als Fallback verwendet.

**Bedingungen:**
- Ein Standard-Plugin wird als String-Name in der `AppEinstellung`-Tabelle gespeichert
- Der String-Name entspricht der `PluginName`-Eigenschaft des Plugins
- Es können mehrere SCM- und KI-Plugins gleichzeitig installiert sein
- Nicht alle Plugins müssen als Standard-Plugin fungieren

**Verhalten:**
- Beim Laden der Einstellungen: Der gespeicherte Plugin-Name wird mit der verfügbaren Plugin-Liste abgeglichen
  - Falls ein Plugin mit dem Namen existiert → wird als Standard-Plugin selektiert
  - Falls das Plugin nicht mehr existiert → wird ein Fallback verwendet (erstes Plugin der Liste)
  - Falls keine Plugins verfügbar sind → wird kein Standard-Plugin gesetzt (null)
- Beim Speichern: Der Name des gewählten Plugins wird persistiert
- Bei der Verwendung in Workflows: Das Standard-Plugin wird automatisch für neue Aufgaben verwendet, sofern der Anwender keine explizite Auswahl trifft

**Umsetzung:**
- `SettingsViewModel.LadenAsync()` — Abgleich von String-Namen mit Plugin-Objekten (Zeile 181-183 für SCM, analog für KI)
- `SettingsViewModel.SpeichernAsync()` — Speicherung des Plugin-Namens via `AppEinstellungService.SetSettingAsync()` (Zeile 226)
- `AppEinstellungService` — Konstanten `DefaultScmPluginKey` und `DefaultKiPluginKey`

## Plugin-Einstellungen-Verwaltung

**Beschreibung:** Jedes Plugin kann eine Liste von konfigurierbare Einstellungsgruppen definieren. Diese Einstellungen sind Plugin-spezifisch und werden vom Plugin selbst via `GetSettingGroups()` definiert.

**Bedingungen:**
- Ein Plugin implementiert `GetSettingGroups()` und liefert eine Liste von `PluginSettingGroup`-Objekten
- Jede Gruppe ist eine logische Zusammenfassung von zusammenhängenden Feldern (z. B. "Authentifizierung", "Limits")
- Jedes Feld hat einen eindeutigen `Key`, eine Beschreibung, einen Typ und optionale Validierungsregeln
- Einstellungswerte werden im Windows Credential Store gespeichert (für Sicherheit)

**Verhalten:**
- Beim Plugin-Wechsel: Die neuen Einstellungsgruppen werden via `GetSettingGroups()` abgerufen
  - Für jedes Feld wird der aktuelle Wert via `PluginSettingsService.GetValue()` geladen (falls vorhanden)
  - Die Felder werden in der UI gerendert, basierend auf ihrem `FieldType`
- Beim Speichern: Jeder Wert wird validiert, dann via `PluginSettingsService.SetValue()` persistiert
  - Boolean-Werte werden zu "true"/"false" Strings konvertiert
  - Andere Typen werden als String gespeichert
- Beim Laden: Alle Werte werden als String aus dem Credential Store abgerufen; die UI interpretiert sie basierend auf `FieldType`

**Umsetzung:**
- `SettingsViewModel.LadePluginEinstellungen()` — Abruf von `GetSettingGroups()` und Wertladung (Zeile 269-279)
- `SettingsViewModel.SpeicherePluginEinstellungen()` — Speicherung aller Werte (Zeile 281-293)
- `PluginSettingsService.GetValue()` / `SetValue()` — Interface zu Credential Store
- Feldtypen: `PluginSettingFieldType` Enum (Text, Secret, Url, Integer, Boolean, Enum, FilePath)

## Validierung vor dem Speichern

**Beschreibung:** Alle Einstellungswerte werden vor dem Speichern validiert. Validierungsfehler werden dem Anwender angezeigt, und der Speichervorgang wird abgebrochen.

**Bedingungen:**
- Ein Feld kann `IsRequired=true` sein (Pflichtfeld)
- Ein Feld vom Typ `Integer` muss eine gültige Ganzzahl sein (wenn nicht leer)
- Ein Feld vom Typ `Enum` muss einen Wert aus `EnumOptions` enthalten (wenn nicht leer)
- Längenwerte und Format-Validierungen sind nicht standardisiert; können vom Plugin via Custom-Logik definiert werden

**Verhalten:**
- Wenn Validierung fehlgeschlagen:
  - `FehlerMeldung` wird mit einer sprechenden Nachricht gesetzt
  - Der Speichervorgang stoppt, kein Wert wird persistiert
  - Die Nachricht wird dem Anwender angezeigt
- Wenn Validierung erfolgreich:
  - Alle Werte werden persistiert
  - Eine Erfolgsmeldung wird angezeigt
  - Die Einstellungen sind sofort verfügbar

**Umsetzung:**
- `SettingsViewModel.ValidierePflichtfelder()` — Orchestriert die Validierung (Zeile 295-299)
- `SettingsViewModel.ValidierePflichtfelderFuerSettings()` — Konkrete Validierungslogik (Zeile 301-333)
  - Zeilen 307-311: Pflichtfeld-Validierung
  - Zeilen 313-319: Integer-Typ-Validierung
  - Zeilen 321-328: Enum-Wert-Validierung

## Theme-Konsistenz bei Eingabekomponenten

**Beschreibung:** Alle Eingabekomponenten (TextBox, CheckBox, ComboBox, PasswordBox, Button) verwenden zentrale Dark-Mode-Farben aus `DarkTheme.xaml` / `LightTheme.xaml`. Dies garantiert eine konsistente Benutzeroberfläche in beiden Themes.

**Bedingungen:**
- Alle Styles verwenden `DynamicResource` statt `StaticResource` für Farben
- Komponenten-Styles sind zentral in den Theme-ResourceDictionaries definiert
- Hover- und Selected-States nutzen konsistente Akzent-Farben
- Text- und Background-Farben werden dynamisch basierend auf dem aktiven Theme bestimmt

**Verhalten:**
- Beim Design-Wechsel (z. B. von Hell zu Dunkel):
  - `DarkModeService.SetMode()` wird aufgerufen
  - Das WPF-`ResourceDictionary` wird zwischen `LightTheme.xaml` und `DarkTheme.xaml` gewechselt
  - Alle Komponenten mit `DynamicResource` Bindings aktualisieren sofort ihre Farben
  - Keine Komponente-Renderung, nur Farb-Wechsel
  - Der Theme-Wechsel erfolgt nahezu zeitlos
- Während der Benutzerinteraktion (z. B. Hover über Button):
  - WPF-Triggers in den Styles ändern die Überlagerungsfarbe (`#20FFFFFF` für leichtes Highlight)
  - Der Trigger-Zustand ist unabhängig vom aktuellen Theme

**Umsetzung:**
- `DarkTheme.xaml` / `LightTheme.xaml` — Zentrale Theme-Definition mit Farben und Styles (Zeilen 221-242 in DarkTheme für Label und CheckBox)
- `SettingsView.xaml` — Verwendet `{DynamicResource}` Bindings überall (z. B. Zeile 6, 78, 183, etc.)
- `DarkModeService.SetModeAsync()` — Wechselt das aktive ResourceDictionary

## Plugin-Einstellungen beim Plugin-Wechsel

**Beschreibung:** Wenn ein Anwender zwischen Plugins wechselt, werden die Einstellungen des neuen Plugins geladen und angezeigt, während die Einstellungen des alten Plugins erhalten bleiben.

**Bedingungen:**
- Der Anwender hat bereits ein SCM- oder KI-Plugin ausgewählt und dessen Einstellungen modifiziert
- Der Anwender wechselt zu einem anderen Plugin
- Die modifizierten Werte des bisherigen Plugins sind noch nicht gespeichert

**Verhalten:**
- Beim Wechsel zu einem neuen Plugin:
  - Die UI zeigt sofort die Felder des neuen Plugins (lazy Load)
  - Die Werte des vorherigen Plugins bleiben im ViewModel erhalten (nicht verloren)
  - Wenn der Anwender auf "Speichern" klickt, werden **alle** gemodifizierten Werte persistiert
  - Falls der Anwender auf "Verwerfen" klickt, werden **alle** Änderungen verworfen (inkl. des vorherigen Plugins)
  - Wenn der Anwender zurück zum vorherigen Plugin wechselt, sieht er seine modifizierten Werte wieder

> **Hinweis:** Dies unterscheidet sich von der häufigen UI-Praxis, Änderungen sofort beim Wechsel zu persistieren. Hier werden Änderungen erst beim expliziten "Speichern" persistiert, um Konsistenz zu wahren.

**Umsetzung:**
- `SettingsViewModel.ScmPluginSelectedCommand` / `SettingsViewModel.KiPluginSelectedCommand` — Commands für Plugin-Wechsel
- `SettingsViewModel.LoadScmPluginSettings()` / `SettingsViewModel.LoadKiPluginSettings()` — Laden der neuen Einstellungen ohne die alten zu löschen
- `SelectedScmPluginSettings` / `SelectedKiPluginSettings` — Properties die in der UI gebunden sind und bei Plugin-Wechsel aktualisiert werden
- `SpeichernAsync()` — Speichert **alle** aktuellen Einstellungen (von beiden Plugins, falls beide modifiziert wurden)

## Arbeitsverzeichnis und Standardpfad

**Beschreibung:** Das Arbeitsverzeichnis bestimmt, wo lokal geklonte Repositories gespeichert werden. Ist kein Verzeichnis konfiguriert oder ungültig, wird ein Fallback verwendet.

**Bedingungen:**
- Der Anwender gibt einen lokalen Dateisystem-Pfad ein (z. B. `C:\Projekte`)
- Das Verzeichnis muss physisch auf dem System vorhanden sein oder beschreibbar sein
- Ist kein Arbeitsverzeichnis konfiguriert, wird das Temp-Verzeichnis des Systems verwendet

**Verhalten:**
- Bei der Eingabe: Keine Validierung in der UI (der Anwender kann jeden Pfad eingeben)
- Beim Speichern: Der Pfad wird als String persistiert
- Bei der Verwendung: `ArbeitsverzeichnisSettingsService` verwaltet den Pfad
  - Falls der Pfad ungültig oder nicht beschreibbar ist, wird auf das Temp-Verzeichnis zurückgegriffen
  - Der Fallback wird im Aufgabenprotokoll dokumentiert

**Umsetzung:**
- `SettingsViewModel.Arbeitsverzeichnis` — Property mit bidirektionaler Bindung (Zeile 33-37)
- `ArbeitsverzeichnisSettingsService.SaveArbeitsverzeichnisAsync()` — Persistierung (aufgerufen in Zeile 224)
- `ArbeitsverzeichnisSettingsService.GetArbeitsverzeichnisAsync()` — Laden mit Fallback-Logik

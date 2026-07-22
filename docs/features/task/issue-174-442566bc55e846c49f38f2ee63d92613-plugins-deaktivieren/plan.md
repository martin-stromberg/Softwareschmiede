# Umsetzungsplan: Plugins deaktivieren

## Übersicht

Das Plugin-System wird um einen benutzerspezifischen, persistierten Aktivierungsstatus je Plugin erweitert. Ein neuer Einstellungs-Tab „Plugins" bündelt die bisherigen Tabs „Quellcodeverwaltung" und „KI" und zeigt zwei Listen (SCM- und KI-Plugins) mit Aktivierungs-Umschaltern sowie die plugin-spezifischen Einstellungen des gewählten Plugins. Deaktivierte Plugins werden in allen Auswahlflächen der Projekt- und Aufgabenbearbeitung ausgeblendet; bleibt genau ein Plugin einer Kategorie aktiv, entfällt die Auswahl vollständig und das Plugin wird automatisch verwendet.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Persistierung des Aktivierungsstatus | Key-Value-Einträge im bestehenden `AppEinstellung`-Store, Schlüssel `plugins.enabled.<PluginPrefix>`, Wert `"true"`/`"false"` (**Gateway** über `AppEinstellung`) | Folgt exakt dem bestehenden Muster von `PluginDefaultSettingsService`. **Keine Migration nötig**, da die `AppEinstellung`-Tabelle bereits existiert. Fehlender Schlüssel = aktiviert, wodurch der Default `Enabled = true` und neu entdeckte Plugins ohne Sonderlogik abgedeckt sind. |
| Zuständigkeit für Filterung & Persistierung | Neuer **scoped** `PluginActivationService` (Service Layer + Gateway); Aktivierungs-Filtermethoden **nicht** auf `IPluginManager` | `IPluginManager`/`PluginManager` ist als **Singleton** registriert (App.xaml.cs:239-240), DB-Zugriff erfolgt über den **scoped** `SoftwareschmiededDbContext`. DB-abhängige Methoden auf dem Singleton würden eine Captive-Dependency erzeugen. `PluginActivationService` ist scoped (wie `PluginDefaultSettingsService`) und kombiniert Discovery (`IPluginManager`) mit Persistenz. Weicht bewusst von der Wortwahl der Anforderung ab (`IPluginManager.GetEnabled…`), erfüllt aber dieselbe Fachlichkeit lifetime-korrekt. |
| Plugin-Identität als Schlüssel | `PluginPrefix` (nicht `PluginName`) | `PluginPrefix` ist der im Code durchgängig stabile Identifikator (z. B. in `PluginDefaultSettingsService`, `PluginSelectionService`). `PluginName` ist ein Anzeigename und darf sich ändern. |
| Filterung der KI-Auswahl | Zentral in `PluginSelectionService.GetAvailableKiPluginPrefixesAsync()` filtern | Diese Methode ist die einzige Quelle für die KI-Plugin-Auswahllisten (`TaskDetailViewModel`, Plugin-Auswahl-Dialog). Eine einzige Filterstelle deckt beide Aufrufer ab. |
| Nicht verwendete `PluginKonfiguration.Aktiviert` | Bleibt unangetastet; wird **nicht** als Persistenz genutzt | Die Entität `PluginKonfiguration` ist zwar ein `DbSet`, wird aber nirgends befüllt/gelesen. Ein Umbau auf diese Tabelle erforderte Seeding je entdecktem Plugin und wäre komplexer als der `AppEinstellung`-Ansatz. |
| Master-Detail-Aufbau des Plugins-Tabs | Linke Spalte: zwei gruppierte Listen (SCM/KI) mit Aktivierungs-CheckBox je Eintrag; rechte Spalte: Einstellungsgruppen des selektierten Plugins (Wiederverwendung des bestehenden `FieldTemplateSelector`) | Entspricht der in `requirement.md` beschriebenen zweispaltigen Oberfläche und wiederverwendet die vorhandene Settings-Render-Logik ohne Duplikat. |

## Programmabläufe

### Aktivierungsstatus in den Einstellungen umschalten und speichern

1. Benutzer öffnet Einstellungen → Tab „Plugins". `SettingsViewModel.LadenAsync()` lädt über `IPluginManager.GetSourceCodeManagementPlugins()` und `GetDevelopmentAutomationPlugins()` **alle** entdeckten Plugins (ungefiltert, da im Aktivierungs-UI auch deaktivierte sichtbar sein müssen).
2. Für jedes Plugin wird der aktuelle Status über `PluginActivationService.IsPluginEnabledAsync(pluginPrefix)` ermittelt und je Plugin ein `PluginActivationEntry` (mit `IsEnabled`, `PluginName`, `PluginPrefix`) erzeugt und in `SourceCodeManagementPlugins` bzw. `DevelopmentAutomationPlugins` eingefügt.
3. Benutzer klickt eine Aktivierungs-CheckBox → Bindung setzt `PluginActivationEntry.IsEnabled`; der Eintrag wird als „geändert" vorgemerkt (In-Memory, noch nicht persistiert).
4. Benutzer wählt einen Listeneintrag → `PluginSelectedCommand` lädt über `LadePluginEinstellungen(plugin)` die Einstellungsgruppen in `SelectedPluginSettings` (rechte Spalte).
5. Benutzer klickt „Speichern" → `SettingsViewModel.SpeichernAsync()` ruft für jeden geänderten Eintrag `PluginActivationService.SetPluginEnabledAsync(pluginPrefix, isEnabled)` auf; anschließend werden wie bisher die plugin-spezifischen Feldwerte über `PluginSettingsService` gespeichert.
6. `PluginActivationService.SetPluginEnabledAsync()` schreibt den Wert als `AppEinstellung`-Eintrag `plugins.enabled.<PluginPrefix>`.

Beteiligte Klassen/Komponenten: `SettingsViewModel`, `PluginActivationEntry`, `PluginActivationService`, `AppEinstellung`, `PluginSettingsService`, `SettingsView.xaml`

### Filterung der KI-Plugin-Auswahl in der Aufgabenbearbeitung

1. `TaskDetailViewModel.LadeVerfuegbarePluginsAsync()` ruft `PluginSelectionService.GetAvailableKiPluginPrefixesAsync()`.
2. `GetAvailableKiPluginPrefixesAsync()` ermittelt über `PluginActivationService.GetEnabledDevelopmentAutomationPluginsAsync()` nur die aktiven KI-Plugins und liefert deren `PluginPrefix`e.
3. `TaskDetailViewModel` füllt `VerfuegbareKiPlugins`. Ist genau ein Prefix aktiv, wird dieser gesetzt und `ZeigeKiPluginAuswahl = false` (Selector unsichtbar); bei mehr als einem Prefix `ZeigeKiPluginAuswahl = true`.

Beteiligte Klassen/Komponenten: `TaskDetailViewModel`, `PluginSelectionService`, `PluginActivationService`, `TaskDetailView.xaml`

### Single-Plugin-Verhalten im KI-Plugin-Auswahl-Dialog beim Aufgabenstart

1. Beim Start einer Aufgabe ohne gespeichertes Plugin sammelt der Aufrufer die auswählbaren Plugins über `PluginSelectionService.GetAvailableKiPluginPrefixesAsync()` (bereits auf aktive gefiltert).
2. Enthält die Liste genau einen Eintrag, wird der Dialog **nicht** angezeigt; das Plugin wird direkt verwendet und der Startablauf fortgesetzt.
3. Enthält die Liste mehr als einen Eintrag, wird der Dialog wie bisher über `PluginSelectionDialogService.ShowPluginSelectionDialogAsync()` angezeigt.

Beteiligte Klassen/Komponenten: `TaskDetailViewModel`, `PluginSelectionDialogService`, `PluginSelectionDialogViewModel`, `PluginSelectionService`

### Filterung der SCM-Plugin-Auswahl in der Projekt-/Repository-Bearbeitung

1. `RepositoryAssignViewModel.LadenAsync()` lädt die aktiven SCM-Plugins über `PluginActivationService.GetEnabledSourceCodeManagementPluginsAsync()` statt `IPluginManager.GetSourceCodeManagementPlugins()`.
2. `AvailableScmPlugins` wird gefüllt, `HasScmPlugins` gesetzt. Ist genau ein Plugin aktiv, wird es automatisch selektiert und der Selector über `HasMultipleScmPlugins = false` ausgeblendet (bestehende `HasScmPlugins`-Logik erweitert).

Beteiligte Klassen/Komponenten: `RepositoryAssignViewModel`, `PluginActivationService`, zugehörige View

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `PluginActivationService` | Klasse (Service Layer / Gateway, scoped) | Liest/schreibt den Aktivierungsstatus je Plugin aus dem `AppEinstellung`-Store und liefert gefilterte Listen aktiver SCM-/KI-Plugins. |
| `PluginActivationEntry` | Klasse (ViewModel/Editable-Entry) | Darstellbarer Listeneintrag im Plugins-Tab mit `PluginName`, `PluginPrefix`, `IsEnabled` (bindbar) und Referenz auf das zugehörige `IPlugin`. |

## Änderungen an bestehenden Klassen

### `PluginSelectionService` (Application Service, scoped)

- **Neue Abhängigkeit:** `PluginActivationService` (Konstruktor-Injektion).
- **Geänderte Methoden:** `GetAvailableKiPluginPrefixesAsync()` — liefert nur noch Prefixe **aktiver** KI-Plugins (Ermittlung über `PluginActivationService.GetEnabledDevelopmentAutomationPluginsAsync()`).

### `SettingsViewModel` (ViewModel)

- **Neue Abhängigkeit:** `PluginActivationService`.
- **Neue Eigenschaften:** `SourceCodeManagementPlugins` (`ObservableCollection<PluginActivationEntry>`); `DevelopmentAutomationPlugins` (`ObservableCollection<PluginActivationEntry>`); `SelectedPlugin` (`PluginActivationEntry?`); `SelectedPluginSettings` (`IReadOnlyList<PluginSettingGroupEntry>`) für die Detailspalte.
- **Neue Commands:** `PluginSelectedCommand` — lädt bei Selektion eines Listeneintrags dessen Einstellungsgruppen.
- **Geänderte Methoden:** `LadenAsync()` — befüllt zusätzlich die beiden Aktivierungs-Collections und initialisiert je Eintrag `IsEnabled` über `PluginActivationService.IsPluginEnabledAsync()`. `SpeichernAsync()` — persistiert vor dem Speichern der Feldwerte die geänderten Aktivierungsstatus über `PluginActivationService.SetPluginEnabledAsync()`.
- **Hinweis:** Bestehende Eigenschaften/Commands zu Default-SCM-/KI-Plugin (`DefaultScmPlugin`, `DefaultKiPlugin`, `ScmPluginSelectedCommand`, `KiPluginSelectedCommand`) bleiben erhalten; ihre UI wird in den neuen Tab verlagert (siehe SettingsView).

### `SettingsView.xaml` (View)

- **Entfernt:** Tabs „Quellcodeverwaltung" und „KI".
- **Neu:** Tab „Plugins" mit zweispaltiger Master-Detail-Oberfläche: links zwei überschriebene Listen (SCM/KI) je `ItemsControl`/`ListBox` mit Aktivierungs-CheckBox (`IsChecked` → `PluginActivationEntry.IsEnabled`) und `AutomationProperties.Name` je Eintrag für E2E-Zugriff; rechts die Einstellungsgruppen des selektierten Plugins (Wiederverwendung des `FieldTemplateSelector`). Die Default-Plugin-Auswahl (SCM/KI) wird **vollständig** in diesen Tab integriert; die bisherigen Tabs „Quellcodeverwaltung" und „KI" entfallen ersatzlos.
- **Code-Behind `SettingsView.xaml.cs`:** Selektions-Handler analog zu `OnScmPluginSelectionChanged`/`OnKiPluginSelectionChanged` für die neue Liste.

### `TaskDetailViewModel` (ViewModel)

- **Neue Eigenschaften:** `ZeigeKiPluginAuswahl` (`bool`) — steuert Sichtbarkeit des KI-Plugin-Selectors (false bei genau einem aktiven Plugin).
- **Geänderte Methoden:** `LadeVerfuegbarePluginsAsync()` — setzt zusätzlich `ZeigeKiPluginAuswahl` abhängig von der Anzahl aktiver Plugins und selektiert bei genau einem Plugin dieses automatisch. Der Aufgabenstart-Ablauf umgeht den Auswahl-Dialog, wenn nur ein aktives KI-Plugin vorhanden ist.

### `RepositoryAssignViewModel` (ViewModel)

- **Neue Abhängigkeit:** `PluginActivationService`.
- **Neue Eigenschaften:** `HasMultipleScmPlugins` (`bool`) — steuert Sichtbarkeit des SCM-Selectors.
- **Geänderte Methoden:** `LadenAsync()` — lädt SCM-Plugins über `PluginActivationService.GetEnabledSourceCodeManagementPluginsAsync()`; setzt `HasMultipleScmPlugins`.

### `IssueCreateDialogViewModel` (ViewModel)

- **Geänderte Methoden:** Der Aufbau von `VerfuegbareKiPlugins` (aktuell direkt über `IPluginManager.GetDevelopmentAutomationPlugins()`) filtert zusätzlich auf aktive Plugins (über `PluginActivationService`), damit deaktivierte Plugins nicht als Issue-Text-Generatoren erscheinen.

### `App.xaml.cs` (Composition Root)

- **Neue Registrierung:** `services.AddScoped<PluginActivationService>();`.
- Falls `RepositoryAssignViewModel` / `IssueCreateDialogViewModel` bisher nur `IPluginManager` erhielten: zusätzliche Injektion von `PluginActivationService`.

## Datenbankmigrationen

Keine.

Begründung: Der Aktivierungsstatus wird als Key-Value-Eintrag in der bereits existierenden `AppEinstellung`-Tabelle gespeichert (`plugins.enabled.<PluginPrefix>`). Es werden weder neue Tabellen noch neue Spalten benötigt.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| Aktivierungsstatus je Kategorie | Es muss mindestens ein Plugin je Kategorie (SCM bzw. KI) aktiv bleiben. Das Deaktivieren des letzten aktiven Plugins einer Kategorie wird verhindert. | Beim Speichern (bzw. beim Umschalten) Anzeige einer Fehlermeldung „Mindestens ein <Kategorie>-Plugin muss aktiv bleiben."; der Status wird nicht persistiert / zurückgesetzt. |

Hinweis: Die bestehende Feldvalidierung (`ValidierePflichtfelderFuerSettings`) bleibt unverändert bestehen.

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `plugins.enabled.<PluginPrefix>` | `AppEinstellung`-Key-Value (String `"true"`/`"false"`) | Fehlender Eintrag ⇒ aktiviert (`true`) | Persistiert den benutzerspezifischen, globalen Aktivierungsstatus je Plugin. |
| Konstante `EnabledKeyPrefix` in `PluginActivationService` | `const string` = `"plugins.enabled."` | — | Schlüssel-Präfix für die Aktivierungseinträge (analog `PluginDefaultSettingsService.DefaultKeyPrefix`). |

## Seiteneffekte und Risiken

- **Default-Plugin-Auflösung:** `IPluginManager.GetDefaultSourceCodeManagementPlugin()` / `GetDefaultDevelopmentAutomationPlugin()` durchsuchen weiterhin **alle** geladenen Plugins (Singleton, kein DB-Zugriff). Dadurch könnte ein deaktiviertes Plugin als Default aufgelöst werden. Risiko wird durch die Validierungsregel (mind. ein Plugin je Kategorie aktiv) und die Filterung in `PluginSelectionService` gemindert; die harte Default-Auflösung dient nur als Fallback, wenn keine Auswahl greift.
- **`SettingsView`-Umbau:** Das Entfernen der Tabs „Quellcodeverwaltung" und „KI" verschiebt vorhandene Bedienelemente. Bestehende E2E-Tests, die diese Tabs per Namen ansteuern (`E2E_SettingsKiPluginPersistence`, `E2E_SettingsCommandLineParameters`), müssen auf den neuen Tab „Plugins" angepasst werden.
- **Singleton/Scoped-Grenze:** `PluginActivationService` darf nicht in Singletons injiziert werden. Nur scoped Consumer (ViewModels, `PluginSelectionService`) greifen darauf zu.
- **Discovery-Timing:** Neu entdeckte Plugins sind ohne Neustart sofort aktiv (fehlender Schlüssel = aktiv); kein Seeding-Schritt nötig.

## Umsetzungsreihenfolge

1. **`PluginActivationService` anlegen**
   - Voraussetzungen: `SoftwareschmiededDbContext`, `AppEinstellung`-Entität, `IPluginManager` (alle vorhanden).
   - Beschreibung: Scoped Service mit Methoden `IsPluginEnabledAsync(pluginPrefix)`, `SetPluginEnabledAsync(pluginPrefix, enabled)`, `GetEnabledSourceCodeManagementPluginsAsync()`, `GetEnabledDevelopmentAutomationPluginsAsync()`. Persistenz über `AppEinstellung`-Key `plugins.enabled.<PluginPrefix>`, fehlender Schlüssel ⇒ aktiv.

2. **`PluginActivationService` in DI registrieren**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: `services.AddScoped<PluginActivationService>();` in `App.xaml.cs`.

3. **`PluginSelectionService` filtern**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: `PluginActivationService` injizieren; `GetAvailableKiPluginPrefixesAsync()` auf aktive KI-Plugins beschränken.

4. **`PluginActivationEntry` anlegen**
   - Voraussetzungen: `ViewModelBase` (vorhanden).
   - Beschreibung: Editable-Entry mit `PluginName`, `PluginPrefix`, `IsEnabled` und `IPlugin`-Referenz.

5. **`SettingsViewModel` erweitern**
   - Voraussetzungen: Schritte 1 und 4.
   - Beschreibung: Aktivierungs-Collections, `SelectedPlugin`, `SelectedPluginSettings`, `PluginSelectedCommand`; `LadenAsync`/`SpeichernAsync` erweitern; Validierung „mind. ein Plugin je Kategorie aktiv".

6. **`SettingsView.xaml`/`.xaml.cs` umbauen**
   - Voraussetzungen: Schritt 5.
   - Beschreibung: Tabs „Quellcodeverwaltung" und „KI" entfernen; Tab „Plugins" mit Master-Detail und Aktivierungs-CheckBoxen hinzufügen; Default-Plugin-Auswahl integrieren; Selektions-Handler im Code-Behind.

7. **`TaskDetailViewModel` Single-Plugin-Verhalten**
   - Voraussetzungen: Schritt 3.
   - Beschreibung: `ZeigeKiPluginAuswahl` einführen; `LadeVerfuegbarePluginsAsync` und Startablauf so anpassen, dass bei genau einem aktiven Plugin kein Selector/Dialog erscheint.

8. **`TaskDetailView.xaml` Sichtbarkeit binden**
   - Voraussetzungen: Schritt 7.
   - Beschreibung: KI-Plugin-Selector an `ZeigeKiPluginAuswahl` binden.

9. **`RepositoryAssignViewModel` filtern**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: SCM-Plugins über `PluginActivationService` laden; `HasMultipleScmPlugins` für Selector-Sichtbarkeit; zugehörige View binden.

10. **`IssueCreateDialogViewModel` filtern**
    - Voraussetzungen: Schritt 1.
    - Beschreibung: Aufbau von `VerfuegbareKiPlugins` auf aktive Plugins beschränken.

11. **Unit-Tests schreiben**
    - Voraussetzungen: Schritte 1, 3, 5.
    - Beschreibung: siehe Abschnitt Tests.

12. **Bestehende Tests anpassen**
    - Voraussetzungen: Schritte 3, 6.
    - Beschreibung: siehe Abschnitt „Betroffene bestehende Tests".

13. **E2E-Tests ergänzen/anpassen**
    - Voraussetzungen: Schritte 6, 8.
    - Beschreibung: siehe Abschnitt E2E-Tests.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `IsPluginEnabled_LiefertTrue_WennKeinEintragVorhanden` | `PluginActivationServiceTests` (neu) | Default = aktiv bei fehlendem Schlüssel |
| `SetPluginEnabled_PersistiertUndLiestZurueck` | `PluginActivationServiceTests` | Speichern und erneutes Lesen liefert denselben Status |
| `GetEnabledSourceCodeManagementPlugins_FiltertDeaktivierte` | `PluginActivationServiceTests` | Deaktivierte SCM-Plugins werden entfernt |
| `GetEnabledDevelopmentAutomationPlugins_FiltertDeaktivierte` | `PluginActivationServiceTests` | Deaktivierte KI-Plugins werden entfernt |
| `GetAvailableKiPluginPrefixes_LiefertNurAktive` | `PluginSelectionServiceTests` (ggf. neu) | Filterung in der KI-Auswahlquelle |
| `Laden_BefuelltAktivierungsCollections_MitStatus` | `SettingsViewModelTests` | Aktivierungs-Collections werden korrekt initialisiert |
| `TogglePlugin_UndSpeichern_PersistiertStatus` | `SettingsViewModelTests` | Umschalten + Speichern persistiert über `PluginActivationService` |
| `Speichern_VerhindertDeaktivierenDesLetztenPlugins` | `SettingsViewModelTests` | Validierungsregel „mind. ein Plugin je Kategorie" |
| `LadeVerfuegbarePlugins_VerstecktSelector_BeiEinemAktivenPlugin` | `TaskDetailViewModel`-Testklasse | `ZeigeKiPluginAuswahl == false` bei genau einem Plugin |
| Test-Hilfsmethode zum Setzen von Aktivierungsstatus in In-Memory-DB | `PluginActivationServiceTests` / bestehende Test-Helper | Bereitstellung von `AppEinstellung`-Einträgen für Filtertests |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `SettingsViewModelTests` | Konstruktor von `SettingsViewModel` erhält zusätzliche Abhängigkeit `PluginActivationService`; ggf. Mock/Fake ergänzen. `ScmPluginSelectedCommand_LaeadtSettingsGroups_…` ggf. auf neuen `PluginSelectedCommand` migrieren. |
| `PluginSelectionService`-Tests (falls vorhanden) | `PluginSelectionService`-Konstruktor erhält `PluginActivationService`; Erwartung „nur aktive Prefixe". |
| `TaskDetailViewModelTestFactory` | Muss `PluginActivationService`/erweiterte Abhängigkeiten bereitstellen. |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Benutzer deaktiviert ein SCM-Plugin im Plugins-Tab, speichert, und das Plugin erscheint anschließend nicht mehr in der Repository-/Projektauswahl | Neu: `E2E_PluginAktivierung` (konsolidiert mehrere Aspekte in einer Testmethode) | Deaktiviertes SCM-Plugin wird in der Auswahl gefiltert |
| Nur ein aktives KI-Plugin ⇒ KI-Plugin-Auswahl in der Aufgabenbearbeitung verschwindet und Start ohne Dialog | `E2E_PluginAktivierung` (weitere Phase derselben Methode oder erweiterter `E2E_PluginAuswahlUndWechsel`) | Single-Plugin-Verhalten (Selector/Dialog ausgeblendet) |
| Aktivierungsstatus bleibt nach erneutem Öffnen der Einstellungen erhalten | `E2E_PluginAktivierung` | Persistenz des Aktivierungsstatus |

### Betroffene bestehende E2E-Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `E2E_SettingsKiPluginPersistence` | Tab „KI" existiert nicht mehr; Navigation auf neuen Tab „Plugins" umstellen (`OpenKiSettings`, `FindDefaultKiPluginComboBox`). |
| `E2E_SettingsCommandLineParameters` | Falls über den KI-/SCM-Tab navigiert wird: auf Tab „Plugins" umstellen. |
| `E2E_PluginAuswahlUndWechsel` | Prüfen, ob durch Single-Plugin-Verhalten der Auswahl-Dialog im Testkontext (mehrere aktive KI-Plugins) weiterhin erscheint; ggf. Testvoraussetzung „mindestens zwei aktive Plugins" absichern. |

## Offene Punkte

Keine.

Geklärte Punkte (in den Plan eingearbeitet):

- **Umfang des Tab-Umbaus:** Die Default-Plugin-Auswahlen (SCM/KI) werden **vollständig** in den neuen Tab „Plugins" integriert; die alten Tabs „Quellcodeverwaltung" und „KI" entfallen ersatzlos. (siehe Übersicht, Designentscheidungen, `SettingsView.xaml`)
- **Letztes aktives Plugin einer Kategorie:** Das Deaktivieren wird verhindert und eine Fehlermeldung angezeigt. (siehe Validierungsregeln)
- **Scope des Aktivierungsstatus:** Global/benutzerspezifisch, **nicht** pro Projekt konfigurierbar. (siehe Konfigurationsänderungen)

# Bestandsaufnahme

## Kurzfazit

Die betroffene Persistenz- und Ausfuehrungskette fuer Codex-Kommandozeilenparameter ist klein und gut isolierbar:

1. `CodexPlugin.GetSettingGroups()` deklariert das Feld `CommandLineParameters`.
2. `SettingsViewModel` laedt daraus editierbare `PluginSettingEntry`-Objekte und speichert deren `Value` ueber `PluginSettingsService`.
3. `PluginSettingsService` persistiert unter dem CredentialStore-Key `Softwareschmiede.Codex.CommandLineParameters`.
4. `CodexPlugin.BuildProcessStartInfo()` haengt diesen gespeicherten Wert ueber `CliKiPluginBase.AppendCommandLineParameters()` an den CLI-Aufruf an.

Im untersuchten Code gibt es keine Codex-spezifische automatische Default-Setzung fuer `CommandLineParameters`. Der wahrscheinlich relevante Schwachpunkt ist die allgemeine Settings-Logik: `PluginSettingEntry` ersetzt fehlende gespeicherte Werte durch `field.DefaultValue`, und `SettingsViewModel.SpeicherePluginEinstellungen()` schreibt jeden angezeigten Einstellungswert immer zurueck. Falls fuer Codex spaeter ein DefaultValue oder eine automatisch vorgeladene Altkonfiguration existiert, kann diese Logik einen nicht vom Anwender gesetzten Wert persistieren oder nach Entfernung wiederherstellen.

## Anforderungsauszug

Quelle: `docs/features/task/issue-117-63bf4daf65314067bc5758a99758a901-automatischen-parameter-fuer-c/requirement.md`

Relevante Ziele:

- Anwenderdefinierte Codex-CLI-Parameter muessen nach Speichern, Neuladen und erneutem Oeffnen unveraendert erhalten bleiben.
- Automatische Logik darf Codex-Parameter nicht hinzufuegen, aendern oder zuruecksetzen.
- Wenn ein Anwender einen automatisch gesetzten Parameter entfernt, darf er nicht erneut hinzugefuegt werden.
- Standardparameter sind nur erlaubt, solange kein Anwenderwert existiert.
- Regressionstest fuer Nicht-Ueberschreiben anwenderdefinierter Codex-Parameter ist gefordert.

## Relevante Codepfade

### CodexPlugin

Datei: `plugins/Softwareschmiede.Plugin.Codex/CodexPlugin.cs`

- `GetSettingGroups()` deklariert zwei Gruppen: `Ausfuehrung` mit `ExecutablePath` und `CLI-Konfiguration` mit `CommandLineParameters` (`CodexPlugin.cs:32`, `CodexPlugin.cs:47`).
- Das Feld `CommandLineParameters` hat `FieldType.CommandLineParameters`, aber keinen `DefaultValue` (`CodexPlugin.cs:47` bis `CodexPlugin.cs:50`).
- `BuildProcessStartInfo()` setzt optionale Laufzeitparameter zuerst als `psi.Arguments` und ruft danach `AppendCommandLineParameters(psi, _credentialStore, PluginPrefix)` auf (`CodexPlugin.cs:79`, `CodexPlugin.cs:99`).
- `PluginPrefix` ist `Softwareschmiede.Codex`; der effektive CredentialStore-Key lautet dadurch `Softwareschmiede.Codex.CommandLineParameters`.

Bewertung:

- Codex selbst erzeugt aktuell keine Parameterwerte.
- Codex uebernimmt nur exakt den CredentialStore-Wert und fuegt ihn an vorhandene Startparameter an.
- Eine Korrektur kann Codex-spezifisch erfolgen, ohne andere KI-Plugins zu beeinflussen, wenn sie auf `Softwareschmiede.Codex` und `CommandLineParameters` begrenzt wird.

### Gemeinsame CLI-Basisklasse

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/CliKiPluginBase.cs`

- `StartCliAsync()` delegiert direkt an `BuildProcessStartInfo()` (`CliKiPluginBase.cs:28` bis `CliKiPluginBase.cs:30`).
- `AppendCommandLineParameters()` liest `credentialStore.GetCredential($"{pluginPrefix}.CommandLineParameters")` (`CliKiPluginBase.cs:115`, `CliKiPluginBase.cs:117`).
- Nur nicht-whitespace Werte werden angehaengt; whitespace-only Werte veraendern `Arguments` nicht.

Bewertung:

- Die Ausfuehrungslogik ist deterministisch, solange der CredentialStore-Wert deterministisch ist.
- Die Methode unterscheidet nicht zwischen Anwenderwert, Defaultwert und automatisch erzeugtem Wert.
- Aenderungen hier betreffen auch Claude CLI und GitHub Copilot. Fuer diese Anforderung waere eine breite Aenderung nur sinnvoll, wenn sie rein semantisch unveraendert bleibt.

### PluginSettingsService und CredentialStore

Dateien:

- `src/Softwareschmiede/Application/Services/PluginSettingsService.cs`
- `src/Softwareschmiede/Infrastructure/Services/WindowsCredentialStore.cs`

Relevante Stellen:

- `PluginSettingsService.GetValue()` liest den gebauten Key aus dem CredentialStore (`PluginSettingsService.cs:37`).
- `SetValue()` schreibt den Wert unveraendert unter denselben Key (`PluginSettingsService.cs:44`, `PluginSettingsService.cs:48`).
- `DeleteValue()` loescht denselben Key (`PluginSettingsService.cs:52`, `PluginSettingsService.cs:56`).
- `WindowsCredentialStore.GetCredential()`, `SetCredential()` und `DeleteCredential()` kapseln Windows Credential Manager (`WindowsCredentialStore.cs:44`, `WindowsCredentialStore.cs:60`, `WindowsCredentialStore.cs:86`).

Bewertung:

- Der Service bietet bereits eine Loeschoperation, wird aber vom Settings-Speichern aktuell nicht genutzt.
- `SetValue(..., string.Empty)` speichert einen leeren Credential statt den Key zu entfernen. `AppendCommandLineParameters()` ignoriert leere Werte zwar beim Ausfuehren, aber der Unterschied zwischen "nicht gesetzt" und "vom Anwender bewusst leer gesetzt" ist im aktuellen Modell nicht explizit.
- Fuer Akzeptanzkriterium "entfernter automatisch gesetzter Parameter wird nicht wieder hinzugefuegt" ist wichtig, ob leerer Wert als bewusste Anwenderentscheidung erhalten bleibt oder ob der Key geloescht wird und spaeter Defaultlogik erneut greift.

### Settings UI und ViewModel

Dateien:

- `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/PluginSettingEntry.cs`
- `src/Softwareschmiede.App/Views/SettingsView.xaml`

Relevante Stellen:

- `LadePluginEinstellungen()` erzeugt fuer jedes Feld einen `PluginSettingEntry(field, _pluginSettingsService.GetValue(plugin, field))` (`SettingsViewModel.cs:291` bis `SettingsViewModel.cs:296`).
- `PluginSettingEntry` setzt `_value = currentValue ?? field.DefaultValue ?? string.Empty` (`PluginSettingEntry.cs:46`, `PluginSettingEntry.cs:49`).
- `SpeicherePluginEinstellungen()` schreibt alle Eintraege immer via `_pluginSettingsService.SetValue(plugin, entry.Field, entry.Value)` (`SettingsViewModel.cs:303`, `SettingsViewModel.cs:312`).
- Das XAML rendert `CommandLineParameters` als TextBox mit `UpdateSourceTrigger=PropertyChanged` (`SettingsView.xaml:69`, `SettingsView.xaml:73`).

Bewertung:

- Anwenderaenderungen werden grundsaetzlich gespeichert.
- Es gibt keine Dirty-Erkennung: Auch nicht geaenderte Feldwerte werden beim Speichern persistiert.
- Die DefaultValue-Initialisierung in `PluginSettingEntry` ist fachlich fuer echte Defaults nuetzlich, kann bei `CommandLineParameters` aber problematisch sein: Ein fehlender Credential wird zu einem konkreten UI-Wert und beim Speichern persistiert.
- Eine leere TextBox wird als leerer String gespeichert, nicht geloescht. Das verhindert zwar die Ausfuehrung leerer Parameter, dokumentiert aber nicht sauber "Anwender hat Default entfernt".

### Prozessstart

Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`

- Klassischer Start: `StartCliAsync()` ruft `kiPlugin.StartCliAsync(effectiveWorkdir, optionalParameters, ct)` auf (`KiAusfuehrungsService.cs:86`, `KiAusfuehrungsService.cs:113`).
- ConPTY-Start: `StartWithPseudoConsoleAsync()` ruft ebenfalls `kiPlugin.StartCliAsync(...)` auf und baut daraus den gesendeten CLI-Befehl (`KiAusfuehrungsService.cs:203`, `KiAusfuehrungsService.cs:204`, `KiAusfuehrungsService.cs:596`).

Bewertung:

- Beide Startmodi verwenden denselben Plugin-Parameterpfad.
- Tests sollten daher bevorzugt auf Plugin-/Serviceebene absichern; ein einzelner E2E-Test reicht zur UI-Persistenz.

## Bestehende Tests

### Gute vorhandene Abdeckung

- `src/Softwareschmiede.Tests/Infrastructure/Plugins/CodexPluginTests.cs`
  - prueft Metadaten inklusive `CommandLineParameters` (`CodexPluginTests.cs:39`).
  - prueft, dass `StartCliAsync` gespeicherte Codex-Parameter in `ProcessStartInfo` uebernimmt (`CodexPluginTests.cs:106` bis `CodexPluginTests.cs:109`).

- `src/Softwareschmiede.Tests/Domain/Abstractions/CliKiPluginBaseTests.cs`
  - prueft Anhängen an leere und bestehende `Arguments` (`CliKiPluginBaseTests.cs:83`, `CliKiPluginBaseTests.cs:97`).
  - prueft keine Aenderung bei `null` (`CliKiPluginBaseTests.cs:111`).

- `src/Softwareschmiede.Tests/ServiceIntegration/CliKiPluginCommandLineParametersIntegrationTests.cs`
  - prueft Codex, Claude und GitHub Copilot mit CredentialStore-Werten (`CliKiPluginCommandLineParametersIntegrationTests.cs:14`, `CliKiPluginCommandLineParametersIntegrationTests.cs:38`, `CliKiPluginCommandLineParametersIntegrationTests.cs:50`).
  - prueft whitespace-only Codex-Wert ohne Argumentaenderung (`CliKiPluginCommandLineParametersIntegrationTests.cs:62`).

- `src/Softwareschmiede.Tests/ServiceIntegration/PluginSettingsServiceIntegrationTests.cs`
  - prueft Set/Get und Delete fuer allgemeine Plugin-Settings (`PluginSettingsServiceIntegrationTests.cs:25`, `PluginSettingsServiceIntegrationTests.cs:62`).

- `src/Softwareschmiede.Tests/App/ViewModels/SettingsViewModelTests.cs`
  - prueft Speichern von KI-Einstellungswerten allgemein (`SettingsViewModelTests.cs:204`).

- `src/Softwareschmiede.Tests/E2E/E2E_SettingsCommandLineParameters.cs`
  - prueft Sichtbarkeit des Felds und Speichern/Laden eines Codex-Werts (`E2E_SettingsCommandLineParameters.cs:20`, `E2E_SettingsCommandLineParameters.cs:36`).

- `src/Softwareschmiede.Tests/E2E/E2E_TaskExecutionCommandLineParameters.cs`
  - prueft, dass gespeicherte Codex-Parameter einen Start mit KI-Simulator nicht beeintraechtigen (`E2E_TaskExecutionCommandLineParameters.cs:20`, `E2E_TaskExecutionCommandLineParameters.cs:23`).

### Testluecken fuer diese Anforderung

- Kein Test, dass ein vom Anwender geaenderter Codex-Wert nicht durch einen Default oder automatische Logik ersetzt wird.
- Kein Test, dass das Entfernen eines Codex-Parameters dauerhaft bleibt.
- Kein Test, der zwischen fehlendem Credential, leer gespeichertem Wert und DefaultValue unterscheidet.
- Kein Codex-spezifischer SettingsViewModel-Test fuer `CommandLineParameters`; vorhandene ViewModel-Tests nutzen generische Fake-Plugins.
- Kein Regressionstest, der einen potentiell vorhandenen automatisch gesetzten Altwert migriert oder neutralisiert.

## Risiken und Umsetzungshinweise

1. **DefaultValue-Falle in `PluginSettingEntry`**
   - Wenn fuer Codex `CommandLineParameters` ein `DefaultValue` eingefuehrt oder aus einer anderen Quelle gesetzt wird, wird er beim Speichern automatisch persistiert.
   - Fuer diese Anforderung sollte Codex `CommandLineParameters` entweder keinen DefaultValue haben oder die UI sollte Defaultwerte nicht als gespeicherte Anwenderwerte behandeln.

2. **Leerer Wert vs. geloeschter Credential**
   - Aktuell schreibt `SettingsViewModel` leere Strings als Credential.
   - Loeschen per `PluginSettingsService.DeleteValue()` waere sauberer, kann aber Default-Fallbacks erneut aktivieren, falls die UI weiter `DefaultValue` verwendet.
   - Falls "bewusst leer" erhalten bleiben muss, braucht es entweder keine Default-Fallbacks fuer Codex-Parameter oder eine explizite Markierung/Policy fuer anwenderueberschriebene Defaults.

3. **Keine globale Aenderung ohne Not**
   - `CliKiPluginBase.AppendCommandLineParameters()` wird von Codex, Claude CLI und GitHub Copilot genutzt.
   - Eine Codex-spezifische Anforderung sollte primaer in `CodexPlugin`/Settings-Persistenz getestet werden, damit andere Plugin-Konfigurationen nicht unbeabsichtigt geaendert werden.

4. **Altwerte**
   - Die Anforderung nennt bereits vorhandene automatisch gesetzte, nicht anwenderdefinierte Parameter.
   - Im Code ist keine Quelle fuer solche Werte erkennbar. Falls ein konkreter Altwert bekannt ist, sollte eine gezielte Bereinigung/Ignore-Regel fuer genau diesen Codex-CredentialStore-Wert geplant werden.
   - Ohne bekannte Signatur ist eine automatische Entfernung riskant, weil echte Anwenderwerte nicht sicher unterscheidbar sind.

## Empfohlene naechste Pruefpunkte fuer die Planung

- Entscheiden, ob leere `CommandLineParameters` fuer Codex als leerer Credential gespeichert bleiben oder den CredentialStore-Key loeschen sollen.
- Falls ein konkreter automatisch gesetzter Altparameter bekannt ist: als Codex-spezifische Migrations-/Sanitizing-Regel dokumentieren und testen.
- Einen fokussierten Regressionstest ergaenzen:
  - gespeicherter Codex-Wert `--user-choice` bleibt nach Laden/Speichern unveraendert;
  - entfernter Wert bleibt leer und wird beim Start nicht angehaengt;
  - kein Codex-Default wird beim Speichern ohne Anwenderaenderung persistiert.
- Bestehende Tests fuer `CodexPluginTests`, `CliKiPluginCommandLineParametersIntegrationTests`, `SettingsViewModelTests` und optional `E2E_SettingsCommandLineParameters` erweitern statt neue breite Testinfrastruktur aufzubauen.

## Detaildokumente

Keine Detaildokumente angelegt; die Bestandsaufnahme ist vollstaendig in diesem Hauptdokument enthalten.

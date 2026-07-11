# Umsetzungsplan

## Zielbild

Codex-CLI-Parameter werden nur aus einem gespeicherten Anwenderwert verwendet. Fuer das Feld `CommandLineParameters` des Plugins mit Prefix `Softwareschmiede.Codex` darf die Settings-UI keinen `DefaultValue` oder sonstigen automatisch angezeigten Wert als Anwenderwert behandeln und beim Speichern zurueckschreiben. Ein bewusst leerer Wert bleibt als leerer gespeicherter Wert erhalten und fuehrt beim Start weiterhin zu keinen zusaetzlichen CLI-Argumenten.

## Umsetzungsschritte

1. Codex-Parameterfeld in der Settings-Pipeline eindeutig erkennen.
   - Datei: `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs`
   - Private Hilfsmethode ergaenzen, z. B. `IsCodexCommandLineParameters(IPlugin plugin, PluginSettingField field)`.
   - Bedingung: `plugin.PluginPrefix == "Softwareschmiede.Codex"` und `field.Key == "CommandLineParameters"`.
   - Die Pruefung bewusst nicht auf `FieldType` allein stuetzen, damit andere Plugins mit `CommandLineParameters` unveraendert bleiben.

2. Default-Fallback fuer Codex-CLI-Parameter beim Laden unterdruecken.
   - Datei: `src/Softwareschmiede.App/ViewModels/PluginSettingEntry.cs`
   - Konstruktor um einen optionalen Parameter erweitern, z. B. `bool useDefaultValue = true`.
   - Initialisierung aendern von `currentValue ?? field.DefaultValue ?? string.Empty` auf:
     - `currentValue`, falls vorhanden;
     - sonst `field.DefaultValue`, nur wenn `useDefaultValue == true`;
     - sonst `string.Empty`.
   - Datei: `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs`
   - In `LadePluginEinstellungen()` fuer Codex-`CommandLineParameters` `useDefaultValue: false` uebergeben.
   - Ergebnis: ein fehlender Credential wird fuer Codex-Parameter nicht zu einem automatisch vorbelegten UI-Wert.

3. Bewusst entfernte Codex-Parameter nicht durch Loeschen des Keys erneut defaultfaehig machen.
   - Datei: `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs`
   - `SpeicherePluginEinstellungen()` darf fuer Codex-`CommandLineParameters` einen leeren String weiterhin mit `_pluginSettingsService.SetValue(plugin, entry.Field, string.Empty)` speichern.
   - Keine Verwendung von `DeleteValue()` fuer dieses Feld einfuehren.
   - Begruendung: Ein leer gespeicherter Wert bildet die Anwenderentscheidung "keine Parameter" ab und verhindert, dass spaetere Default-Logik erneut einen Parameter sichtbar oder wirksam macht.

4. Codex-Plugin-Definition gegen kuenftige automatische Parameter absichern.
   - Datei: `plugins/Softwareschmiede.Plugin.Codex/CodexPlugin.cs`
   - Sicherstellen, dass das Feld `CommandLineParameters` weiterhin keinen `DefaultValue` setzt.
   - Keine Startargumente in `BuildProcessStartInfo()` ergaenzen, die nicht aus `parameters` oder dem CredentialStore-Key `Softwareschmiede.Codex.CommandLineParameters` stammen.
   - Falls waehrend der Implementierung ein bekannter Altparameter entdeckt wird, keine breite Migrationslogik einbauen; nur eine exakt signierte, Codex-spezifische Bereinigung planen und testen. Nach der aktuellen Bestandsaufnahme ist keine solche Signatur vorhanden.

5. Ausfuehrungspfad unveraendert lassen.
   - Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/CliKiPluginBase.cs`
   - Keine fachliche Aenderung an `AppendCommandLineParameters()`.
   - Die Methode ignoriert bereits `null`, leere und whitespace-only Werte und haengt nur gespeicherte konkrete Werte an.
   - Dadurch bleiben Claude CLI und GitHub Copilot unveraendert.

## Tests

1. Settings-Regression: Codex-Default wird nicht persistiert.
   - Datei: `src/Softwareschmiede.Tests/App/ViewModels/SettingsViewModelTests.cs`
   - Test ergaenzen, z. B. `SpeichernAsync_PersistiertKeinenCodexCommandLineDefault_WennKeinAnwenderwertExistiert`.
   - Setup: KI-Plugin-Mock mit `PluginPrefix = "Softwareschmiede.Codex"` und Feld `CommandLineParameters` mit `DefaultValue = "--auto-default"`.
   - Ablauf: Laden, nicht editieren, Speichern.
   - Erwartung: UI-Wert fuer das Feld ist leer; CredentialStore enthaelt nicht `"--auto-default"` fuer `Softwareschmiede.Codex.CommandLineParameters` beziehungsweise enthaelt hoechstens den explizit leeren Wert.

2. Settings-Regression: Anwenderwert bleibt unveraendert.
   - Datei: `src/Softwareschmiede.Tests/App/ViewModels/SettingsViewModelTests.cs`
   - Test ergaenzen, z. B. `SpeichernAsync_ErhaeltGeaenderteCodexCommandLineParameters`.
   - Setup: Codex-KI-Plugin-Mock mit Feld `CommandLineParameters`; optional ein abweichender `DefaultValue`, der nicht gewinnen darf.
   - Ablauf: Laden, Wert auf `"--user-choice --model custom"` setzen, Speichern, erneut Laden.
   - Erwartung: gespeicherter Wert und erneut geladener UI-Wert sind exakt `"--user-choice --model custom"`.

3. Settings-Regression: Entfernte Parameter bleiben entfernt.
   - Datei: `src/Softwareschmiede.Tests/App/ViewModels/SettingsViewModelTests.cs`
   - Test ergaenzen, z. B. `SpeichernAsync_ErhaeltLeereCodexCommandLineParameters_NachEntfernung`.
   - Setup: vorhandener Credential `Softwareschmiede.Codex.CommandLineParameters = "--auto-old"` und Codex-Feld mit optionalem `DefaultValue = "--auto-default"`.
   - Ablauf: Laden, Feld auf `string.Empty` setzen, Speichern, erneut Laden.
   - Erwartung: gespeicherter und geladener Wert bleiben leer; weder `"--auto-old"` noch `"--auto-default"` wird wiederhergestellt.

4. Codex-Plugin-Absicherung.
   - Datei: `src/Softwareschmiede.Tests/Infrastructure/Plugins/CodexPluginTests.cs`
   - Bestehenden Metadaten-Test erweitern oder neuen Test ergaenzen:
     - `CommandLineParameters` hat `DefaultValue == null`.
     - `CommandLineParameters` bleibt `IsRequired == false`.

5. Ausfuehrungs-Regression.
   - Datei: `src/Softwareschmiede.Tests/ServiceIntegration/CliKiPluginCommandLineParametersIntegrationTests.cs`
   - Vorhandenen Test `CodexPlugin_ShouldNotModifyArguments_WhenCommandLineParametersAreEmpty` beibehalten.
   - Falls noetig erweitern um einen Fall mit leer gespeichertem String:
     - Credential `Softwareschmiede.Codex.CommandLineParameters = string.Empty`.
     - Start mit optionalen Parametern `"--initial"`.
     - Erwartung: `psi.Arguments == "--initial"`.

## Auszufuehrende Testbefehle

1. Fokuslauf fuer betroffene Unit-/Integrationstests:

```powershell
dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "FullyQualifiedName~SettingsViewModelTests|FullyQualifiedName~CodexPluginTests|FullyQualifiedName~CliKiPluginCommandLineParametersIntegrationTests"
```

2. Voller Regressionstest des Testprojekts:

```powershell
dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj
```

## Akzeptanzkriterien

1. Ein vom Anwender gesetzter Wert fuer `Softwareschmiede.Codex.CommandLineParameters` bleibt nach Speichern, Neuladen und erneutem Oeffnen bytegenau erhalten.
2. Ein vom Anwender leer gesetzter Codex-Parameterwert bleibt leer und wird weder durch einen gespeicherten Altwert noch durch einen `DefaultValue` ersetzt.
3. Ohne gespeicherten Anwenderwert wird fuer Codex `CommandLineParameters` kein automatisch vorbelegter Parameter in die UI uebernommen und beim Speichern nicht als echter Parameter wirksam.
4. `CodexPlugin.BuildProcessStartInfo()` verwendet weiterhin nur die optional uebergebenen Laufzeitparameter und den gespeicherten CredentialStore-Wert; es fuegt keine weiteren Codex-Parameter hinzu.
5. Andere Plugins mit `CommandLineParameters`, insbesondere Claude CLI und GitHub Copilot, behalten ihr bestehendes Verhalten.
6. Die unter "Tests" genannten Regressionstests sind implementiert und der volle Testlauf fuer `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj` ist erfolgreich.

## Offene Punkte

Keine.

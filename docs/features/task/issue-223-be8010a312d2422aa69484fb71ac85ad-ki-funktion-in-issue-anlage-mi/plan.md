# Umsetzungsplan – KI-Funktion in Issue-Anlage mit Devin & Copilot

## Zusammenfassung

Der „Issue anlegen"-Dialog soll in der KI-Plugin-Auswahl zusätzlich zu Codex und Claude auch Devin und Copilot anbieten. Die Anzeige der Auswahl erfolgt dynamisch über `_pluginManager.GetDevelopmentAutomationPlugins()` gefiltert auf Plugins, die `IIssueTemplateTextGenerator` implementieren. Devin und Copilot implementieren dieses Interface bisher nicht und müssen erweitert werden.

## Akzeptanzkriterien und Test-Mapping

| Akzeptanzkriterium | Test / Nachweis |
|---|---|
| 1. Dialog zeigt alle vier Plugins an. | Unit: `IssueCreateDialogViewModel.Initialize` mit gemockten Plugins enthält `Softwareschmiede.Devin` und `Softwareschmiede.GitHubCopilot`. |
| 2. Devin und Copilot können ausgewählt und zum Ausfüllen verwendet werden. | Unit: `IssueCreateDialogViewModel.KiAusfuellenCommand` mit echten `DevinPlugin`/`GitHubCopilotPlugin` (Mock-Prozess) füllt den Body. |
| 3. Codex und Claude bleiben unverändert funktionsfähig. | Regressionstests für `CodexPlugin` und `ClaudeCliPlugin` bestehen weiterhin. |
| 4. Auswahl wird korrekt an die Template-Aufbereitung übergeben. | Unit: `IssueCreateDialogViewModel` ruft `FillIssueTemplateAsync` des ausgewählten Plugins auf; Plugin-Tests prüfen Aufrufparameter. |
| 5. UI-Fluss für den Anwender funktioniert. | E2E: Öffnen des Issue-Anlage-Dialogs, Sichtbarkeit aller vier KI-Plugins in der ComboBox, Auswahl und Ausfüllen mit einem Test-Plugin. |

## Implementierungsschritte

1. **`DevinPlugin` erweitern**
   - Implementiere `IIssueTemplateTextGenerator`.
   - Füge `FillIssueTemplateAsync` hinzu, das den Prompt aus `BuildIssueTemplateFillPrompt` nimmt und `devin -p "<prompt>" --respect-workspace-trust false` über `RunOneShotTextGenerationAsync` ausführt.
   - Nutze `ProcessStartInfo.ArgumentList` (nicht `Arguments`), damit mehrzeilige Prompts und Anführungszeichen korrekt übergeben werden.
   - Hänge die vom Anwender gespeicherten `CommandLineParameters` an.

2. **`GitHubCopilotPlugin` erweitern**
   - Implementiere `IIssueTemplateTextGenerator`.
   - Füge `FillIssueTemplateAsync` hinzu, das den Prompt nimmt und `copilot -p "<prompt>" -s --no-ask-user` über `RunOneShotTextGenerationAsync` ausführt.
   - Setze `GH_TOKEN` aus dem Credential Store, falls vorhanden (bereits im bestehenden `BuildProcessStartInfo` vorhanden; für One-Shot analog anwenden).
   - Hänge die vom Anwender gespeicherten `CommandLineParameters` an.
   - Nutze `ProcessStartInfo.ArgumentList`.

3. **Unit-Tests ergänzen**
   - `DevinPluginTests`: Test für `FillIssueTemplateAsync` mit gemocktem Prozess/Executable.
   - `GitHubCopilotPluginTests`: Test für `FillIssueTemplateAsync`.
   - `IssueCreateDialogViewModelTests`: Test, dass bei Verfügbarkeit von Devin und Copilot beide in `VerfuegbareKiPlugins` erscheinen und ausgewählt werden können.

4. **E2E-Test ergänzen**
   - Neuer E2E-Test (oder Erweiterung eines bestehenden) für den Issue-Anlage-Dialog.
   - Szenario: Aufgabe mit GitHub-Repo öffnen, „Issue anlegen", prüfen, dass die KI-Auswahl vier Einträge enthält.
   - Da E2E-Tests echte Plugins benötigen: Verwende ein Plugin, das `IIssueTemplateTextGenerator` implementiert (z. B. erweiterter KiSimulator oder ein Stub), um das Ausfüllen ohne echte CLI-Aufrufe zu testen.

5. **Dokumentation aktualisieren**
   - `docs/help/plugins/devin-plugin/technisch.md` um One-Shot-Modus ergänzen.
   - `docs/help/aufgaben/ablauf-technisch.md` ggf. um Devin/Copilot in Issue-Anlage ergänzen.

## Risiken und Randfälle

- **Workspace-Trust bei Devin:** `devin -p` verweigert in nicht-vertrauten Verzeichnissen den Start. Der One-Shot-Aufruf muss `--respect-workspace-trust false` enthalten, damit der Prompt in einem temporären Verzeichnis ausgeführt werden kann.
- **Copilot-Authentifizierung:** Die `copilot`-CLI benötigt ein gültiges GitHub-Token. `GitHubCopilotPlugin` setzt bereits `GH_TOKEN` aus dem Credential Store; dies wird für One-Shot analog übernommen.
- **Argument-Escaping:** Lange, mehrzeilige Prompts mit Sonderzeichen müssen über `ArgumentList` übergeben werden, nicht als verkettete `Arguments`-Zeichenkette.
- **CommandLineParameters des Anwenders:** Benutzerdefinierte Parameter werden unverändert angehängt. Parameter, die den One-Shot-Modus widersprechen (z. B. `--continue` bei Devin), können den Aufruf ungültig machen. Das ist bewusst Anwender-Konfiguration.
- **E2E-Test ohne echte CLI:** Für einen automatisierten E2E-Test wird ein `IIssueTemplateTextGenerator`-Stub benötigt, da Devin/Copilot nicht in der Testumgebung installiert sind. Der `KiSimulatorPlugin` wird daher optional um `FillIssueTemplateAsync` erweitert, um das Ausfüllen im E2E-Test simulieren zu können.

## Offene Punkte

Keine.

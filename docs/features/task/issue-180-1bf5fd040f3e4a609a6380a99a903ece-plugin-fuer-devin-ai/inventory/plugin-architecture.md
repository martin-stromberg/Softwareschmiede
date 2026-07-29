# Plugin-Architektur und Discovery

## Discovery

`PluginManager` durchsucht das konfigurierte Plugin-Verzeichnis nach DLLs, lädt Assemblies und instanziiert exportierte Typen, die `IGitPlugin` oder `IKiPlugin` implementieren. Die Zuordnung erfolgt über `IPlugin.PluginType`. Für Devin ist `PluginType.DevelopmentAutomation` erforderlich.

Es gibt keine zentrale Liste aller Plugin-Klassen und keine explizite `AddDevinPlugin`-Registrierung. Entscheidend sind daher die Plugin-DLL im Build-/Deploy-Output und der Eintrag im Testmodus-Filter, damit das Plugin in Testläufen geladen werden kann.

## Auswahl und Aktivierung

`PluginSelectionService` erhält die KI-Plugins vom `IPluginManager`, berücksichtigt Aktivierung, explizite Auswahl sowie gespeicherte globale und projektbezogene Defaults. Der eindeutige Schlüssel ist `PluginPrefix`. Das Devin-Plugin muss daher einen stabilen, eindeutigen Prefix liefern.

## Betroffene Dateien

- `src/Softwareschmiede/Infrastructure/Plugins/PluginManager.cs`
- `src/Softwareschmiede/Application/Services/PluginSelectionService.cs`
- `Softwareschmiede.slnx`
- neues Projekt unter `plugins/Softwareschmiede.Plugin.Devin/`

## Risiko

Wird die DLL nicht mitgebaut oder im Testmodus nicht zugelassen, erscheint Devin nicht in der Plugin-Auswahl, obwohl die Plugin-Klasse korrekt implementiert ist.

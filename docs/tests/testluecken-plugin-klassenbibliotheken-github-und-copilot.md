# Testlücken – Plugin-Klassenbibliotheken für GitHub und GitHub Copilot

## Identifizierte Lücken (Stand Analyse)

- [x] `PluginManager` Discovery-Pfad (fehlender Ordner, ungültige DLL, gültige Plugins, Default-Auswahl)
- [x] Typzuordnung (`PluginType`) für SCM- und Development-Automation-Plugins
- [x] Fehlerrobustheit bei defekten Assemblies ohne Host-Absturz
- [x] GitHub-Copilot-Plugin: Agentenliste, Paket-Kompatibilität, CLI-Aufrufparameter, Health-Check
- [x] GitHub-Plugin: Metadaten, zentrale CLI-Interaktionspfade
- [x] Build-/Publish-Kopie nach `plugins/` über MSBuild-Targets dokumentiert und in Build validiert

## Offene Restpunkte

- [ ] Optional: dedizierter Integrations-Smoketest, der Build-Output (`bin/**/plugins`) automatisiert auf konkrete DLL-Dateien prüft.
- [x] Für die Feature-Abnahme keine funktionalen Testlücken mehr offen.

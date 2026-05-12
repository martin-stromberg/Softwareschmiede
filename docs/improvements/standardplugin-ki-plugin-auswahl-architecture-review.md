# Architektur-Review – Standardplugin je Pluginart & KI-Plugin-Auswahl

## Ergebnis
Aktuell **nicht freigabefähig**, bis die Blocker im Ausführungspfad geschlossen sind.

## Priorisierte Findings
### Blocker
1. **UI-Auswahl wird nicht vollständig bis zur tatsächlichen Plugin-Ausführung durchgereicht.**  
   Maßnahme: End-to-End-Parameter `selectedKiPluginId` durch alle relevanten Services.
2. **Persistente Default-Konfiguration je Pluginart fehlt in den Einstellungen.**  
   Maßnahme: settings-basierte Speicherung + Validierung + Laden beim Start.

### Major
3. **Plugin-Identität nicht sauber standardisiert.**  
   Maßnahme: persistente Referenz nur über stabile technische ID (Prefix/Identifier).
4. **Fallback-Verhalten nicht überall explizit.**  
   Maßnahme: zentrale Auflösung im PluginSelectionService inkl. Logging.
5. **Testlücken für Selektionspfad.**  
   Maßnahme: Unit-/Integrationstests für „explizit gewählt“, „Default“, „Fallback“.

## Verbesserungsmaßnahmen mit Zielkriterien
- Auswahl im UI -> exakt gewählte Plugininstanz wird aufgerufen.
- Default je Pluginart ist speicherbar und restart-sicher.
- Ungültige Defaults führen nie zu App-Abbruch.
- Protokoll enthält verwendetes KI-Plugin je Prompt-Lauf.

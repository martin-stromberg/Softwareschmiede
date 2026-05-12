# Testplan – Standardplugin & KI-Plugin-Auswahl

## Ziel

Systematische Absicherung der Funktion:
- Standardplugin je Plugin-Typ speichern/laden.
- KI-Plugin im Prompt-Start vorauswählen und korrekt durchreichen.

## Umgesetzte Maßnahmen

1. **Service-Layer**
   - `PluginDefaultSettingsServiceTests` ergänzt.
   - `PluginSelectionServiceTests` ergänzt.

2. **UI-/Page-Layer**
   - `EinstellungenBaseArbeitsverzeichnisTests` um Defaultplugin-Tests erweitert.
   - `AufgabeDetailFolgePromptTests` um KI-Plugin-Auswahl-Tests erweitert.

3. **Konfiguration**
   - `ProgramDiWiringTests` um DI-Registrierungen erweitert.

4. **Regression**
   - Bestehende Unit- und Integrationstest-Suites vollständig ausgeführt.

## Abschlusskriterien

- [x] Kritische Pfade (Save/Get/Resolve/Forwarding/Error) abgedeckt.
- [x] Keine Test-Regression in bestehenden Suiten.
- [ ] Optional: UI-Render-/Interaktionstest (bUnit) für Dropdown-Binding nachziehen.

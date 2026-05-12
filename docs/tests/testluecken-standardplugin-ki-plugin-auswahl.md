# Testlücken – Standardplugin & KI-Plugin-Auswahl

## Geschlossene Lücken

- [x] **PluginDefaultSettingsService**
  - Neue Unit-Tests für Create/Update/Trim/null-Persistenz je `PluginType`.

- [x] **PluginSelectionService**
  - Neue Unit-Tests für Auflösungsreihenfolge:
    - explizite Auswahl vor gespeichertem Default,
    - gespeicherter Default vor Fallback,
    - KI-Fallback mit Copilot-Bevorzugung,
    - Default-Resolver bei leerer Plugin-Liste.

- [x] **Einstellungen-Seite (Defaultplugin speichern/laden)**
  - Tests für Laden gültiger gespeicherter Auswahl.
  - Tests für Speichern gültiger und ungültiger Auswahl.

- [x] **AufgabeDetail (KI-Plugin-Auswahl)**
  - Tests für Vorauswahl des gespeicherten KI-Standardplugins.
  - Tests für Weitergabe des ausgewählten Plugin-Prefixes an den KI-Start.
  - Test für Fehlerpfad ohne verfügbare KI-Plugins.

- [x] **DI-Absicherung**
  - `ProgramDiWiringTests` prüft Registrierung von `PluginDefaultSettingsService` und `PluginSelectionService`.

## Verbleibende Risiken

- Es gibt keinen vollständigen UI-Render-Test (bUnit) für das tatsächliche Dropdown-Rendering und Binding im Browser.
- Multi-Plugin-Ende-zu-Ende-Flows sind vor allem über Unit-Tests abgesichert; echte Plugin-Implementierungen werden dabei nicht gemeinsam ausgeführt.

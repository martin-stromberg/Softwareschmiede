# Anforderungsanalyse – Standardplugin je Pluginart & KI-Plugin-Auswahl

**Primärquelle:** `c11e26e2-b280-4797-95f3-58a8da6589f0.copilot-task.md`

## Ziele
- Pro Pluginart (z. B. SCM, DevelopmentAutomation/KI) kann genau ein Standardplugin in den Einstellungen festgelegt werden.
- Beim Absenden eines Prompts kann das KI-Plugin ausgewählt werden.
- Das konfigurierte Standard-KI-Plugin ist in der Prompt-UI vorausgewählt.
- Das tatsächlich gewählte KI-Plugin erhält den Prompt zur Ausführung.

## Scope
### In Scope
- UI-Erweiterung in Einstellungen zur Default-Auswahl je Pluginart.
- Persistenz der Default-Auswahl.
- UI-Erweiterung in der Prompt-Maske zur KI-Plugin-Auswahl.
- Durchreichen der konkreten Auswahl durch die Ausführungskette.
- Transparenz im Protokoll, welches Plugin verwendet wurde.

### Out of Scope
- Plugin-Installation/Update-Mechanismen.
- Multi-Dispatch eines Prompts an mehrere KI-Plugins gleichzeitig.

## Funktionale Anforderungen
- **FR-1:** Einstellungen zeigen je Pluginart verfügbare Plugins und erlauben die Auswahl genau eines Standardplugins.
- **FR-2:** Standardplugin wird persistent gespeichert und nach Neustart geladen.
- **FR-3:** Beim Prompt-Senden ist eine KI-Plugin-Auswahl vorhanden.
- **FR-4:** Vorauswahl in der Prompt-Maske ist das konfigurierte Standard-KI-Plugin.
- **FR-5:** Das ausgewählte KI-Plugin wird bei der Ausführung verbindlich verwendet.
- **FR-6:** Ist ein gespeichertes Standardplugin nicht verfügbar, greift ein deterministischer Fallback ohne Abbruch.

## Nicht-funktionale Anforderungen
- **NFR-1 (Robustheit):** Ungültige/fehlende Default-Einträge dürfen keine Laufzeitabbrüche verursachen.
- **NFR-2 (Nachvollziehbarkeit):** Pro Prompt-Lauf ist das verwendete KI-Plugin nachvollziehbar.
- **NFR-3 (Kompatibilität):** Bestehendes Verhalten bleibt erhalten, wenn kein Default explizit gesetzt wurde.

## Akzeptanzkriterien
1. Nutzer kann in den Einstellungen je Pluginart ein Standardplugin speichern.
2. Gespeicherte Defaults bleiben nach Neustart erhalten.
3. Prompt-Dialog zeigt KI-Plugin-Auswahl mit vorausgewähltem Standardplugin.
4. Nach Absenden wird der Prompt vom gewählten Plugin verarbeitet.
5. Bei nicht mehr vorhandenem Default-Plugin funktioniert der Lauf mit Fallback weiter.

## Domänenobjekte (fachlich)
- `PluginType` (Pluginart)
- `PluginIdentifier` (stabile technische ID, z. B. Prefix)
- `DefaultPluginSelection` (PluginType -> PluginIdentifier)
- `PromptDispatchRequest` (inkl. `SelectedKiPluginIdentifier`)

## Risiken / Abhängigkeiten
- Stabile technische Plugin-ID muss verfügbar sein (nicht nur Anzeigename).
- Aufrufkette UI -> Ausführungsservice -> Prozessservice muss Pluginauswahl transportieren.
- Persistenz muss pluginartspezifische Defaults zuverlässig speichern können.

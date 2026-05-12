# Testlücken – Feature „claude-cli-integration“

## Identifizierte Lücken (Stand Analyse)

- [x] `ClaudeCliPlugin`: Agent-Discovery über `.github`-Suchroot inklusive Fallback-Beschreibung
- [x] `ClaudeCliPlugin`: Deploy-Überschreiben vorhandener `.github`-Dateien
- [x] `ClaudeCliPlugin`: Parsing kombinierter `StdOut`/`StdErr`-Testausgaben (`Passed`/`Failed`/`Skipped`)
- [x] `ClaudeCliPlugin`: negativer Health-Check-Pfad (`claude --version` schlägt fehl)
- [x] `EntwicklungsprozessService.ProzessStartenAsync`: Abbruch bei inkompatiblem Agentenpaket vor dem Clone
- [x] `EntwicklungsprozessService.TestsAusfuehrenAsync`: Persistenz von Erfolgs-/Fehlerzusammenfassung in `ProtokollService`
- [x] `PluginManager`: Default-Auswahl wenn ausschließlich Claude-Plugin verfügbar ist
- [x] `CliKiPluginBase`: Dateiname-/Pfad-Helfer für provider-spezifische Präfixe
- [x] `GitHubCopilotPlugin`: negativer Health-Check-Pfad

## Offene Restpunkte

- [x] Für die Feature-Abnahme sind keine priorisierten Testlücken mehr offen.

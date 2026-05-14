# Architektur-Review – KI-Simulator-Plugin

## Scope
- `docs/requirements/AI-Plugin-Simulator-Requirements.md`
- `docs/architecture/ai-plugin-simulator-architecture-blueprint.md`
- `docs/architecture/ai-plugin-simulator-entity-relationship-model.md`
- Ist-Code in `src/` und `plugins/`

## Ergebnis
**Freigabestatus:** ✅ **Freigabe mit Auflagen** für Implementierungsstart.

Die Zielarchitektur passt grundsätzlich zum bestehenden Plugin-System. Vor dem Merge in einen produktiven Hauptbranch müssen die unten genannten Auflagen umgesetzt und getestet sein.

## Priorisierte Findings

### Blocker
1. **Konsistenz der Plugin-Auswahl im gesamten Ablauf muss garantiert werden.**  
   Risiko: Preflight/Tests könnten über Default/Fallback auf einem anderen Plugin laufen als der UI-Auswahl.
   - Maßnahme A: `selectedKiPluginPrefix` durchgängig für Preflight, KI-Lauf und Testlauf verwenden.
   - Maßnahme B: E2E-Test für „explizit gewähltes Plugin wird überall verwendet“ ergänzen.

2. **Normative Antworttexte müssen als stabile Quelle im Code verankert werden.**  
   Risiko: Abweichungen zwischen Doku, Code und Tests.
   - Maßnahme A: Vier Antworttexte als unveränderliche Konstanten im Simulator-Plugin definieren.
   - Maßnahme B: Snapshot-/String-Equality-Tests auf exakte Texte (inkl. Kleinschreibung und Lorem-Block).

### Major
3. **Build-/Publish-Integration des neuen Plugin-Projekts fehlt noch.**
   - Maßnahme: `Softwareschmiede.slnx`, Host-Copy-Targets und Test-Projektreferenzen um Simulator-Projekt ergänzen.

4. **Delay-Validierung muss zentral und deterministisch umgesetzt werden.**
   - Maßnahme: Gemeinsame Parse-/Clamp-Logik `0..10000`, sonst `2000`, inkl. Warnprotokoll.

5. **Nachvollziehbarkeit im Protokoll muss plugin-spezifisch bleiben.**
   - Maßnahme: In relevanten Prompt-/KI-/Test-Protokolleinträgen den effektiven `PluginPrefix` mitführen.

### Minor
6. **Timing-Toleranzen müssen testbar dokumentiert werden.**
   - Maßnahme: Testvorgabe pro Delay-Intervall ±150 ms, um flakey Tests zu vermeiden.

7. **No-Dependency-Verhalten für Health/Test klar fixieren.**
   - Maßnahme: `CheckHealthAsync` und `RunTestsAsync` ohne CLI-/Netzabhängigkeit implementieren und absichern.

## Zielkriterien (Definition of Done)
1. Simulator wird durch `PluginManager.GetDevelopmentAutomationPlugins()` gefunden.
2. Vier Antwortschritte werden in exakt definierter Reihenfolge und exakt definiertem Wortlaut ausgegeben.
3. Delay-Fälle `-1`, `10001`, `abc`, leer führen deterministisch zu `2000 ms`.
4. E2E bestätigt konsistente Verwendung des explizit gewählten KI-Plugins.
5. Build/Publish enthält `Softwareschmiede.Plugin.KiSimulator.dll`.
6. Protokolle enthalten den tatsächlich verwendeten `PluginPrefix`.

## Offene Punkte / Risiken
- Timing-Tests können auf langsamen CI-Runnern schwanken → toleranzbasiert prüfen.
- Exakte Stringtests sind absichtlich streng; bei Textänderungen müssen Doku und Tests gemeinsam angepasst werden.


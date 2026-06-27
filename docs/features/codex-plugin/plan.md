# Umsetzungsplan

## Zielbild
Es entsteht ein neues anwendungsinternes KI-Plugin `Softwareschmiede.Plugin.Codex`, das die bestehende Plugin-Landschaft um die Codex-CLI ergaenzt. Das Plugin folgt dem vorhandenen `IKiPlugin`-/`CliKiPluginBase`-Muster und wird wie die bestehenden KI-Plugins ueber Discovery, Startlogik und Health-Check in die Softwareschmiede-Anwendung eingebunden.

Die fachliche Einordnung ist bewusst die eines Softwareschmiede-Plugins, nicht eines Plugins fuer Codex selbst. Die bestehende Priorisierung der KI-Plugins bleibt erhalten; das neue Plugin erweitert die Auswahl, ersetzt aber nicht die vorhandenen Integrationen.

## Leitentscheidungen
- Projektname: `Softwareschmiede.Plugin.Codex`
- Plugin-Klasse: `CodexPlugin`
- Basisklasse: `CliKiPluginBase`
- Metadaten:
  - `PluginName`: `Codex CLI`
  - `PluginPrefix`: `Softwareschmiede.Codex`
  - `ProviderDateiPraefix`: `codex`
  - `PluginType`: `DevelopmentAutomation`
- CLI-Aufruf: standardmaessig `codex`
- Konfiguration: optionaler `ExecutablePath` als Plugin-Setting, analog zu den bestehenden CLI-Plugins
- Authentifizierung: keine Token-Umgebungsvariable per Default; ein Secret-Feld wird nur dann ergaenzt, wenn die Codex-CLI eine klar dokumentierte und stabile Umgebungsvariable fuer die Authentifizierung verlangt
- Session-Continuation: vorzugsweise aktiv, weil der vorhandene `IKiPlugin`-Contract diese Faehigkeit explizit vorsieht und `StartCliAsync(..., parameters)` bereits einen Opaque-Parameter fuer Continue-/Session-Flags traegt; die konkrete Umsetzung wird aber nur dann aktiviert, wenn die Codex-CLI dafuer eine stabile und reproduzierbare Semantik liefert

## Umsetzung
1. Neues Plugin-Projekt anlegen
   - Verzeichnis `plugins/Softwareschmiede.Plugin.Codex/`
   - Projektdatei `plugins/Softwareschmiede.Plugin.Codex/Softwareschmiede.Plugin.Codex.csproj`
   - Referenz auf `Softwareschmiede.Plugin.Contracts` und die fuer die bestehenden Plugin-Projekte verwendeten Basis-Abhaengigkeiten
   - Projekt so ausrichten, dass es als eigenstaendige DLL fuer die Plugin-Discovery gebaut werden kann

2. `CodexPlugin` implementieren
   - Datei `plugins/Softwareschmiede.Plugin.Codex/CodexPlugin.cs`
   - Ableitung von `CliKiPluginBase`
   - Metadaten und Einstellungsgruppen entsprechend den Leitentscheidungen setzen
   - `StartCliAsync` bzw. `BuildProcessStartInfo` auf den CLI-Start `codex` ausrichten
   - Optionalen `ExecutablePath`-Wert aus dem Credential-/Settings-Store lesen und als absolute Executable verwenden
   - Falls eine stabile Codex-Auth ueber Environment-Variablen sinnvoll belegt werden kann, diese nur als optionale Zusatzkonfiguration aufnehmen; andernfalls keine separate Token-Konfiguration einfuehren
   - Health-Check ueber einen nicht-invasiven CLI-Aufruf analog zu Claude/Copilot umsetzen

3. Session-Continuation final absichern
   - Auf Basis der vorhandenen Contracts und der Codex-CLI-Dokumentation entscheiden, ob `SupportsSessionContinuation()` definitiv `true` oder bewusst `false` bleibt
   - Wenn eine continue-Semantik vorhanden ist, die Implementation so gestalten, dass sie ueber den bestehenden `parameters`-Kanal abgebildet werden kann
   - Wenn keine stabile Codex-Continuation dokumentiert ist, die Faehigkeit bewusst nicht aktivieren, damit der Contract nicht vorgetaeuscht wird

4. Testprojekt verdrahten
   - `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj`
   - `ProjectReference` auf `plugins/Softwareschmiede.Plugin.Codex/Softwareschmiede.Plugin.Codex.csproj` ergaenzen
   - Damit bleibt das neue Plugin in Unit- und Discovery-Tests direkt verfuegbar

5. Unit-Tests fuer das Plugin anlegen
   - Neue Testdatei `src/Softwareschmiede.Tests/Infrastructure/Plugins/CodexPluginTests.cs`
   - Pruefen von Name, Prefix, ProviderDateiPraefix und `PluginType`
   - Pruefen der Settings, insbesondere des optionalen `ExecutablePath`
   - Pruefen des CLI-Starts mit Default-Executable `codex`
   - Pruefen des optionalen, konfigurierten Executable-Pfads
   - Pruefen von Session-Continuation gemaess der in Schritt 3 getroffenen Entscheidung
   - Pruefen des optionalen Auth-/Token-Env-Vars nur dann, wenn es in der Implementation tatsaechlich einen belastbaren Anwendungsfall dafuer gibt

6. Discovery-Tests erweitern
   - `src/Softwareschmiede.Tests/Infrastructure/Plugins/PluginManagerTests.cs`
   - Sicherstellen, dass das neue Plugin in der Development-Automation-Liste auftaucht
   - Absichern, dass die Default-Auswahl weiterhin bei Copilot bleibt
   - Wenn die bestehenden Tests im Testmodus laufen, den Testmode-Filter um `Softwareschmiede.Plugin.Codex` ergaenzen, damit das Plugin nicht versehentlich herausgefiltert wird

7. Dokumentation aktualisieren
   - `README.md` um das neue Codex-KI-Plugin ergaenzen
   - Falls im Repo weitere Plugin- oder KI-Integrationshinweise existieren, den Codex-Eintrag dort ebenfalls aufnehmen
   - Die Dokumentation so formulieren, dass die Rolle als Softwareschmiede-Plugin klar bleibt

## Akzeptanzkriterien
- `plugins/Softwareschmiede.Plugin.Codex/Softwareschmiede.Plugin.Codex.csproj` existiert und baut erfolgreich.
- `CodexPlugin` ist als eigenes `IKiPlugin` auf Basis von `CliKiPluginBase` umgesetzt.
- Die Metadaten sind konsistent gesetzt: Name, Prefix, ProviderDateiPraefix, `PluginType`.
- Der CLI-Start nutzt standardmaessig `codex` und unterstuetzt optional einen konfigurierbaren ExecutablePath.
- Eine Token-/Auth-Umgebungsvariable wird nur eingefuehrt, wenn sie fuer die Codex-CLI tatsaechlich sinnvoll und dokumentiert ist.
- Die Session-Continuation-Entscheidung ist explizit getroffen und mit dem vorhandenen Contract begruendet.
- Testprojekt, Unit-Tests und Discovery-Tests decken das neue Plugin ab.
- `README.md` und relevante Doku-Stellen nennen das neue Plugin.

## Offene Punkte
Keine.

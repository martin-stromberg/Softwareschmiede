# Testplan – Claude-CLI-Integration

## Ziel
Vollständige Schließung der in `docs/tests/testluecken-claude-cli-integration.md` dokumentierten Testlücken und Absicherung gegen Regressionen.

## Ausgangsbasis
- Lückenliste: `docs/tests/testluecken-claude-cli-integration.md`
- Ergebnis der Analyse: **alle identifizierten Lücken sind umgesetzt** (keine offenen Restpunkte)

## Abgedeckte Testbereiche (Schließung der Lücken)

1. **ClaudeCliPlugin – Agent-Discovery, Deploy, Testparsing, Health-Check**  
   Datei: `src/Softwareschmiede.Tests/Infrastructure/Plugins/ClaudeCliPluginTests.cs`
   - `.github`-Suchroot mit Fallback-Beschreibung
   - Überschreiben bestehender `.github`-Dateien beim Deploy
   - Parsing kombinierter `StdOut`/`StdErr`-Ausgaben (`Passed`/`Failed`/`Skipped`)
   - Negativer Health-Check-Pfad (`claude --version` fehlschlägt)

2. **EntwicklungsprozessService – Paketkompatibilität & Testprotokoll-Persistenz**  
   Datei: `src/Softwareschmiede.Tests/Application/Services/EntwicklungsprozessServiceTests.cs`
   - `ProzessStartenAsync`: Abbruch bei inkompatiblem Agentenpaket **vor** Clone
   - `TestsAusfuehrenAsync`: Persistenz von Erfolgs-/Fehlerzusammenfassungen in `ProtokollService`

3. **PluginManager – Default-Auswahl für KI-Plugin**  
   Datei: `src/Softwareschmiede.Tests/Infrastructure/Plugins/PluginManagerTests.cs`
   - Default-Auswahl, wenn ausschließlich Claude-Plugin verfügbar ist

4. **CliKiPluginBase – provider-spezifische Dateinamen/Pfade**  
   Datei: `src/Softwareschmiede.Tests/Domain/Abstractions/CliKiPluginBaseTests.cs`
   - Helfer für provider-spezifische Präfixe (`*.context.md`, `*-task.md`)

5. **GitHubCopilotPlugin – negativer Health-Check**  
   Datei: `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubCopilotPluginTests.cs`
   - Negativer Pfad für `copilot --version`

## Konkrete Abschluss- und Regressionsschritte

1. **Gezielte Tests für Claude-CLI-Feature ausführen**
   - `dotnet test .\src\Softwareschmiede.Tests\Softwareschmiede.Tests.csproj --filter "ClaudeCliPluginTests|EntwicklungsprozessServiceTests|PluginManagerTests|CliKiPluginBaseTests|GitHubCopilotPluginTests"`

2. **Gesamte Testsuite ausführen**
   - `dotnet test .\Softwareschmiede.slnx`

3. **Abgleich mit Lückenliste**
   - Prüfen, dass in `docs/tests/testluecken-claude-cli-integration.md` alle Punkte auf `[x]` stehen.
   - Bei neuen Befunden: Lückenliste aktualisieren und Folgepaket im Testplan ergänzen.

## Abnahmekriterien
- Alle oben genannten Tests laufen erfolgreich.
- In der Lückenliste sind keine offenen Punkte mehr vorhanden.
- Die Claude-CLI-Integration ist in Plugin-, Service- und Basisklassen-Ebene regressionssicher abgedeckt.

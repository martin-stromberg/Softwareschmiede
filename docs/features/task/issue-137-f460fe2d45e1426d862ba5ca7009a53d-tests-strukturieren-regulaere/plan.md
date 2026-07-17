# Umsetzungsplan - Tests strukturieren: regulaere Tests und OS-Schnittstellen-Tests

## Zielbild

Die Testsuite wird so strukturiert, dass reguläre Tests ohne echte OS-Schnittstellen mit `dotnet test --filter "Category!=OsInterface"` separat und stabil laufen. Tests mit echter ConPTY-, Prozess-, Clipboard-, Desktop-/E2E- oder potentiell lockanfälliger Dateisystem-Berührung werden zusätzlich als `Category=OsInterface` markiert und separat ausgeführt, ausgewertet und dokumentiert.

Die vorhandenen Kategorien `E2E` und `ConPTY` bleiben aus Kompatibilitätsgründen erhalten. `OsInterface` wird als zusätzliche, übergreifende Kategorie eingeführt. Dadurch funktionieren bestehende lokale Filter weiter, während der neue Pflichtfilter `Category!=OsInterface` die fachlich gewünschte Trennung abbildet.

## Planannahmen

1. Es wird ein zentrales Testattribut eingeführt, statt überall direkt `[Trait("Category", "OsInterface")]` zu schreiben. Das Attribut muss xUnit-kompatibel den Trait `Category=OsInterface` liefern; ein bloß von `FactAttribute` abgeleitetes Attribut reicht nicht.
2. Für bestehende E2E- und ConPTY-Tests wird `OsInterface` zusätzlich zu den vorhandenen Traits gesetzt. `E2E` und `ConPTY` werden nicht entfernt.
3. Der CI-Pflichtlauf wird blockierend auf reguläre Tests umgestellt. Der OS-Schnittstellen-Lauf wird zunächst best-effort und nicht-blockierend ausgeführt, damit bekannte Umgebungsflakiness sichtbar bleibt, aber PRs nicht mit externen Runner-Problemen blockiert.
4. `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` bleibt erhalten. Weitere Env-Flags werden nicht neu eingeführt, solange beim Umbau keine zusätzliche echte Umgebungsabhängigkeit sichtbar wird.
5. Die zentrale Dokumentation wird unter `docs/help/stabilitaet/os-interface-tests.md` abgelegt, weil die Sonderfälle nicht nur Terminal/ConPTY, sondern auch Clipboard, Prozessstart, Dateisystem und CI betreffen.
6. `/run-tests` und `/lifecycle` sind nicht als versionierter Repository-Code sichtbar. Die Repository-Umsetzung bereitet deshalb getrennte Testläufe, klare Kategorie-Konventionen und auswertbare Artefakte vor; externe Codex-Kommandos müssen diese Trennung anschließend verwenden.

## Arbeitspakete

### 1. Zentrale Testkategorie-Infrastruktur einführen

- Neue Test-Infrastruktur unter `src/Softwareschmiede.Tests/Infrastructure/Testing/` anlegen.
- `OsInterfaceFactAttribute` und `OsInterfaceTheoryAttribute` bereitstellen.
- Einen xUnit Trait-Discoverer implementieren, der für beide Attribute `Category=OsInterface` liefert.
- Konstanten für Kategorie-Namen an zentraler Stelle halten, z. B. `TestCategories.OsInterface`, damit Schreibfehler vermieden werden.
- Einen kleinen Selbsttest ergänzen, der mindestens indirekt absichert, dass die OS-Attribute für `dotnet test --filter "Category=OsInterface"` sichtbar sind. Falls ein direkter Discoverer-Test zu fragil ist, wird die Absicherung über einen dokumentierten Filterlauf in den Testkommandos vorgenommen.

### 2. Bestehende OS-nahe Tests kategorisieren

- Alle Klassen unter `src/Softwareschmiede.Tests/E2E/` zusätzlich als `OsInterface` markieren. Vorhandene `[Trait("Category", "E2E")]` bleiben bestehen.
- `src/Softwareschmiede.Tests/ServiceIntegration/CliEmbeddingServiceIntegrationTests.cs` zusätzlich als `OsInterface` markieren. Vorhandenes `ConPTY` bleibt bestehen.
- Tests mit direkter echter PseudoConsole-Erzeugung prüfen und entweder entkoppeln oder als `OsInterface` markieren:
  - `src/Softwareschmiede.Tests/App/Controls/TerminalControlTests.cs`
  - `src/Softwareschmiede.Tests/Infrastructure/Terminal/PseudoConsoleSessionTests.cs`
  - `src/Softwareschmiede.Tests/Infrastructure/Terminal/PseudoConsoleSessionTests_WritePromptAsync.cs`
  - `src/Softwareschmiede.Tests/Application/Services/CliProcessManagerTests_LaufStatus.cs`
  - `src/Softwareschmiede.Tests/Application/Services/PromptZeitVersandServiceTests.cs`
- Clipboard-Tests in `src/Softwareschmiede.Tests/App/Controls/TerminalControlTests.ClipboardPaste.cs` zunächst als `OsInterface` einstufen, solange sie `System.Windows.Clipboard` direkt verwenden.
- Integrationstests mit echten Dateisystem-/Cleanup-Risiken in `src/Softwareschmiede.IntegrationTests/` prüfen. Nur Tests mit echter OS-Abhängigkeit oder bekannten Lock-Risiken bekommen `OsInterface`; rein deterministische Temp-Verzeichnis-Tests bleiben regulär.

### 3. Reguläre Tests von echter ConPTY und Prozessstart entkoppeln

- `TaskDetailViewModelTestFactory` so ändern, dass sie für reguläre Tests keinen `KiAusfuehrungsService` mit Default-`Win32PseudoConsoleProcessLauncher` erstellt.
- Die Factory erhält einen optionalen `IPseudoConsoleProcessLauncher` oder einen optionalen `KiAusfuehrungsService`. Der Default verwendet `SimulatedPseudoConsoleProcessLauncher` oder einen deterministischen Fake.
- ViewModel-Tests, die nur Status, Commands, Protokolle oder Navigation prüfen, verwenden den OS-freien Default.
- Tests, deren Testziel ausdrücklich echter Prozessstart oder echte ConPTY ist, verwenden die echte Implementierung nur noch in `OsInterface`-Tests.
- Race-anfällige Prüfungen rund um `IsRunning` werden über deterministisch steuerbare Fakes statt echte Prozesslaufzeit synchronisiert.

### 4. TerminalControl-Tests OS-frei machen, wo möglich

- `TerminalControlTests.CreateSession` von `PseudoConsole.Create(1, 1)` auf `NullPseudoConsoleHandle.Instance` umstellen, sofern der Test keine echte Resize-/ConPTY-Funktion prüft.
- Falls Zugänglichkeit aus dem Testprojekt im Weg steht, die bestehende interne Sichtbarkeit über `InternalsVisibleTo` prüfen oder einen Testhelper im Produktiv-/Testprojekt ergänzen, der eine OS-freie `PseudoConsoleSession` erzeugt.
- Die regulären TerminalControl-Tests für Stream-Verhalten, BufferChanged-Bindung, Session-Wechsel und Fehlerlogging bleiben danach ohne `OsInterface`.
- Clipboard-Paste-Tests bleiben zunächst `OsInterface`, weil sie echte Windows-Clipboard-API und STA/MTA-Verhalten prüfen. Eine spätere Produktivcode-Abstraktion für Clipboard kann daraus zusätzliche reguläre Tests machen.

### 5. PseudoConsoleSession- und Service-Tests entkoppeln oder klassifizieren

- Tests, die nur Parser-, Buffer-, RuntimeStatus-, Dispose- oder Stream-Verhalten von `PseudoConsoleSession` prüfen, auf `NullPseudoConsoleHandle.Instance` umstellen.
- Tests, die echte ConPTY-Handle-Erzeugung, Resize gegen Windows-ConPTY oder echten Prozessstart prüfen, als `OsInterface` markieren.
- `KiAusfuehrungsServiceTests` und angrenzende Service-Tests darauf prüfen, ob sie bereits den `SimulatedPseudoConsoleProcessLauncher` verwenden. Reguläre Tests müssen bei diesem oder einem Fake bleiben.
- `CliProcessManagerTests_LaufStatus` und `PromptZeitVersandServiceTests` auf OS-freie Sessions umstellen, wenn nur Status-/Prompt-Verhalten geprüft wird.

### 6. Testauswertung und Hilfsskript trennen

- `scripts/Run-AllTestsIndividually.ps1` um eine Kategorie-Steuerung erweitern:
  - regulärer Lauf: `Category!=OsInterface`
  - OS-Schnittstellen-Lauf: `Category=OsInterface`
- Retry-Logik so begrenzen, dass sie nicht reguläre Testfehler kaschiert. Zulässig bleibt Retry für klar erkannte Infrastrukturfehler im OS-Schnittstellen-Lauf oder für Testhost-/Build-Artefakt-Recovery, sofern diese separat als Infrastruktur-Recovery ausgewiesen wird.
- Die Ausgabe des Skripts um getrennte Zusammenfassungen erweitern:
  - reguläre Fehler
  - OS-Schnittstellen-Fehler
  - Infrastruktur-/Ausführungsfehler
- Falls das Skript weiterhin beide Testprojekte automatisch findet, muss der Filter auf jedes Projekt konsistent angewandt werden.

### 7. CI zweiteilen

- `.github/workflows/test.yml` Pflichtlauf auf den neuen Filter umstellen:

```powershell
dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --no-build -c Debug --filter "Category!=OsInterface" --logger "trx;LogFileName=test-results-regular.trx" --logger "console;verbosity=normal"
```

- Separaten OS-Schnittstellen-Job oder separaten Schritt ergänzen:

```powershell
dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --no-build -c Debug --filter "Category=OsInterface" --logger "trx;LogFileName=test-results-os-interface.trx" --logger "console;verbosity=normal"
```

- Der OS-Schnittstellen-Lauf wird mit `continue-on-error: true` oder als eigener nicht-blockierender Job geplant.
- TRX-Artefakte getrennt hochladen, damit reguläre und OS-Schnittstellen-Ergebnisse separat sichtbar sind.
- Kommentare im Workflow von `E2E`/`ConPTY`-Ausschluss auf die neue `OsInterface`-Trennung aktualisieren.
- Prüfen, ob `src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj` ebenfalls in CI aufgenommen werden soll. Falls nicht, wird diese Entscheidung in der Dokumentation transparent gemacht; die Kategorie-Konvention gilt trotzdem auch dort.

### 8. Dokumentation aktualisieren

- Neue Datei `docs/help/stabilitaet/os-interface-tests.md` erstellen mit:
  - Definition regulärer Tests und OS-Schnittstellen-Tests
  - Kategorie-Konvention `Category=OsInterface`
  - Beispiele für lokale Befehle
  - CI-Verhalten: blockierender regulärer Lauf, best-effort OS-Lauf
  - bekannte Fehlerbilder: ConPTY-Verfügbarkeit, `CLIPBRD_E_CANT_OPEN`, Dateisystem-Locks, Testhost-/Build-Artefakt-Rennen
  - gültige Env-Flags, insbesondere `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`
  - Retry-Regeln
- `docs/CI_CD.md`, `README.md` und `CLAUDE.md` prüfen und Testabschnitte auf die neue Trennung aktualisieren.
- Kommentare in E2E-Testdateien aktualisieren, die aktuell nur `Category!=E2E` oder `Category!=ConPTY` nennen.

### 9. Externe `/run-tests`- und `/lifecycle`-Anpassung vorbereiten

- Da keine versionierte Implementierung im Repository sichtbar ist, wird im Repository keine nicht vorhandene Kommando-Logik geändert.
- In `docs/help/stabilitaet/os-interface-tests.md` und optional in `scripts/Run-AllTestsIndividually.ps1` werden die erwarteten Ergebnisgruppen so beschrieben, dass externe Runner sie übernehmen können.
- Erwartete Regel für `/run-tests`: reguläre Fehler bilden die blockierende Fehlermenge; OS-Schnittstellen-Fehler werden separat gelistet.
- Erwartete Regel für `/lifecycle`: automatische Iterationsentscheidungen berücksichtigen nur reguläre Fehlschläge; OS-Schnittstellen-Fehler werden dokumentiert, aber nicht als regulärer Regressionsfehler gezählt.

## Validierung

1. `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"` ausführen.
2. Falls CI oder lokale Umgebung es erlaubt: `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category=OsInterface"` ausführen und Ergebnis separat dokumentieren.
3. Falls IntegrationTests kategorisiert oder in Skripte aufgenommen wurden: dieselben Filter für `src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj` prüfen.
4. `scripts/Run-AllTestsIndividually.ps1` mindestens im regulären Modus ausführen oder, falls zu teuer, mit einem engen Projekt-/Namensfilter smoke-testen.
5. In der CI-Konfiguration prüfen, dass reguläre TRX-Artefakte und OS-Schnittstellen-TRX-Artefakte getrennt hochgeladen werden.
6. Dokumentation gegen die tatsächlichen Kommandos und Env-Flags gegenlesen.

## Risiken und Gegenmaßnahmen

- **xUnit-Trait-Discoverer falsch registriert:** Als Fallback direkte `[Trait("Category", "OsInterface")]` zusätzlich setzen oder zunächst direkte Traits verwenden. Entscheidend ist der funktionierende `dotnet test`-Filter.
- **Bestehende lokale Filter brechen:** `E2E` und `ConPTY` bleiben erhalten; `OsInterface` wird ergänzt statt ersetzt.
- **Zu viele Tests werden als OS-nah eingestuft:** Tests mit reiner Logik werden aktiv auf `NullPseudoConsoleHandle`, Simulationen oder Fakes umgestellt, statt pauschal aus dem regulären Lauf herauszufallen.
- **OS-Lauf wird in CI zu laut:** Er bleibt initial best-effort und separat reportet. Erst nach Stabilisierung sollte entschieden werden, ob Teile davon blockierend werden.
- **Externe Codex-Kommandos bleiben unverändert:** Repository-Artefakte liefern klare Kommandos und getrennte Ergebnisstruktur; die tatsächliche `/run-tests`-/`/lifecycle`-Anpassung muss außerhalb dieses Repositories erfolgen, falls diese Kommandos dort implementiert sind.

## Offene Punkte

- Keine Nutzerentscheidung ist vor der Umsetzung zwingend erforderlich.
- Nach der ersten Umsetzung sollte entschieden werden, ob `src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj` in den GitHub-Actions-Pflichtlauf aufgenommen wird oder nur lokal bzw. in einem separaten Workflow läuft.
- Eine spätere Entscheidung bleibt offen, ob der best-effort OS-Schnittstellen-Lauf langfristig teilweise blockierend werden soll, sobald die Umgebung stabil genug ist.

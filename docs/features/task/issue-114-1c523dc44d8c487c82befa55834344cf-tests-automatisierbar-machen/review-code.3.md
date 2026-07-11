# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### `.claude/settings.json`

- **Namenskonventionen und Einheitlichkeit** — Im neu hinzugefügten `PreToolUse`-Hook-Block sind die Zeilen `"if"` und `"statusMessage"` (Zeilen 123–124) mit Tabs eingerückt, während der gesamte Rest der Datei Leerzeichen verwendet. Der parallel aufgebaute `PostToolUse`-Block (Zeilen 136–137) ist demgegenüber durchgehend mit Leerzeichen eingerückt.

  Empfehlung: Die Tab-Einrückung in Zeilen 123–124 durch dieselbe Leerzeichen-Einrückung wie die umgebenden Zeilen ersetzen, damit die Datei einheitlich formatiert ist.

### `.claude/hooks/dotnet_lock.py` / `.claude/hooks/release_build_lock.py`

- **Fehlerbehandlung (fehlende Vorbedingungsprüfung vor kritischer Operation)** — `dotnet_lock.release()` (Zeile 82–83) entfernt das Lock-Verzeichnis bedingungslos per `shutil.rmtree`, ohne zu prüfen, ob der aufrufende Prozess das Lock tatsächlich hält (kein Abgleich mit der `owner.txt`). `release_build_lock.py` ruft dies im `PostToolUse` ebenfalls bedingungslos auf. Damit bleibt ein Restfenster der ursprünglich gefixten Race Condition: Erwirbt `build_before_test.py` das Lock nach `TIMEOUT_SECONDS` nicht (`got_lock = False`) und baut ungeschützt weiter, hält zu diesem Zeitpunkt ein anderer Prozess (z. B. der Stop-Hook `test-csharp-startup.ps1`) das Lock. Nach Abschluss von `dotnet test` löscht `release_build_lock.py` dann das fremde Lock des noch laufenden Stop-Hook-Builds — genau die konkurrierende Build-Situation, die der Lock verhindern soll, wird wieder möglich.

  Empfehlung: In `release()` nur freigeben, wenn das Lock dem eigenen Prozess gehört (z. B. `owner.txt` gegen die eigene PID prüfen), bzw. in `release_build_lock.py` nur freigeben, wenn der zugehörige `build_before_test.py`-Lauf das Lock erworben hatte. Alternativ die bewusste Kopplung „PreToolUse erwirbt / PostToolUse gibt frei" so absichern, dass ein nicht erworbenes Lock nicht fremd freigegeben wird.

### `src/Softwareschmiede.Tests/E2E/WpfTestBase.cs` (`WpfTestBase`)

- **Fehlerbehandlung (Diagnose greift nicht in allen Fehlerfällen)** — In `LaunchApp` (Zeilen 97–105) wird die neue Startup-Log-Diagnose nur ausgewertet, wenn `CheckAppStartupException()` einen Treffer liefert. Ist das Hauptfenster nicht verfügbar (`HasExited`/`MainWindowHandle == IntPtr.Zero`), aber im Log steht keine `[ERR]`/`[FTL]`-Zeile (Absturz ohne geloggte Signatur oder reiner Hänger), fällt der Code stillschweigend durch, wartet `Thread.Sleep(2000)` und gibt den bereits beendeten Prozess zurück. Der Aufrufer (`app.GetMainWindow(...)!`) läuft dann wieder in einen unspezifischen Folgefehler (Timeout/NullRef) — also genau den opaken Zustand, den die neue Diagnose eigentlich vermeiden soll.

  Empfehlung: Auch im Fall „Fenster fehlt und keine Startup-Exception gefunden" eine aussagekräftige `InvalidOperationException` werfen (mit Hinweis, dass der Prozess beendet ist bzw. kein `[ERR]/[FTL]` im Log stand), statt einen beendeten Prozess an den Aufrufer zurückzugeben.

### `src/Softwareschmiede.Tests/E2E/AppStartupLogInspector.cs` (`AppStartupLogInspector`)

- **Fehlerbehandlung (Randfall bei Log-Rotation)** — `Snapshot` merkt sich die Länge der zum Snapshot-Zeitpunkt neuesten Log-Datei; `GetNewEntries` wählt jedoch beim späteren Auslesen erneut die *dann* neueste Datei (`FindLatestLogFile`, `OrderByDescending(LastWriteTimeUtc)`). Wird zwischen Snapshot und Auswertung eine neue Log-Datei angelegt (Serilog-Rolling nach Datum/Größe, z. B. Tageswechsel oder neuer Prozess), wird der Byte-Offset der *alten* Datei auf die *neue* Datei angewandt. Ist die neue Datei bereits über den Offset hinaus gewachsen, werden deren erste `offset` Bytes übersprungen — u. U. genau die Startup-`[ERR]`-Zeile, die erkannt werden soll. (Bei kürzerer neuer Datei greift der Reset auf 0 in Zeile 36 korrekt.)

  Empfehlung: In `Snapshot` neben der Länge auch den Dateinamen/-pfad merken und in `GetNewEntries` den Offset nur anwenden, wenn dieselbe Datei ausgewertet wird; bei abweichender (neuer) Datei ab 0 lesen.

## Geprüfte Dateien

- `.claude/hooks/build_before_test.py`
- `.claude/hooks/release_build_lock.py`
- `.claude/hooks/dotnet_lock.py` (mitgeprüft als gemeinsame Lock-Implementierung der geänderten Hooks)
- `.claude/hooks/test-csharp-startup.ps1`
- `.claude/settings.json`
- `CLAUDE.md`
- `src/Softwareschmiede.Tests/E2E/WpfTestBase.cs`
- `src/Softwareschmiede.Tests/E2E/AppStartupLogInspector.cs`
- `src/Softwareschmiede.Tests/E2E/AppStartupLogInspectorTests.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_AutoStartCli.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_CreateNewTaskNavigation.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_TaskDetailNavigation.cs`
- `src/Softwareschmiede.Tests/E2E/ProjectDetailE2ETests.cs`

## Testing 
- Always run a full build before running tests. Never use --no-build. If E2E or unit tests fail, verify the build succeeded first before diagnosing as flakiness or pre-existing failures.
- This project requires .NET Desktop workload to run tests. Verify the correct test configuration before running; do not repeatedly retry a config the user has already flagged as broken.
- When a WPF E2E test fails with "element not found" / timeout in `WpfTestBase.WaitForElement` or `WaitWhileMainHandleIsMissing`, do NOT default to "environment flakiness / no interactive desktop session" as the explanation. First check the launched app's own log file (`src/Softwareschmiede.App/bin/<Config>/<TargetFramework>/logs/softwareschmiede-*.log` of the test run's process) for a startup exception (e.g. `[ERR] MainWindow konnte nicht angezeigt werden.`). A crash during `App.xaml.cs` startup (e.g. XamlParseException from a bad resource path) looks identical from the test's perspective to a genuinely missing/slow window, but has a completely different, fixable root cause. Only fall back to the "no interactive session" explanation once the app log confirms the window/process actually started cleanly and no exception was thrown.


## Sub-Agent / Lifecycle Workflow section

- Never trust sub-agent completion or test-pass reports at face value. Independently verify build cleanliness and test results (e.g., confirm files were actually created, code was actually removed) before reporting success to the user.

## Self-Hosting-Risiko: NIEMALS Softwareschmiede.App.exe beenden

Dieses Projekt wird mit sich selbst weiterentwickelt: Die laufende Claude-Code-Session (du) wird typischerweise **innerhalb** einer laufenden `Softwareschmiede.App.exe`-Instanz gehostet/gestartet (Desktop-Installation, z. B. `C:\Users\<user>\Desktop\Softwareschmiede\Softwareschmiede.App.exe`, nicht das Build-Ausgabeverzeichnis im Repo). Das bedeutet: **jede** laufende `Softwareschmiede.App.exe`, die du oder ein Sub-Agent auf dem System findest, könnte genau der Prozess sein, der dich gerade ausführt.

**Konkret bereits passiert:** Ein Sub-Agent stieß beim `dotnet build` auf einen Datei-Lock durch eine laufende `Softwareschmiede.App.exe` (MSB3027-Fehler beim Kopieren der DLL) und versuchte vermutlich, den blockierenden Prozess zu beenden, um den Lock aufzulösen. Dabei wurde die Instanz beendet, die die Haupt-Session hostete — die Session wurde dadurch unbewusst neu gestartet, der laufende Hintergrund-Task verwaist (`status: stopped`, Notification erst nach manueller Wiederaufnahme).

**Regeln:**
- Beende **niemals** eigenständig einen `Softwareschmiede.App.exe`-Prozess, auch nicht um einen Build-Lock aufzulösen — weder direkt noch über einen Sub-Agenten.
- Trifft ein `dotnet build` auf einen MSB3027/MSB3026-Kopierfehler wegen einer gesperrten `Softwareschmiede.App.exe`-DLL, informiere den Anwender und bitte ihn, die blockierende Instanz selbst zu schließen (er kann anhand von Fenstertitel/PID/Startzeit beurteilen, ob es eine unwichtige Test-Instanz oder die Host-Instanz ist — das kannst du von außen nicht sicher unterscheiden).
- Weiche stattdessen auf einen Teil-Build aus, der das App-Projekt nicht benötigt (z. B. `dotnet build src/Softwareschmiede/Softwareschmiede.csproj` für Domain-/Service-/Migrationscode), und dokumentiere transparent, dass die volle Verifikation (inkl. App-abhängiger Tests) noch aussteht.
- Verwaiste, aber eindeutig unabhängige Prozesse (z. B. `vstest.console` ohne Fenster) sind unkritisch und dürfen nach Rückfrage beendet werden — das Risiko betrifft ausschließlich `Softwareschmiede.App.exe`.
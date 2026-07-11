# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

- [x] `AppStartupLogInspector` (internal static Hilfsklasse) — angelegt (`src/Softwareschmiede.Tests/E2E/AppStartupLogInspector.cs`)
  - [x] Methode `Snapshot(string)` (`internal static long`) — vorhanden
  - [x] Methode `ReadLatestLog(string)` (`internal static string`) — vorhanden
  - [x] Methode `GetNewEntries(string, long)` (`internal static string`) — vorhanden, öffnet mit `FileShare.ReadWrite` und liest ab Offset
  - [x] Methode `CheckAppStartupException(string)` (`internal static string?`) — vorhanden, filtert `[ERR]`/`[FTL]`
  - [x] Hilfsmethode `FindLatestLogFile` — wählt neueste `softwareschmiede-*.log` nach `LastWriteTimeUtc`
- [x] `AppStartupLogInspectorTests` (xUnit Testklasse) — angelegt (`src/Softwareschmiede.Tests/E2E/AppStartupLogInspectorTests.cs`)
  - [x] `CheckAppStartupException_ErkenntMainWindowFehler` — vorhanden
  - [x] `CheckAppStartupException_OhneFehler_LiefertNull` — vorhanden
  - [x] `GetNewEntries_LiestNurInhaltNachOffset` — vorhanden
  - [x] `ReadLatestLog_KeinVerzeichnis_LiefertLeer` — vorhanden
  - [x] `ReadLatestLog_WaehltNeuesteDatei` — vorhanden
- [x] Feld `_appLogDirectory` (`string?`) in `WpfTestBase` — vorhanden
- [x] Feld `_appLogOffset` (`long`) in `WpfTestBase` — vorhanden
- [x] Methode `ResolveAppLogDirectory()` (`private static string`) in `WpfTestBase` — vorhanden, leitet `<exe-Verzeichnis>/logs` ab
- [x] Methode `GetLatestAppLogContent()` (`protected string`) in `WpfTestBase` — vorhanden
- [x] Methode `CheckAppStartupException()` (`protected string?`) in `WpfTestBase` — vorhanden
- [x] Methode `LaunchApp(bool)` in `WpfTestBase` — erweitert: Offset-Snapshot vor Start; nach `WaitWhileMainHandleIsMissing` Prüfung auf `HasExited`/`MainWindowHandle == Zero`; wirft `InvalidOperationException` mit Log-Auszug
- [x] Methode `Dispose()` in `WpfTestBase` — erweitert: `WaitForProcessExit(processId)` wartet auf vollständigen Prozess-Exit ohne Kill (Debug-Output bei Timeout)
- [x] Hilfsmethode `WaitForProcessExit(int?)` in `WpfTestBase` — vorhanden, kein Kill
- [x] Build-Hook `build_before_test.py` — erweitert: `_is_app_running()` erkennt laufende `Softwareschmiede.App.exe` per `tasklist` und warnt vor MSB3027/MSB3026, ohne den Prozess zu beenden

## Offene Aufgaben

Keine.

## Hinweise

- **Filter-Umfang `CheckAppStartupException`:** Die Plantabelle „Neue Klassen" beschreibt das Filtern von `[ERR]/[FTL]/bekannte Signaturen`. Die Umsetzung filtert ausschließlich `[ERR]`/`[FTL]`-Zeilen. Da Serilog die bekannten Signaturen (`MainWindow konnte nicht angezeigt werden.`, `Fehler beim Starten der Anwendung.`) auf `[ERR]`/`[FTL]`-Ebene protokolliert, werden sie dennoch erfasst — der abgesicherte Test `CheckAppStartupException_ErkenntMainWindowFehler` bestätigt das. Funktional vollständig; die explizite String-Signatur-Prüfung entfällt bewusst als Vereinfachung.
- **Dispose-Timeout:** Der Prozess-Exit-Timeout ist mit 10s fest verdrahtet (`WaitForProcessExit`); der Plan nennt lediglich „einen Timeout". Konform.
- **E2E-Fehlerpfad:** Wie im Plan festgehalten wird der neue Fehlerpfad (Startup-Exception → Klartext) deterministisch nur über die Unit-Tests von `AppStartupLogInspector` abgedeckt; ein realer `XamlParseException`-Crash ist nicht erzwingbar. Kein neuer E2E-Test nötig.

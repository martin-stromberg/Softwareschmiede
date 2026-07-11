# Tasks: Tests automatisierbar machen

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | `AppStartupLogInspector` (internal static) anlegen mit Snapshot-, Read-ab-Offset- und Fehlerzeilen-Filter-Funktionen | Offen | — |
| 2 | Logik | Bekannte Fehlersignaturen (`[ERR]`, `[FTL]`, "MainWindow konnte nicht angezeigt werden.", "Fehler beim Starten der Anwendung.") in `AppStartupLogInspector` definieren | Offen | — |
| 3 | Logik | Lesen der neuesten `softwareschmiede-*.log` mit `FileShare.ReadWrite` implementieren | Offen | — |
| 4 | Test-Infrastruktur | `ResolveAppLogDirectory()` in `WpfTestBase` ergänzen | Offen | — |
| 5 | Test-Infrastruktur | Felder `_appLogDirectory` und `_appLogOffset` in `WpfTestBase` ergänzen | Offen | — |
| 6 | Test-Infrastruktur | `GetLatestAppLogContent()` in `WpfTestBase` ergänzen | Offen | — |
| 7 | Test-Infrastruktur | `CheckAppStartupException()` in `WpfTestBase` ergänzen | Offen | — |
| 8 | Test-Infrastruktur | `LaunchApp` um Offset-Snapshot + Startup-Exception-Prüfung (wirft `InvalidOperationException` mit Log-Auszug) erweitern | Offen | — |
| 9 | Test-Infrastruktur | `Dispose` um Warten auf vollständigen Prozess-Exit (ohne Kill) erweitern | Offen | — |
| 10 | Hooks | `build_before_test.py` um Erkennung + Warnung bei laufender `Softwareschmiede.App.exe` (kein Kill) erweitern | Offen | — |
| 11 | Tests | `AppStartupLogInspectorTests`: `CheckAppStartupException_ErkenntMainWindowFehler` | Offen | — |
| 12 | Tests | `AppStartupLogInspectorTests`: `CheckAppStartupException_OhneFehler_LiefertNull` | Offen | — |
| 13 | Tests | `AppStartupLogInspectorTests`: `GetNewEntries_LiestNurInhaltNachOffset` | Offen | — |
| 14 | Tests | `AppStartupLogInspectorTests`: `ReadLatestLog_KeinVerzeichnis_LiefertLeer` | Offen | — |
| 15 | Tests | `AppStartupLogInspectorTests`: `ReadLatestLog_WaehltNeuesteDatei` | Offen | — |

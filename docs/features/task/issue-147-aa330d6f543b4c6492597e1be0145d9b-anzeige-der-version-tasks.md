# Tasks: Anzeige der Programmversion in der Seitenleiste

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | `MainWindowViewModel`: Feld `_versionProvider` und optionalen Konstruktor-Parameter `IApplicationVersionProvider? versionProvider = null` (am Ende) ergänzen und zuweisen | Offen | — |
| 2 | Logik | `MainWindowViewModel`: Property `CurrentVersion` (`string?`) mit Backing-Field und `SetProperty` anlegen | Offen | — |
| 3 | Logik | `MainWindowViewModel`: Methode `VersionLadenImHintergrund()` (Fire-and-Forget) anlegen und im Konstruktor aufrufen | Offen | — |
| 4 | Logik | `MainWindowViewModel`: Methode `VersionLadenAsync()` anlegen (ruft `GetInstalledVersionAsync`, ermittelt Anzeigetext/Fallback, setzt `CurrentVersion` über `_dispatcherInvoke`) | Offen | — |
| 5 | Logik | `MainWindowViewModel`: Fallback-Konstante `"Version unbekannt"` definieren | Offen | — |
| 6 | UI | `MainWindow.xaml`: Versions-`TextBlock` in Fußzeile (`Grid.Row="2"`) mit Binding an `CurrentVersion`, Styling und Platzierung ergänzen | Offen | — |
| 7 | UI | `MainWindow.xaml`: `Visibility`-Binding des Versions-`TextBlock` an `IsNavigationExpanded` setzen | Offen | — |
| 8 | UI | `MainWindow.xaml`: `AutomationProperties.AutomationId="AppVersionText"` am Versions-`TextBlock` setzen | Offen | — |
| 9 | Tests | `MainWindowViewModelTests.CreateSut` um optionalen Parameter `IApplicationVersionProvider? versionProvider = null` erweitern | Offen | — |
| 10 | Tests | Unit-Test `CurrentVersion_ShouldExposeInstalledVersion_WhenProviderReturnsValue` schreiben | Offen | — |
| 11 | Tests | Unit-Test `CurrentVersion_ShouldUseFallback_WhenProviderReturnsNull` schreiben | Offen | — |
| 12 | E2E-Tests | E2E-Test `E2E_VersionAnzeige` anlegen: Versions-`TextBlock` in aufgeklappter Seitenleiste zeigt nicht-leeren Versionstext | Offen | — |

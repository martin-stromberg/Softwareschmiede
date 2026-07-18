# Tasks: Anzeige der Programmversion in der Seitenleiste

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | `MainWindowViewModel`: Feld `_versionProvider` und optionalen Konstruktor-Parameter `IApplicationVersionProvider? versionProvider = null` (am Ende) ergänzen und zuweisen | Erledigt | `MainWindowViewModelTests.CurrentVersion_ShouldExposeInstalledVersion_WhenProviderReturnsValue` |
| 2 | Logik | `MainWindowViewModel`: Property `CurrentVersion` (`string?`) mit Backing-Field und `SetProperty` anlegen | Erledigt | `MainWindowViewModelTests.CurrentVersion_ShouldExposeInstalledVersion_WhenProviderReturnsValue` |
| 3 | Logik | `MainWindowViewModel`: Methode `VersionLadenImHintergrund()` (Fire-and-Forget) anlegen und im Konstruktor aufrufen | Erledigt | `MainWindowViewModelTests.CurrentVersion_ShouldExposeInstalledVersion_WhenProviderReturnsValue` |
| 4 | Logik | `MainWindowViewModel`: Methode `VersionLadenAsync()` anlegen (ruft `GetInstalledVersionAsync`, ermittelt Anzeigetext/Fallback, setzt `CurrentVersion` über `_dispatcherInvoke`) | Erledigt | `MainWindowViewModelTests.CurrentVersion_ShouldExposeInstalledVersion_WhenProviderReturnsValue` |
| 5 | Logik | `MainWindowViewModel`: Fallback-Konstante `"Version unbekannt"` definieren | Erledigt | `MainWindowViewModelTests.CurrentVersion_ShouldUseFallback_WhenProviderReturnsNull` |
| 6 | UI | `MainWindow.xaml`: Versions-`TextBlock` in Fußzeile (`Grid.Row="2"`) mit Binding an `CurrentVersion`, Styling und Platzierung ergänzen | Erledigt | `E2E_VersionAnzeige.AppStarten_ZeigtVersionsTextInFusszeile_E2E` |
| 7 | UI | `MainWindow.xaml`: `Visibility`-Binding des Versions-`TextBlock` an `IsNavigationExpanded` setzen | Erledigt | Kein direkter Test (deklaratives XAML-Binding; visuell abgedeckt durch `E2E_VersionAnzeige.AppStarten_ZeigtVersionsTextInFusszeile_E2E` bei aufgeklappter Seitenleiste) |
| 8 | UI | `MainWindow.xaml`: `AutomationProperties.AutomationId="AppVersionText"` am Versions-`TextBlock` setzen | Erledigt | `E2E_VersionAnzeige.AppStarten_ZeigtVersionsTextInFusszeile_E2E` |
| 9 | Tests | `MainWindowViewModelTests.CreateSut` um optionalen Parameter `IApplicationVersionProvider? versionProvider = null` erweitern | Erledigt | `MainWindowViewModelTests.CurrentVersion_ShouldExposeInstalledVersion_WhenProviderReturnsValue` |
| 10 | Tests | Unit-Test `CurrentVersion_ShouldExposeInstalledVersion_WhenProviderReturnsValue` schreiben | Erledigt | `MainWindowViewModelTests.CurrentVersion_ShouldExposeInstalledVersion_WhenProviderReturnsValue` |
| 11 | Tests | Unit-Test `CurrentVersion_ShouldUseFallback_WhenProviderReturnsNull` schreiben | Erledigt | `MainWindowViewModelTests.CurrentVersion_ShouldUseFallback_WhenProviderReturnsNull` |
| 12 | E2E-Tests | E2E-Test `E2E_VersionAnzeige` anlegen: Versions-`TextBlock` in aufgeklappter Seitenleiste zeigt nicht-leeren Versionstext | Erledigt | `E2E_VersionAnzeige.AppStarten_ZeigtVersionsTextInFusszeile_E2E` |

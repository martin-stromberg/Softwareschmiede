# Tasks: Aktive Aufgaben im Menü

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Converter | `KiAusfuehrungsStatusConverter` implementieren (`IValueConverter` mit Status-String-Logik) | Offen | — |
| 2 | Service | `AufgabeService.GetAktiveAufgabenAsync()` implementieren (filtert nach `Gestartet`/`Wartend`, sortiert nach Heartbeat) | Offen | — |
| 3 | ViewModel | `MainWindowViewModel.AktiveAufgaben` Property hinzufügen (`ObservableCollection<Aufgabe>`) | Offen | — |
| 4 | ViewModel | `MainWindowViewModel.IsDashboardVisible` computed Property hinzufügen (prüft `CurrentView is DashboardViewModel`) | Offen | — |
| 5 | ViewModel | `MainWindowViewModel.AktiveAufgabenAktualisierenAsync()` Methode implementieren (ruft Service auf, befüllt Collection) | Offen | — |
| 6 | ViewModel | `MainWindowViewModel.NavigateZuAufgabeCommand` Command implementieren (erstellt `TaskDetailViewModel`, setzt `CurrentView`) | Offen | — |
| 7 | ViewModel | `MainWindowViewModel` Constructor aktualisieren (initialisiert `AktiveAufgaben`, ruft `AktiveAufgabenAktualisierenAsync()` auf) | Offen | — |
| 8 | ViewModel | `DashboardViewModel.AktiveAufgabenListe` Property hinzufügen (`ObservableCollection<Aufgabe>`) | Offen | — |
| 9 | ViewModel | `DashboardViewModel.LadenAsync()` erweitern (ruft `GetAktiveAufgabenAsync()` auf, befüllt `AktiveAufgabenListe`) | Offen | — |
| 10 | Konfiguration | `App.xaml` aktualisieren: `KiAusfuehrungsStatusConverter` registrieren als StaticResource | Offen | — |
| 11 | UI (XAML) | `MainWindow.xaml` Seitenleiste erweitern: Border-Separator nach Navigationseinträgen hinzufügen | Offen | — |
| 12 | UI (XAML) | `MainWindow.xaml` Seitenleiste erweitern: "Aktive Aufgaben" TextBlock hinzufügen | Offen | — |
| 13 | UI (XAML) | `MainWindow.xaml` Seitenleiste erweitern: ScrollViewer mit ItemsControl für `AktiveAufgaben` hinzufügen | Offen | — |
| 14 | UI (XAML) | `MainWindow.xaml` ItemsControl DataTemplate implementieren: Aufgabenkachel mit Titel, Status, Navigation-Button | Offen | — |
| 15 | UI (XAML) | `MainWindow.xaml` Seitenleisten-Sektion Sichtbarkeit setzen: `IsDashboardVisible` mit `InvertedBoolToVisibilityConverter` | Offen | — |
| 16 | UI (XAML) | `DashboardView.xaml` erweitern: Neue Sektion "Aktive Aufgaben" hinzufügen (unter Statistik-Kacheln oder in separater Panel) | Offen | — |
| 17 | UI (XAML) | `DashboardView.xaml` ItemsControl implementieren: DataTemplate für Aufgabenkacheln (identisch oder ähnlich MainWindow) | Offen | — |
| 18 | Tests (Unit) | `AufgabeServiceTests.GetAktiveAufgabenAsync_ShouldReturnAufgabenWithStatusGestartetOrWartend_WhenCalled` schreiben | Offen | — |
| 19 | Tests (Unit) | `AufgabeServiceTests.GetAktiveAufgabenAsync_ShouldSortByLastHeartbeatDescThenByErstellungsDatum_WhenCalled` schreiben | Offen | — |
| 20 | Tests (Unit) | `AufgabeServiceTests.GetAktiveAufgabenAsync_ShouldLimitTo20Results_WhenMoreThan20Exist` schreiben (optional je nach Implementierung) | Offen | — |
| 21 | Tests (Unit) | `KiAusfuehrungsStatusConverterTests` Klasse anlegen | Offen | — |
| 22 | Tests (Unit) | `KiAusfuehrungsStatusConverterTests.Convert_ShouldReturnLaeuftString_WhenAktiveRunIdPresentAndHeartbeatRecent` schreiben | Offen | — |
| 23 | Tests (Unit) | `KiAusfuehrungsStatusConverterTests.Convert_ShouldReturnWartetString_WhenStatusIstWartend` schreiben | Offen | — |
| 24 | Tests (Unit) | `KiAusfuehrungsStatusConverterTests.Convert_ShouldReturnBereitOrStatusFallback_WhenNoActiveRunOrOldHeartbeat` schreiben | Offen | — |
| 25 | Tests (Unit) | `MainWindowViewModelTests.AktiveAufgabenAktualisierenAsync_ShouldFillObservableCollection_WhenCalled` schreiben | Offen | — |
| 26 | Tests (Unit) | `MainWindowViewModelTests.IsDashboardVisible_ShouldReturnTrue_WhenCurrentViewIsDashboardViewModel` schreiben | Offen | — |
| 27 | Tests (Unit) | `MainWindowViewModelTests.IsDashboardVisible_ShouldReturnFalse_WhenCurrentViewIsNotDashboard` schreiben | Offen | — |
| 28 | Tests (Unit) | `MainWindowViewModelTests.NavigateZuAufgabeCommand_ShouldCreateTaskDetailViewModelAndSetCurrentView_WhenExecutedWithAufgabeId` schreiben | Offen | — |
| 29 | Tests (Unit) | `DashboardViewModelTests.LadenAsync_ShouldFillAktiveAufgabenListe_WhenCalled` schreiben (oder existierende Tests anpassen) | Offen | — |
| 30 | Tests (E2E) | E2E-Test: Menü-Anzeige aktiver Aufgaben (Seitenleiste zeigt korrekte Aufgaben mit Titel und Status) | Offen | — |
| 31 | Tests (E2E) | E2E-Test: Navigation zu Aufgabendetail via Menü-Aufgabenkachel | Offen | — |
| 32 | Tests (E2E) | E2E-Test: Dashboard-Anzeige (aktive Aufgaben im Dashboard, Menü-Sektion verborgen) | Offen | — |
| 33 | Tests (E2E) | E2E-Test: KI-Status-Anzeige "Läuft" (Aufgabe mit aktuellem Heartbeat) | Offen | — |
| 34 | Tests (E2E) | E2E-Test: KI-Status-Anzeige "Wartet" (Aufgabe mit Status Wartend) | Offen | — |
| 35 | Tests (E2E) | E2E-Test: Sichtbarkeits-Toggle Menü-Sektion (verschwindet bei Dashboard, erscheint beim Verlassen) | Offen | — |
| 36 | Tests (Integration) | Bestehende E2E/Integration-Tests überprüfen: Navigation-Tests ggf. anpassen (DOM-Struktur-Änderungen) | Offen | — |
| 37 | Tests (Integration) | Bestehende E2E/Integration-Tests überprüfen: Seitenleisten-Layout-Tests ggf. anpassen | Offen | — |
| 38 | Tests (Integration) | Bestehende E2E/Integration-Tests überprüfen: Dashboard-Layout-Tests ggf. anpassen | Offen | — |

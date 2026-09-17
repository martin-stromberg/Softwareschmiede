# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

Geprüft wurde der Branch gegen `origin/staging` mit Fokus auf Lebenszyklus- und
Nebenläufigkeitskorrektheit (Update-Gates, Generations-Invalidierung,
`CancellationToken`-Propagation, Semaphore-/CTS-Freigabe, Event-Subscription,
Dispatcher-Affinität, Exception-Behandlung, E2E-Testinfrastruktur). Die zentralen
Mechanismen sind korrekt umgesetzt: `_updateGate` wird auf allen Pfaden per
`finally` freigegeben, `_checkGate` wird nur nach erfolgreicher Akquise
freigegeben, die Generationsprüfung (`IstUpdateVersuchAktuell`) verwirft stale
Ergebnisse in `UpdatePruefenAsync`, `UpdateStartenAsync` und
`InstalliereUpdateAsync` konsistent, `_startInitialisierungErfolgt` wird per
`Interlocked.Exchange` atomar gesetzt, Event-Subscriptions werden in `Dispose`
gelöst, und `OperationCanceledException` wird überall kontrolliert behandelt.
Es wurden sechs konkrete, aber nicht kritische Befunde identifiziert.

## Befunde

### 1. Asset-Download-URL erlaubt `http://` (Niedrig)

`src/Softwareschmiede/Infrastructure/Services/Updates/GitHubReleaseClient.cs:190-192`
(`IsAbsoluteHttpUrl` akzeptiert `UriSchemeHttp`), Verwendung in Zeile 125.

Der neue Branch validiert `browser_download_url` mit `IsAbsoluteHttpUrl`, das
neben `https` auch `http` zulässt. Pagination-Folge-URLs werden dagegen in
`IsAllowedFollowUpUri` (Zeile 198) strikt auf `https` beschränkt. Das
heruntergeladene Update-Paket wird anschließend ohne Signatur- oder
Hash-Prüfung entpackt und per Skript installiert — die Transportabsicherung ist
damit die einzige Integritätsgrenze. Da GitHub in der Praxis ausschließlich
`https`-URLs liefert, ist das Risiko gering, aber die Validierung ist
inkonsistent und schwächer als die benachbarte Folge-URL-Prüfung.

**Empfehlung:** `IsAbsoluteHttpUrl` auf `UriSchemeHttps` einschränken (bzw. in
`IsAbsoluteHttpsUrl` umbenennen), analog zu `IsAllowedFollowUpUri`, und den
`http`-Fall in `GitHubReleaseClientTests_Filters` als abgelehnten Fall
ergänzen (aktuell wird `ftp://` abgelehnt, `http://` aber akzeptiert).

### 2. `CancelLaufendenUpdateAblauf` fängt nur `ObjectDisposedException` (Niedrig)

`src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs:303-314`.

`CancellationTokenSource.Cancel()` kann eine `AggregateException` werfen, wenn
einer der registrierten Cancellation-Callbacks (z. B. von `HttpClient`- oder
EF-Operationen auf dem verlinkten Token) eine Ausnahme auslöst. Der Aufruf
erfolgt in `OnUpdateSettingsSaved` (Dispatcher-Callback, Zeile 276) und in
`Dispose` (Zeile 305); eine dort propagierte Ausnahme würde als unbehandelte
Dispatcher-/Dispose-Ausnahme durchschlagen. Die Wahrscheinlichkeit ist gering,
die Behebung trivial.

**Empfehlung:** Zusätzlich `catch (Exception)` mit Logeintrag ergänzen (oder
`Cancel()` in einen eigenen Try-Block mit breiterem Catch legen), damit ein
fehlerhafter Cancellation-Callback weder das Speichern der Einstellungen noch
`Dispose` stört.

### 3. Stream-Gate-Timeout wird als `OperationCanceledException` gemeldet (Niedrig, nur Testinfrastruktur)

`src/Softwareschmiede.App/Services/Testing/UpdateFixtureHttpMessageHandler.cs:299-313`
(`GateBlockierterStream.WarteFallsBlockiertAsync`) im Vergleich zu `SendAsync`
in Zeilen 127-137.

Beim Antwort-Gate unterscheidet `SendAsync` korrekt zwischen Timeout
(`HttpRequestException`) und echtem Abbruch (`OperationCanceledException`). Im
blockierenden Stream hingegen wird auch ein Gate-Timeout (das Gate wurde nicht
freigegeben — ein Fehler der Teststeuerung, nicht der App) als
`OperationCanceledException` geworfen. Die App zeigt dann
„Update-Vorbereitung wurde abgebrochen." und der Test schlägt mit einer
irreführenden Ursache fehl.

**Empfehlung:** Im Stream analog unterscheiden: `ct.IsCancellationRequested`
→ `OperationCanceledException`, sonst (Timeout) eine
`InvalidOperationException`/`HttpRequestException` mit Gate-Namen, damit ein
nicht freigegebenes Test-Gate als Infrastrukturfehler erkennbar bleibt.

### 4. Runner-seitiges Protokoll-Append ohne Sharing-Violation-Retry (Niedrig, nur Testinfrastruktur)

`src/Softwareschmiede.App/Services/Testing/UpdateE2EProtokoll.cs:87-95`
(`Anfuegen`) im Vergleich zu `Schreibe` in Zeilen 122-134.

`Schreibe` (App-seitig) wiederholt `File.AppendAllText` bis zu fünfmal bei
`IOException`, um Kollisionen mit gleichzeitigen Lese-/Schreibzugriffen des
Testrunners abzufedern. `Anfuegen` (Runner-seitig, für `TestStart`/`TestEnd`)
hat keinen Retry. Schreibt die getestete App im selben Moment ein Ereignis,
kann `File.AppendAllText` mit einer `IOException` (Sharing Violation)
fehlschlagen und den Testlauf mit einem Infrastrukturfehler abbrechen.

**Empfehlung:** `Anfuegen` denselben kurzen Retry-Mechanismus wie `Schreibe`
verwenden lassen (oder beide auf eine gemeinsame Hilfsmethode umstellen).

### 5. Wiederherstellungs-Speichern in `finally` kann den eigentlichen Testfehler maskieren (Niedrig, nur Tests)

`src/Softwareschmiede.Tests/E2E/E2E_SettingsCommandLineParameters.cs:41-45` und
`src/Softwareschmiede.Tests/E2E/E2E_SettingsKiPluginPersistence.cs:48-52`.

In beiden `finally`-Blöcken wird `settings.SaveSettings()` aufgerufen, um den
Ausgangszustand wiederherzustellen. Schlägt das Speichern fehl (z. B.
`TimeoutException` beim Warten auf das Schließen des Fensters), ersetzt diese
Ausnahme den eigentlichen Assert-Fehler des Tests — Diagnose und
Fehlermeldung zeigen dann nur noch das Cleanup-Problem. Zum Vergleich:
`E2E_UpdateSettings.SetzeUpdateSteuerungZurueckAsync` kapselt alle
Rücksetzoperationen einzeln in `catch`-Blöcke.

**Empfehlung:** Die Wiederherstellung in beiden `finally`-Blöcken in einen
eigenen `try/catch` legen (Fehler ignorieren oder als Zusatzkontext loggen),
damit der ursprüngliche Testfehler erhalten bleibt.

### 6. Irreführender Interceptor-Kommentar und inkonsistente Synchronisation von `_aktuellerVersuch` (Hinweis, nur Testinfrastruktur)

`src/Softwareschmiede.App/Services/Testing/UpdateSettingsReadFailureInterceptor.cs:53-57`
und `src/Softwareschmiede.App/Services/Testing/UpdateE2ETestKontext.cs:117-190`.

Zwei Kleinigkeiten:

- Der Kommentar im Interceptor behauptet, dass je Scoped-`DbContext` eine neue
  Interceptor-Instanz entsteht und Zustand daher nicht in Instanzfeldern
  gehalten werden kann. Tatsächlich wird die über `options.AddInterceptors`
  (`App.xaml.cs:245`) registrierte Instanz von allen Kontexten geteilt, die mit
  denselben `DbContextOptions` arbeiten. Das Design bleibt korrekt (Zustand im
  Singleton-Kontext, `volatile`-Flag, Lock), aber die Begründung im Kommentar
  ist falsch und kann bei späteren Änderungen in die Irre führen.
- `UpdateE2ETestKontext._aktuellerVersuch` wird in
  `NaechsterMarkierterReadOrdinal` unter `_ordinalLock` gelesen, in
  `BeginneVersuch`/`BeendeVersuch` aber ohne Lock geschrieben. Praktisch laufen
  alle Zugriffe auf dem UI-Thread, sodass kein echtes Race entsteht; die
  asymmetrische Synchronisation ist aber inkonsistent (entweder Schreiben auch
  unter dem Lock, oder `volatile` und ohne Lock lesen).

**Empfehlung:** Kommentar korrigieren und Schreibzugriff auf
`_aktuellerVersuch` unter `_ordinalLock` legen (oder das Feld `volatile`
markieren), damit die Synchronisationsstrategie einheitlich ist.

## Geprüfte Dateien

### Produktionscode (Application/Infrastructure)

- `src/Softwareschmiede/Application/Services/AppEinstellungService.cs`
- `src/Softwareschmiede/Application/Services/Updates/SemanticUpdateVersion.cs`
- `src/Softwareschmiede/Application/Services/Updates/UpdateInterfaces.cs`
- `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs`
- `src/Softwareschmiede/Application/Services/Updates/UpdateService.cs`
- `src/Softwareschmiede/Application/Services/Updates/UpdateVersionComparer.cs`
- `src/Softwareschmiede/Infrastructure/Services/Updates/GitHubReleaseClient.cs`

### App (ViewModels, Views, Startup)

- `src/Softwareschmiede.App/App.xaml.cs`
- `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/UpdateProgressViewModel.cs`
- `src/Softwareschmiede.App/Views/MainWindow.xaml`
- `src/Softwareschmiede.App/Views/MainWindow.xaml.cs`
- `src/Softwareschmiede.App/Views/SettingsView.xaml`
- `src/Softwareschmiede.App/Views/UpdateProgressDialog.xaml`
- `src/Softwareschmiede.App/Views/UpdateProgressDialog.xaml.cs`

### App-Testinfrastruktur

- `src/Softwareschmiede.App/Services/Testing/FixtureCliUpdateSafetyService.cs`
- `src/Softwareschmiede.App/Services/Testing/ProtokollierenderUpdateService.cs`
- `src/Softwareschmiede.App/Services/Testing/RecordingApplicationShutdownService.cs`
- `src/Softwareschmiede.App/Services/Testing/RecordingUpdateProcessLauncher.cs`
- `src/Softwareschmiede.App/Services/Testing/UpdateE2EProtokoll.cs`
- `src/Softwareschmiede.App/Services/Testing/UpdateE2ESzenario.cs`
- `src/Softwareschmiede.App/Services/Testing/UpdateE2ETestConfiguration.cs`
- `src/Softwareschmiede.App/Services/Testing/UpdateE2ETestKontext.cs`
- `src/Softwareschmiede.App/Services/Testing/UpdateFixtureHttpMessageHandler.cs`
- `src/Softwareschmiede.App/Services/Testing/UpdateSettingsReadFailureInterceptor.cs`

### Unit- und Integrationstests

- `src/Softwareschmiede.Tests/App/Services/Testing/UpdateFixtureHttpMessageHandlerTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests_UpdateSettingsReadFailure.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests_UpdateStartup.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/SettingsViewModelTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/AppEinstellungServiceTests_UpdateSettings.cs`
- `src/Softwareschmiede.Tests/Application/Services/Updates/ApplicationVersionProviderTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/Updates/UpdatePackageServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/Updates/UpdateProgressViewModelTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/Updates/UpdateServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/Updates/UpdateServiceTests_Options.cs`
- `src/Softwareschmiede.Tests/Application/Services/Updates/UpdateServiceTests_PrereleaseChain.cs`
- `src/Softwareschmiede.Tests/Application/Services/Updates/UpdateVersionComparerTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/Updates/UpdateVersionComparerTests_SemVer.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Services/GitHubReleaseClientTests.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Services/GitHubReleaseClientTests_Filters.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Services/GitHubReleaseClientTests_Pagination.cs`

### E2E-Tests und Test-Hilfscode

- `src/Softwareschmiede.Tests/E2E/E2E_SettingsCommandLineParameters.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_SettingsKiPluginPersistence.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_UpdateFixture.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_UpdateSettings.cs`
- `src/Softwareschmiede.Tests/E2E/MainTest.cs`
- `src/Softwareschmiede.Tests/E2E/UpdateE2EFixture.cs`
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/UpdateProgressDialogView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/UpdateSafetyDialogView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/MenuView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/SettingsView.cs`
- `src/Softwareschmiede.Tests/E2E/WpfTestBase.cs`
- `src/Softwareschmiede.Tests/Helpers/TestDbContextFactory.cs`

### Zusätzlich gelesene Kontextdateien (nicht geändert, zur Verifikation)

- `src/Softwareschmiede.App/Services/DispatcherInvokeFactory.cs`
- `src/Softwareschmiede/AppData/DirectoryLocations.cs`

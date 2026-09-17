# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### MainWindowViewModel.cs (MainWindowViewModel)

- **Lange Parameterliste** — Der Konstruktor (Zeilen 160–175) umfasst inzwischen 15 Parameter, darunter der Test-only-Parameter `Services.Testing.UpdateE2ETestKontext? updateE2ETestKontext`. Damit ist das produktive ViewModel direkt an die E2E-Testinfrastruktur gekoppelt (Aufrufe in `InitializeUpdatesAfterWindowReadyAsync`, `UpdatePruefenAsync`, `UpdateStartenAsync`: Zeilen 455, 463, 501, 514, 547, 560, 599).

  Empfehlung: Die Versuchs-Instrumentierung hinter einer kleinen Schnittstelle (z. B. `IUpdateVersuchProtokoll` mit No-op-Standardimplementierung) kapseln und die Update-Kollaborateure (`IUpdateService`, `ICliUpdateSafetyService`, `IUpdateProgressDialogService`, `IDialogService`, `IApplicationVersionProvider`, Instrumentierung) in einem eigenen Parameter-/Service-Objekt bündeln.

### UpdateService.cs (UpdateService)

- **Mehrdeutige Fehlersemantik / kontextarme Meldung** — `CheckForUpdateAsync` (Zeilen 43–47) bildet `latest is null` pauschal auf `NichtPruefbar("GitHub-Release ist nicht prüfbar.")` ab. `IUpdateReleaseClient.GetLatestReleaseAsync` liefert `null` aber sowohl bei echten Fehlern (HTTP-Fehler, Timeout, JSON-Fehler, ungültige Folge-URL) als auch im regulären Fall „kein passendes Release vorhanden" (leere Liste oder alle Kandidaten per Option/draft/Asset herausgefiltert, z. B. ausschließlich Prereleases bei `IncludePrereleases = false`). Der Anwender erhält so im Erfolgsfall ohne passendes Release eine irreführende „nicht prüfbar"-Meldung statt „kein Update".

  Empfehlung: Den Client-Vertrag um einen Ergebnisstatus erweitern (z. B. `UpdateReleaseLookupResult` mit `Erfolg`/`Fehler` + Kandidat) oder `null` konsistent als „kein zulässiges Release" → `KeinUpdate` behandeln, da Fehler im Client bereits protokolliert werden.

### TestDbContextFactory.cs (TestDbContextFactory)

- **Toter Code** — Die neue öffentliche Hilfsmethode `CreateSqlite` (Zeilen 21–37) hat keinen einzigen Aufrufer im Testprojekt (die beiden neuen Testklassen `AppEinstellungServiceTests_UpdateSettings` und `MainWindowViewModelTests_*` bauen den `DbContextOptionsBuilder` jeweils selbst).

  Empfehlung: Entweder `CreateSqlite` in den betroffenen Tests tatsächlich verwenden oder die Methode entfernen.

### MainWindowViewModelTests.cs (MainWindowViewModelTests)

- **Ungenutzte Parameter** — `CreateSut` (Zeilen 80–108) nimmt weiterhin `updateService`, `cliUpdateSafetyService` und `updateProgressDialogService` entgegen; nach dem Auszug der Update-Tests in `MainWindowViewModelTests_UpdateStartup`/`_UpdateSettingsReadFailure` ruft kein verbleibender Test diese Parameter mehr auf.

  Empfehlung: Die drei Parameter entfernen (der Konstruktor von `MainWindowViewModel` akzeptiert `null`-Defaults).

### MainWindowViewModelTests_UpdateStartup.cs / MainWindowViewModelTests_UpdateSettingsReadFailure.cs

- **Duplizierter Code** — Beide neuen Testklassen enthalten nahezu identische Blöcke: der Konstruktor mit dem kompletten DI-Container-Aufbau (~30 Zeilen), `CreateSut`, `SetUpdateSettingsAsync` und `SpeichereUpdateEinstellungenUeberUiAsync` (~60 Zeilen insgesamt, bis auf den Interceptor-Zusatz wörtlich gleich).

  Empfehlung: Gemeinsame Basisklasse oder Test-Fixture mit `CreateSut`/Settings-Helfern auslagern, sodass der Interceptor-Fall nur die Abweichung ergänzt.

### GitHubReleaseClientTests_Pagination.cs / UpdateServiceTests_PrereleaseChain.cs

- **Duplizierter Code** — `RoutingHttpHandler` ist in beiden neuen Testdateien nahezu identisch implementiert (Routing-Dictionary, `AddPage` mit `Link`-Header, `RequestedUrls`, `SendAsync`; Zeilen 204–239 bzw. 108–149). Zusätzlich fügt `UpdateServiceTests_PrereleaseChain` mit `TempDirectory` (ab Zeile 151) eine weitere Kopie einer Klasse hinzu, die bereits viermal als private Nested Class existiert (`UpdatePackageServiceTests`, `ApplicationVersionProviderTests`, `UpdateScriptServiceTests`, `DatenbankPfadResolverTests`).

  Empfehlung: `RoutingHttpHandler` und `TempDirectory` einmalig unter `src/Softwareschmiede.Tests/Helpers/` bereitstellen und in den Tests wiederverwenden.

### GitHubReleaseClientTests_Filters.cs (GitHubReleaseClientTests_Filters)

- **Duplizierter Code** — Die private Nested Class `StaticHttpHandler` (Zeilen 226–242) ist eine nahezu identische Variante des bereits vorhandenen `StaticHttpHandler` in `GitHubReleaseClientTests.cs` (dort mit Statuscode-Parameter).

  Empfehlung: Einen gemeinsamen statischen Test-Handler in `Tests/Helpers` extrahieren oder den vorhandenen Handler wiederverwenden.

### UpdateSettingsReadFailureInterceptor.cs / UpdateE2ETestKontext.cs

- **Duplizierter Code (sync/async-Varianten)** — `WarteUndWirf`/`WarteUndWirfAsync` (UpdateSettingsReadFailureInterceptor, Zeilen 143–177) sowie `WarteAufGate`/`WarteAufGateAsync` (UpdateE2ETestKontext, Zeilen 147–184) sind jeweils nahezu identische Schleifen, die sich nur in `Thread.Sleep`/`Task.Delay` und der Cancellation-Behandlung unterscheiden.

  Empfehlung: Die gemeinsame Warte-Logik über einen übergebenen Wait-Delegate (z. B. `Func<int, bool>` bzw. `Func<int, Task<bool>>`) oder einen geteilten Hilfsalgorithmus zusammenführen, damit Timeout-/Gate-Semantik nur an einer Stelle gepflegt wird.

### E2E_UpdateFixture.cs (End2EndTest.Fixture_SettingsReadFailureTargetsPhaseAfterDatabaseInitialization)

- **Fehlerbehandlung im Cleanup** — Der `finally`-Block (Zeilen 247–253) ruft `fixture.DeaktiviereLesefehler()` und `fixture.SetzeGateZurueck(...)` ungeschützt auf. Beide Methoden führen Datei-I/O aus (`Szenariodatei schreiben`, `Protokoll append`, `File.Delete`) und können bei einem Fehlschlag die eigentliche Test-Assertion maskieren. In `E2E_UpdateSettings.SetzeUpdateSteuerungZurueckAsync` ist genau dafür bereits das bewachte Muster (`try/catch` je Aufruf) etabliert.

  Empfehlung: Die Cleanup-Aufrufe ebenfalls einzeln mit `try/catch` absichern (oder die vorhandene `SetzeUpdateSteuerungZurueckAsync`-Hilfe verwenden).

### MenuView.cs (MenuView)

- **Fehlende Ausgabeprüfung** — `GetOfferedUpdateVersion` (Zeilen 119–137) liefert bei vorhandenem, aber nicht dem erwarteten Format entsprechendem `HelpText` den ungeprüften Rohwert zurück (Zeile 136) statt `null`. Ein ToolTip mit abweichendem Text würde so als „angebotene Version" durchgereicht und erzeugt erst an der Assert-Stelle einen schwer interpretierbaren Vergleichsfehler.

  Empfehlung: Bei nicht passendem Format `null` zurückgeben (kein gültiges Angebot) oder explizit mit Meldung fehlschlagen.

## Geprüfte Dateien

- `src/Softwareschmiede.App/App.xaml.cs`
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
- `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/UpdateProgressViewModel.cs`
- `src/Softwareschmiede.App/Views/MainWindow.xaml`
- `src/Softwareschmiede.App/Views/MainWindow.xaml.cs`
- `src/Softwareschmiede.App/Views/SettingsView.xaml`
- `src/Softwareschmiede.App/Views/UpdateProgressDialog.xaml`
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
- `src/Softwareschmiede.Tests/Infrastructure/Services/GitHubReleaseClientTests.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Services/GitHubReleaseClientTests_Filters.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Services/GitHubReleaseClientTests_Pagination.cs`
- `src/Softwareschmiede/Application/Services/AppEinstellungService.cs`
- `src/Softwareschmiede/Application/Services/Updates/SemanticUpdateVersion.cs`
- `src/Softwareschmiede/Application/Services/Updates/UpdateInterfaces.cs`
- `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs`
- `src/Softwareschmiede/Application/Services/Updates/UpdateService.cs`
- `src/Softwareschmiede/Application/Services/Updates/UpdateVersionComparer.cs`
- `src/Softwareschmiede/Infrastructure/Services/Updates/GitHubReleaseClient.cs`

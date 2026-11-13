# Tasks: Shutdown bei Abschluss

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Baseline | Vollständiger Build + `dotnet test ... --filter "Category!=OsInterface"` als Ausgangszustand erheben | Offen | — |
| 2 | Datenmodell | `KiAusfuehrungsZustand` als public-Enum nach `Softwareschmiede/Domain/Enums` verschieben | Offen | — |
| 3 | Datenmodell | `KiAusfuehrungsStatusKlassifikation` + `KiAusfuehrungsStatusDaten` (public) nach `Softwareschmiede/Application/Services` verschieben; internal Adapter in `AppConverters.cs` belassen | Offen | — |
| 4 | Logik | `ISystemShutdownService.RequestShutdownAsync` um Parameter `bool erzwingen` erweitern | Offen | — |
| 5 | Logik | `SystemShutdownService`: Force-Mapping (`/f` nur bei erzwingen), `ResolveShutdownCommand` internal machen | Offen | — |
| 6 | Logik | `AufzeichnenderSystemShutdownService` (Testmodus-Fake mit `LogDateiName`/`ResolveLogDateiPfad`, `system-shutdowns.log`) anlegen | Offen | — |
| 7 | Konfiguration | `AppEinstellungService`: Konstanten `AutoShutdownErzwingenKey`, `AutoShutdownToleranzMinutenKey`, `AutoShutdownDefaultToleranzMinuten` + Get/Set-Methoden (Defaults, Clamp 0–1440) | Offen | — |
| 8 | Logik | `ILaufendeAutomatisierungenZaehlung`-Interface anlegen | Offen | — |
| 9 | Logik | `LaufendeAutomatisierungenZaehlung` implementieren (Scope → `GetAktiveAufgabenAsync`, Live-Substatus, Prompt-Beleg, Klassifikation → `Laeuft` zählen) | Offen | — |
| 10 | Logik | `IAutoShutdownOrchestrator` erweitern (`SetOptionen`, `CountdownEndeUtc`, `IstAktiviert`, `ZustandGeaendert`) | Offen | — |
| 11 | Logik | `AutoShutdownOrchestrator` umbauen: koaleszierte `EvaluiereAsync`, `TimeProvider`-Countdown-Timer, 5-s-Sicherheitstick, `LaufdatenChanged`-Abo, Force-Durchreichung, Dispose | Offen | — |
| 12 | Logik | `AutoShutdownEinstellungenService` (App-Layer, Singleton mit `IServiceScopeFactory`) anlegen | Offen | — |
| 13 | UI | `MainWindowOptionaleDienste` um `AutoShutdownOrchestrator`, `AutoShutdownEinstellungenService`, `TimeProvider` erweitern | Offen | — |
| 14 | UI | `MainWindowViewModel`: Auto-Shutdown-Properties, Sichtbarkeiten, `LaufendeAutomatisierungenAnzahl`, Toleranz-Validierung, Countdown-Anzeige-Timer, `AutoShutdownEinstellungenLadenAsync`, Dispose | Offen | — |
| 15 | UI | `MainWindow.xaml`: Auto-Shutdown-Block in der Fußzeile (Anzahl-Text, Checkbox, Toleranz-TextBox, Force-Checkbox, Countdown-Text, Fehler-Text, AutomationIds, Collapsed-Navigation-Sichtbarkeit) | Offen | — |
| 16 | Konfiguration | `App.xaml.cs`: bedingte `ISystemShutdownService`-Registrierung, Singletons, `MainWindowOptionaleDienste`-Factory, Orchestrator-Aktivierung in `StartupAsync` | Offen | — |
| 17 | Tests | `AutoShutdownOrchestratorTests` auf Countdown-Semantik umbauen (`FakeTimeProvider`, neue Fakes, neue Fälle) | Offen | — |
| 18 | Tests | `LaufendeAutomatisierungenZaehlungTests` neu anlegen (alle Klassifikationszustände) | Offen | — |
| 19 | Tests | `AppEinstellungService`-Tests für `autoshutdown.*`-Schlüssel (Defaults, Clamp, Roundtrip) | Offen | — |
| 20 | Tests | `AufzeichnenderSystemShutdownServiceTests` neu anlegen (Force-Flag-Aufzeichnung, kein Prozessstart) | Offen | — |
| 21 | Tests | `SystemShutdownServiceTests` neu anlegen (Windows-Argument-Mapping mit/ohne Force) | Offen | — |
| 22 | Tests | `MainWindowViewModelTests` erweitern (Anzahl, Sichtbarkeiten, Einstellungen-Laden, Toleranz-Validierung, SetEnabled-Durchreichung, Countdown-Text) | Offen | — |
| 23 | E2E-Tests | `WpfTestBase`: `ResolveSystemShutdownLogPfad` + `WaitForSystemShutdownEintragAsync` hinzufügen | Offen | — |
| 24 | E2E-Tests | `MenuView`: Zugriffsmethoden für Auto-Shutdown-Elemente (Anzahl, Checkbox, Toleranz, Force, Countdown-Text) hinzufügen | Offen | — |
| 25 | E2E-Tests | `E2E_AutoShutdown.cs`: konsolidierte Testmethode `AutoShutdown_CountdownAbbruchAufzeichnungUndPersistenz_E2E` anlegen und in `MainTest.cs` einbinden | Offen | — |
| 26 | Dokumentation | `docs/help/einstellungen/beschreibung.md` Abschnitt „Automatisches Herunterfahren" korrigieren | Offen | — |
| 27 | Abschluss | `dotnet format Softwareschmiede.slnx --verify-no-changes` ausführen | Offen | — |

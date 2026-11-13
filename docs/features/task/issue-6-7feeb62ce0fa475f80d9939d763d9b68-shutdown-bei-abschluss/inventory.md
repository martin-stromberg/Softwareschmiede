# Bestandsaufnahme: Shutdown bei Abschluss

Bestandsaufnahme des Auto-Shutdown-/Laufstatus-Codes in `Softwareschmiede`, `Softwareschmiede.App` und `Softwareschmiede.Tests` bezogen auf die Anforderung in `requirement.md`: Anzeige laufender Automatisierungen in der Seitenleisten-Fußzeile des `MainWindow`, Auto-Shutdown-Checkbox mit Toleranzzeit-Countdown und „Herunterfahren erzwingen"-Schalter, Abbruch bei Statuswechsel, Persistenz von Force-Flag und Toleranzzeit.

## Zusammenfassung

**Vorhanden und direkt nutzbar:**

- `AutoShutdownOrchestrator` (`src/Softwareschmiede/Application/Services/AutoShutdownOrchestrator.cs`) reagiert auf `IRunningAutomationStatusSource.RunningCountChanged` und ruft beim Übergang >0 → 0 bei aktiviertem Toggle genau einmal `ISystemShutdownService.RequestShutdownAsync` auf — inkl. Final-Recheck, Idempotenz-Guard und Dispose-Abmeldung. **Aber:** weder Orchestrator noch `SystemShutdownService` sind in `App.xaml.cs` im DI-Container registriert; der Code ist in der WPF-App funktional tot (nur Unit-Tests rufen ihn). Countdown/Toleranzzeit, Abbruch bei Statuswechsel, Force-Flag, Persistenz und jede UI-Anbindung fehlen vollständig.
- `SystemShutdownService` (`src/Softwareschmiede/Infrastructure/Services/SystemShutdownService.cs`) löst das OS-Kommando plattformabhängig auf: Windows `shutdown /s /f /t 0` (Force fest eingebaut), Linux `shutdown -h now`, macOS `osascript`. Die Methode `ResolveShutdownCommand` ist privat und nicht parametrisiert — für das Force-Mapping müsste sie erweitert werden.
- `IRunningAutomationStatusSource` (implementiert von `KiAusfuehrungsService`, DI-registriert `App.xaml.cs` Z. 304–305) liefert `RunningCountChanged`, `GetRunningCount()` (zählt **alle** laufenden gehandelten Prozesse, inkl. `WartetAufEingabe`), `IsRunning`, `GetLiveLaufStatus` und `GetLaufendeAufgabenIds`. Die geforderte fachliche „läuft"-Zählung (ohne `Wartet`) existiert nicht als eigene Methode.
- Die Klassifikation `KiAusfuehrungsStatusKlassifikation.Klassifiziere` (`src/Softwareschmiede.App/Converters/AppConverters.cs` Z. 138–218, `internal`) bildet bereits exakt den geforderten „Läuft/Wartet"-Zustand ab (`KiAusfuehrungsZustand.Laeuft` vs. `Wartet`), liegt aber im App-Projekt und ist für den Application-Layer nicht erreichbar.
- Datenbasis für die fachliche Zählung in der UI-Schicht: `MainWindowViewModel.AktiveAufgabenListe` mit gemappten `LiveLaufStatus`/`LaufStatus`/`Status`/`PausiertBisUtc` (Z. 366–411), aktualisiert über `RunningCountChanged`, `LaufdatenChanged` und einen 5-s-`DispatcherTimer`.
- Ereignisquellen für Statuswechsel: `KiAusfuehrungsService.CliProcessStatusChanged`/`CliLaufAktiv`/`RunningCountChanged`, `ITerminalSession.RuntimeStatusChanged` (Läuft↔Wartet-Wechsel, persistiert durch `CliProcessManager`), `AufgabeLaufdatenChangedNotifier.LaufdatenChanged` (Singleton, DI-registriert).
- Persistenzmuster: `AppEinstellungService` (Key-Value-Entität `AppEinstellung`, `AddScoped` in `App.xaml.cs` Z. 251) mit Schlüssel-Konstanten + typsicheren Get/Set-Methoden inkl. `Math.Clamp`-Begrenzung (Muster: `ScmTaskCompletionWaitMinutesKey`, Z. 67, 310–314). Singleton→Scoped-Zugriff per `IServiceScopeFactory` (Muster: `AufgabenListenModusService`, `PromptZeitVersandService`).
- Countdown-/Timer-Muster: `PromptZeitVersandService` nutzt `TimeProvider.CreateTimer` (`ITimer`) mit `timer.Change(...)`-Armiierung nach Eintrag — testbar über `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing` ist referenziert). `TimeProvider.System` ist DI-registriert (`App.xaml.cs` Z. 295).
- E2E-Sicherheitsmuster: `UpdateE2ETestKontext` + `RecordingApplicationShutdownService` (zeichnet `IApplicationShutdownService`-Aufrufe ins JSONL-Protokoll auf, `App.xaml.cs` Z. 385–412) — Muster, wie ein gefährlicher Service im Testmodus durch einen aufzeichnenden Fake ersetzt wird. Achtung: `IApplicationShutdownService` beendet die **App**, nicht das OS — ein eigenes `ISystemShutdownService`-Fake existiert noch nicht.
- UI-Konventionen der Fußzeile: `MainWindow.xaml` Z. 170–228 (`StackPanel` in `Grid.Row="2"` mit Trennlinie, `AppVersionText`, Update-Buttons, `UpdateHinweis`), Sichtbarkeiten über `BoolToVisibilityConverter`/`GreaterThanZeroToVisibilityConverter`/`NullOrEmptyToVisibilityConverter`, Texte bei eingeklappter Navigation (48 px) ausgeblendet.

**Nicht vorhanden (zentral für die Anforderung):**

- Keine DI-Registrierung für `ISystemShutdownService`/`SystemShutdownService` und `IAutoShutdownOrchestrator`/`AutoShutdownOrchestrator` sowie keine Aktivierung des Orchestrators beim App-Start.
- Kein Countdown im Orchestrator (Sofort-Shutdown beim Übergang auf 0), kein abbrechen bei beliebigen Statuswechseln, kein Force-Parameter in `ISystemShutdownService.RequestShutdownAsync`, keine Countdown-Zustands-/Fortschrittsoberfläche für die UI.
- Keine UI-Elemente in der Seitenleisten-Fußzeile für Running-Count, Auto-Shutdown-Checkbox, Toleranzzeit-Eingabe, Force-Checkbox oder Countdown-Anzeige; keine entsprechenden ViewModel-Properties.
- Keine `AppEinstellungService`-Schlüssel/Get/Set für `autoshutdown.force` und `autoshutdown.toleranz.minuten`.
- Kein E2E-/Test-Fake für `ISystemShutdownService` — Tests müssten aktuell ein echtes OS-Shutdown riskieren, falls der Service im Testmodus produktiv registriert würde.
- Dokumentationsabweichung: `docs/help/einstellungen/beschreibung.md` (Z. 148–150) beschreibt den Orchestrator bereits als aktiv lauschend — der Text stammt aus der Blazor-Vorgängerversion und entspricht nicht dem DI-Ist-Stand.

**Test-Ausgangszustand:** Kein Testlauf ausgeführt — diese Bestandsaufnahme ist eine reine Lese-/Dokumentationsaufgabe ohne Build/Test; der Ausgangszustand ist damit **unbekannt** und muss vor der Umsetzung noch erhoben werden. Ermittelte Testlanes, Befehle, relevante Testklassen und Hilfsmittel: [Tests](inventory/tests.md).

## Details

- [Datenmodell](inventory/models.md) — `AppEinstellung`, `Aufgabe` (laufbezogene Eigenschaften), `AktiveAufgabePanelItem`, `ScheduledPromptInfo`, `ZeitgesteuerterPrompt`, `CliProcessHandle`, `UpdateE2ETestKontext`/`UpdateE2ETestConfiguration`
- [Logik](inventory/logic.md) — `AutoShutdownOrchestrator`, `SystemShutdownService`, `KiAusfuehrungsService` (Statusquelle), `CliProcessManager` (Laufstatus-Persistenz), `KiAusfuehrungsStatusKlassifikation`, `MainWindowViewModel` + Fußzeilen-Pipeline, `AppEinstellungService`, `PromptZeitVersandService` (Timer-Muster), `AufgabenListenModusService`/`AufgabenListenModusBindung` (persistierte UI-Einstellung), `ScmTaskCompletionMonitoringService` (Countdown-Vorbild), `UpdateService`/`IApplicationShutdownService` (App-Shutdown vs. OS-Shutdown), `App.xaml.cs` (DI-/E2E-Wiring)
- [Enums](inventory/enums.md) — `KiAusfuehrungsZustand`, `AufgabeLaufStatus`, `AufgabeStatus`, `AufgabeAusfuehrungsStatus`, `CliRuntimeStatus`, `CliProcessStatus`, `AufgabenListenModus`
- [Interfaces](inventory/interfaces.md) — `IRunningAutomationStatusSource`, `ISystemShutdownService`, `IAutoShutdownOrchestrator`, `IAktiveAufgabenService`, `IApplicationShutdownService`, `ICliUpdateSafetyService`, `ITerminalSession` (Statusmitglieder)
- [Tests](inventory/tests.md) — Test-Ausgangszustand (nicht erhoben, mit Begründung und Befehlen), `AutoShutdownOrchestratorTests`, betroffene ViewModel-/Service-Tests, Hilfsklassen und E2E-Infrastruktur

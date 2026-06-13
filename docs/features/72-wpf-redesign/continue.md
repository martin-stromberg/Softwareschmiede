# Offene Aufgaben

Erstellt am: 2026-06-13
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine.

## Code-Review-Befunde

- [ ] `AktualisierenCallbackAusfuehrenAsync()` ist eine unnötige Ein-Zeiler-Indirektion — inline mit `await ProjektListeAktualisierenCallback?.Invoke()!` oder Null-Pattern ersetzen (ProjectDetailViewModel.cs)
- [ ] `LoeschenBestaetigenFunc` und `RepositoryDialogOeffnenFunc` als öffentliche `Func`-Properties verletzen MVVM — durch `IDialogService`-Interface mit Konstruktor-Injection ersetzen (ProjectDetailViewModel.cs)
- [ ] `_disposed`-Check in `RepositoryZuweisenAsync` nutzt nicht den vorhandenen CancellationToken — stattdessen `ct.IsCancellationRequested` nach dem Dialog-Aufruf prüfen (ProjectDetailViewModel.cs)
- [ ] `FehlerMeldung = $"Fehler: {ex.Message}"` wird in `ProjectListViewModel` dreimal inline gesetzt, `SetFehler()`-Hilfsmethode fehlt — `SetFehler(Exception)` in `ViewModelBase` hochziehen und in beiden Klassen nutzen (ProjectListViewModel.cs, ViewModelBase.cs)
- [ ] `LadenProjekteInternAsync` ist ein Middle Man, der `IsLoading`/`FehlerMeldung`-Behandlung in `NeuesProjektHinzufuegen` umgeht — entfernen und direkt `LadenAsync` aufrufen (ProjectListViewModel.cs)
- [ ] `Console.WriteLine` in `WpfTestBase.Dispose` ist im xUnit-Kontext nicht sichtbar — durch `ITestOutputHelper` oder stilles `catch { }` ersetzen (WpfTestBase.cs)
- [ ] Hardcodierte Strings `"Debug"`, `"Release"`, `"net10.0-windows10.0.17763.0"` in `ResolveAppExePath` — als Konstanten auslagern (WpfTestBase.cs)
- [ ] `Thread.Sleep`-Aufrufe in E2E-Tests ohne Begründung — durch `WaitForElement`-Polling ersetzen, Ausnahmen kommentieren (ProjectDetailE2ETests.cs)
- [ ] Hilfsmethoden `NavigateToProjecten`, `CreateProject`, `OpenProject`, `CreateAndOpenProject` in `ProjectDetailE2ETests` sind nicht in `WpfTestBase` ausgelagert, wodurch `WpfE2ETests` die Schritte dupliziert (ProjectDetailE2ETests.cs, WpfE2EPlaceholderTests.cs)
- [ ] `AsyncRelayCommand` schluckt Exceptions still, wenn `OnError` nicht gesetzt ist — an `Dispatcher.UnhandledException` weiterleiten oder per `Debug.WriteLine` protokollieren (ViewModelBase.cs)

## Fehlgeschlagene Tests

Keine fehlgeschlagenen Tests.

## Kundenfeedback

die Pröjektübersichtansicht benötigt ein Ribbonmenü
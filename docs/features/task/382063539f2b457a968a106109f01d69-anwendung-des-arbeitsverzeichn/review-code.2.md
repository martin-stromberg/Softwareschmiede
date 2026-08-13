# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

**Korrektur durch Orchestrator (Lifecycle-Skill) nach unabhängiger Verifikation:** Der ursprünglich zweite Befund ("fehlender Unit-Test für `OeffneArbeitsverzeichnisAsync`") wurde entfernt. Der Review-Unteragent hat die separate Testdatei `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_Arbeitsverzeichnis.cs` nicht berücksichtigt. Der dort vorhandene Test `OeffneArbeitsverzeichnis_MitKonfiguriertemArbeitsverzeichnis_RuftServiceMitAufgeloestemPfadAuf` (Zeile 143–162) prüft exakt das geforderte Verhalten: Aufgabe mit `RepositoryStartKonfiguration.WorkingDirectoryRelativePath`, `OeffneArbeitsverzeichnisCommand.Execute(null)`, und `prozessStarterMock.Verify(...)`, dass der aufgelöste Unterverzeichnis-Pfad (nicht der Repository-Root) übergeben wird. Der erste Befund (doppelte Polling-Logik in `E2E_VerzeichnisAktionen.cs`) wurde geprüft und ist zutreffend.

## Befunde

### E2E_VerzeichnisAktionen.cs (End2EndTest)

- **Doppelter Code** — Die neue private Methode `WaitForNeuemProzessStartEintragAsync(string substring, string vorherigerLogInhalt, TimeSpan? timeout = null)` (Zeile 222–250) dupliziert nahezu vollständig die Polling-Logik der bereits vorhandenen `WpfTestBase.WaitForProzessStartEintragAsync(string substring, TimeSpan? timeout = null)` (`src/Softwareschmiede.Tests/E2E/WpfTestBase.cs`, Zeile 121–150): identischer Deadline-Aufbau, identische `File.Exists`/`File.ReadAllTextAsync`-Schleife, identischer `catch (IOException)`-Kommentar zum kollidierenden Zugriff der App, identische `TimeoutException` am Ende. Der einzige fachliche Unterschied ist, dass nur der seit `vorherigerLogInhalt` neu hinzugekommene Teil der Logdatei durchsucht wird (`inhalt[vorherigerLogInhalt.Length..]` statt `inhalt`) sowie ein kürzeres Poll-Intervall (50 ms statt 200 ms). Zusätzlich wird der Logdatei-Pfad in der neuen Methode über `AufzeichnenderProzessStarter.ResolveLogDateiPfad(TestDbPath)` direkt aufgelöst, weil die entsprechende Hilfsmethode `WpfTestBase.ResolveProzessStartLogPfad()` `private` ist und aus der partiellen Klasse `E2E_VerzeichnisAktionen.cs` nicht wiederverwendet werden kann — auch das ist eine (kleinere) Duplizierung derselben Pfadauflösung.

  Empfehlung: Die Duplizierung auflösen, z. B. indem `WpfTestBase.WaitForProzessStartEintragAsync` um einen optionalen Parameter erweitert wird (z. B. `string sinceContent = ""`), der intern nur den seit `sinceContent` neu hinzugekommenen Teil der Logdatei prüft, und `WaitForNeuemProzessStartEintragAsync` in `E2E_VerzeichnisAktionen.cs` entfällt zugunsten eines Aufrufs dieser erweiterten Basismethode. Alternativ `ResolveProzessStartLogPfad()` in `WpfTestBase.cs` auf `protected` anheben, damit zumindest die Pfadauflösung nicht erneut dupliziert werden muss.

## Geprüfte Dateien

- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_VerzeichnisAktionen.cs`
- `src/Softwareschmiede.Tests/E2E/MainTest.cs`

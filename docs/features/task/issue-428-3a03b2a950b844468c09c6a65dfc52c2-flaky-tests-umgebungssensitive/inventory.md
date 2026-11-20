# Bestandsaufnahme: Flaky Tests — umgebungssensitive Tests ohne `OsInterface`-Trait und fragile Deadlines/Cleanup

Analysiert wurde die Testinfrastruktur der Projekte `Softwareschmiede.Tests` und `Softwareschmiede.IntegrationTests` bezogen auf die Anforderung, OS-nahe Tests mit dem `OsInterface`-Trait zu markieren, Host-/Kind-Deadlines anzugleichen, das Fixture-Cleanup zu härten und eine Flaky-/Retry-Konvention festzulegen.

## Zusammenfassung

- **Kernbefund bestätigt:** Die drei echten Prozess-/Identitäts-Tests in `src/Softwareschmiede.IntegrationTests/Services/ScmEventCliExchangeAccessTests.RestrictedProcess.cs` (`RestrictedWindowsProcess_ReadsWritesAndCompletesChannel_ButCannotWriteAppData`, 6 Theory-Fälle; `RestrictedWindowsProcess_WithoutDedicatedCapability_DeniesChannel`, 3 Fälle; `RestrictingSid_RejectsAccountsAndBroadPrincipals`, 1 Fact) tragen **kein** `OsInterface`-Trait und liefen im dokumentierten Ausgangslauf in der regulären, blockierenden Lane (alle 10 Fälle bestanden).
- **Deadline-Mismatch bestätigt:** Host-Pollfenster `probeDeadline`/`deadline` je `AddSeconds(30)` (Zeilen 137, 154) vs. Kind-Budget `$phaseTimeout = 90` (Zeile 56 des eingebetteten PowerShell-Skripts).
- **Fragiles Cleanup bestätigt:** `ScmEventCliExchangeAccessTests.Dispose()` (Zeile 329) ist ein nacktes `Directory.Delete(_root, recursive: true)` ohne Retry/Logging; der Kindprozess-Kill steht nur im `finally` des Testbodys (Zeilen 184, 250), nicht auf Fixture-Ebene. Gleiches Muster findet sich in `ScmEventExchangeServiceTests`/`ScmEventExchangeAttachmentTests` (mit Attribut-Normalisierung, aber ohne Retry) und `StartPs1IntegrationTests`.
- **OsInterface-Infrastruktur vorhanden, aber nur in `Softwareschmiede.Tests`:** `OsInterfaceAttribute`, `OsInterfaceFactAttribute`, `OsInterfaceTheoryAttribute`, `TestCategories.OsInterface` unter `src/Softwareschmiede.Tests/Infrastructure/Testing/`. `Softwareschmiede.IntegrationTests` hat keine Projektreferenz darauf, nutzt aber bereits `<Compile Include … Link …>` für `ScmDispatchFixture.cs` (csproj Zeile 35) und `[Trait("Category", "OsInterface")]` auf Klassenebene in `StartPs1IntegrationTests.cs` (Zeile 10).
- **Audit-Ergebnis:** Neben den drei RestrictedProcess-Tests gibt es in `Softwareschmiede.Tests` drei Testdateien mit echten Prozessstarts ohne Trait (`ScmEventExchangeAttachmentTests.cs` — `cmd mklink /J` + `git`; `ScmEventExchangeServiceTests.cs` — `git`; `GitHubEventSourceTests.cs` — `cmd mklink /J`) sowie eine indirekte Prozessstart-Quelle: `ScmDispatchFixture` startet pro gemockter Session ein echtes `cmd.exe /c ping -n 300` und `git init` — genutzt von 25 Integrationstest-Dateien. Alle übrigen Heuristik-Treffer sind Fehl-Positive (Fake-Launcher/In-Memory-Streams, `partial End2EndTest`-Szenario-Methoden, Helper ohne Testmethoden).
- **Kein `Flaky`-Trait/Mechanismus vorhanden** — die Konvention muss neu festgelegt werden (Suche nach `flaky` in `src/`, `docs/help/`, `scripts/` ohne Treffer).
- **CI-Lanes existieren bereits:** `build & test` (`Category!=OsInterface`, blockierend) und `os-interface tests` (`Category=OsInterface`, `continue-on-error`, 30 min, Env-Flags `SOFTWARESCHMIEDE_SKIP_WPF_E2E_TESTS=1`/`SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`) in `.github/workflows/pr-staging-ci.yml` und analog `staging-ci.yml`; lokale Lane via `scripts/Run-AllTestsIndividually.ps1 -TestSet OsInterface`.
- **Umgebungsbeobachtung:** In dieser Sandbox löste `cmd.exe /c timeout /t 30`-Kindprozesse das GNU-`timeout` (MSYS `usr/bin` vor `System32` im PATH) auf — die Tests bestanden trotzdem, ein Hinweis auf schwer sichtbare Umgebungsabhängigkeit trivialer Prozessstarts.

**Test-Ausgangszustand:** Alle vier Lanes liefen vollständig durch — regulär `Softwareschmiede.Tests` 2692/2695 bestanden (3 plattformbedingt übersprungen, 0 Fehler), regulär `Softwareschmiede.IntegrationTests` 366/366 (0 Fehler), `OsInterface` `Softwareschmiede.Tests` 51/71 (20 flag-bedingt übersprungen, 0 Fehler), `OsInterface` `Softwareschmiede.IntegrationTests` 9/9. **Kein nachgewiesener bestehender Testfehler.** Nachweis und Lücken (u. a. 20 nicht ausgeführte FlaUI-/ConPTY-E2E-Tests): [inventory/tests.md](inventory/tests.md).

## Details

- [Audit: OS-nahe Tests und Tagging-Lücken](inventory/audit.md)
- [Tests und Test-Ausgangszustand](inventory/tests.md)
- [Logik- und Infrastrukturklassen (Helfer, Fixtures, Attribute)](inventory/logic.md)
- [CI-/Skript-/Konventions-Infrastruktur](inventory/infrastructure.md)
- [Interfaces](inventory/interfaces.md)

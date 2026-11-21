# OS-Schnittstellen-Tests

Regulaere Tests sind deterministische Unit-, Service- und ViewModel-Tests ohne echte Betriebssystem-Schnittstellen. Sie duerfen keine echten ConPTY-Sitzungen, Prozessstarts, Clipboard-Zugriffe oder lockanfaellige Dateisystem-Szenarien benoetigen.

OS-Schnittstellen-Tests pruefen genau diese Umgebungskontakte. Sie bleiben erhalten, werden aber getrennt markiert und ausgewertet.

## Klassifizierungskriterium

Massgeblich ist, ob die **Assertion** eines Tests am OS-Verhalten des Beteiligten haengt — nicht, ob irgendwo ein `Process.Start` vorkommt:

- Als OS-Schnittstelle gelten: eingeschraenkte Tokens/SIDs, `CreateProcessAsUser`, ConPTY, FlaUI-Fenster, Clipboard, NTFS-Reparse-Punkte/Junctions, lockanfaellige Dateisystem-Szenarien.
- Als triviales Fixture-Plumbing gelten (bleiben regulaer): `git`-Helfer-Aufrufe (`init`, `check-ignore`, `add`, `status`, `ls-files`) und Platzhalter-Prozesse wie `cmd.exe /c ping -n 300` im `ScmDispatchFixture`. Diese Spawns sind historisch stabil; ihre Assertion haengt am Service-Verhalten, nicht am Spawn.

## Kategorie

OS-nahe Tests tragen xUnit-kompatibel:

```csharp
[Trait("Category", "OsInterface")]
```

Im Testprojekt `Softwareschmiede.Tests` stehen dafuer zentrale Attribute bereit:

```csharp
[OsInterface]
[OsInterfaceFact]
[OsInterfaceTheory]
```

Im Projekt `Softwareschmiede.IntegrationTests` wird weiterhin direkt `[Trait("Category", "OsInterface")]` auf Methode oder Klasse gesetzt (Muster: `StartPs1IntegrationTests.cs`); die Attributklassen sind dort nur per Compile-Link als Abhaengigkeit des `FlakyConventionTests` vorhanden.

Vorhandene Spezialkategorien wie `E2E` und `ConPTY` bleiben bestehen. `OsInterface` wird zusaetzlich gesetzt, damit alte lokale Filter weiter nutzbar bleiben.

## `Flaky`-Kategorie

`Flaky` ist eine reine Markierungs-Kategorie fuer nachweislich umgebungsflaky OS-nahe Tests. Sie berechtigt zum einmaligen automatischen Retry in der best-effort-Lane.

Vergaberegeln:

- `Flaky` wird **nur in Kombination mit `OsInterface`** vergeben — nie in der regulaeren Lane (abgesichert durch `FlakyConventionTests`, der per `<Compile Include>`-Link in beiden Testprojekten laeuft und jeweils die eigene Assembly scannt).
- `Flaky` wird nur bei einem **dokumentierten Flake-Ereignis** (konkreter CI-/Lokal-Fehlschlag mit Fehlerbild) oder einem Analyzer-Befund aus `Analyze-FlakyTestResults.ps1` vergeben.
- Die Markierung wird entfernt, sobald die Ursache behoben ist.

Schreibweisen:

```csharp
// Softwareschmiede.Tests
[OsInterfaceFact]
[Flaky]

// Softwareschmiede.IntegrationTests
[Fact] // oder [Theory]
[Trait("Category", "OsInterface")]
[Trait("Category", "Flaky")]
```

## Lokale Befehle

```powershell
# Nur regulaere Tests
dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"

# Nur OS-Schnittstellen-Tests
dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category=OsInterface"

# Nur Flaky-markierte Tests
dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category=Flaky"

# Einzeltest-Skript, regulaerer Standardlauf
./scripts/Run-AllTestsIndividually.ps1

# Einzeltest-Skript fuer OS-Schnittstellen-Tests
./scripts/Run-AllTestsIndividually.ps1 -TestSet OsInterface
```

Die Kategorie-Konvention gilt auch fuer `Softwareschmiede.IntegrationTests`.

## Automatischer Retry (Flaky)

Der automatische Einmal-Retry greift ausschliesslich fuer `Flaky`-markierte Tests und existiert nur in der best-effort-Lane — die blockierende Lane bleibt retry-frei.

**In der CI** (`os-interface tests`-Job in `pr-staging-ci.yml` und `staging-ci.yml`): Schlaegt ein Testschritt fehl, laeuft der Schritt `Retry flaky tests once` (`if: failure()`) `scripts/Retry-FlakyTests.ps1` je Testprojekt:

1. Primaer-TRX parsen, fehlgeschlagene Testnamen einsammeln (Theory-Datenzeilen werden auf den Methoden-FQN normalisiert).
2. Flaky-Menge via `dotnet test --no-build --list-tests --filter "Category=Flaky"` ermitteln.
3. Schnittmenge leer → „keine Flaky-Fehlschlaege", Exit 0 (kein Retry; bei Build-Fehlschlag/fehlendem TRX ebenso).
4. Sonst genau ein gefilterter Re-Run: `--filter "(Category=Flaky)&(FullyQualifiedName~...)"` → Retry-TRX `<primaryname>-flaky-retry.trx`.
5. Auswertung recovered/persistent: recovered wird als `::notice::`, persistent als `::warning::` annotiert; persistente Fehlschlaege lassen den Schritt mit Exit 1 fehlschlagen (der Job bleibt durch `continue-on-error` nicht-blockierend).

Beide TRX-Dateien (Primaer und Retry) wandern ueber das Artefakt-Glob `test-results-os-interface*.trx` ins Upload-Artefakt — der Erstfehlschlag bleibt sichtbar.

**Lokal**: `Run-AllTestsIndividually.ps1` ermittelt die Flaky-Menge pro Projekt automatisch und wiederholt einen fehlgeschlagenen Flaky-Test genau einmal ohne Rebuild (`(Flaky-Retry)`-Vermerk in der `summary.md`; der bestehende Rebuild+Retry bei Ausfuehrungsfehlern bleibt unveraendert und hat Vorrang). Alternativ manuell:

```powershell
dotnet test <projekt> --filter "Category=OsInterface" --logger "trx;LogFileName=lauf.trx"
./scripts/Retry-FlakyTests.ps1 -TestProject <projekt> -PrimaryTrx <verzeichnis>/lauf.trx
```

## Flake-Statistik

`scripts/Analyze-FlakyTestResults.ps1` wertet gesammelte TRX-Verzeichnisse aus: Lauf-Gruppierung ueber die Namenskonvention `<name>.trx` (Primaer) / `<name>-flaky-retry.trx` (Retry desselben Laufs). Als Flake-Ereignis zaehlt Fehlschlag-im-Primaer + gruen-im-Retry desselben Laufs oder Fehlschlag→gruen in aufeinanderfolgenden Primaer-Laeufen. Primaer-Outcomes ausserhalb Passed/Failed (z. B. `NotExecuted` durch die `SOFTWARESCHMIEDE_SKIP_*`-Flags) gelten als fehlende Beobachtung: Sie zaehlen weder als Ausfuehrung noch als gruener Folgelauf und unterbrechen die Fehlschlag→gruen-Sequenz nicht.

```powershell
# Lokale Laeufe
./scripts/Analyze-FlakyTestResults.ps1 -Path test-results/<zeitstempel>/trx

# CI-Artefakte
gh run download <run-id> -n test-results-os-interface-pr -D artifacts/os-pr
./scripts/Analyze-FlakyTestResults.ps1 -Path artifacts/os-pr
```

Klassifikation pro Test (Fenster: letzte `-MaxRuns 20` Primaer-Laeufe):

- `Quarantaene-Kandidat`: >= 2 Flake-Ereignisse ODER Fehlrate >= 20 % bei >= 5 Ausfuehrungen → Empfehlung: Quarantaene-Issue anlegen + Fehlerbild unten dokumentieren.
- `beobachten`: >= 1 Flake-Ereignis ODER Fehlrate >= 10 %.
- `stabil`: sonst.

Die Schwellwerte sind Startwerte und kalibrierbar (Skript-Parameter `-QuarantineMinFlakeEvents`, `-QuarantineMinFailureRate`, `-QuarantineMinExecutions`, `-WatchMinFailureRate`). Der Analyzer legt keine Issues automatisch an — er meldet Kandidaten mit Empfehlung. In der CI laeuft er als `if: always()`-Report-Schritt `Analyze flaky test results` ueber den TestResults-Verzeichnissen des Laufs (Exit immer 0).

## CI-Verhalten

Die Workflows `.github/workflows/pr-staging-ci.yml` und `.github/workflows/staging-ci.yml` bauen und testen `Softwareschmiede.Tests` und `Softwareschmiede.IntegrationTests` in getrennten Jobs:

- Job `build & test`: blockierend; `Test regular (Softwareschmiede.Tests)` und `Test regular (IntegrationTests)` laufen mit `Category!=OsInterface` und liefern die Coverage fuer das 70-%-Gate.
- Job `os-interface tests`: best-effort (`continue-on-error: true`, 30 min); `Test OS interface (Softwareschmiede.Tests)` und `Test OS interface (IntegrationTests)` laufen mit `Category=OsInterface`, gefolgt von `Retry flaky tests once` (`if: failure()`) und `Analyze flaky test results` (`if: always()`).
- TRX-Artefakte werden als `test-results-pr`/`test-results-staging` (regulaer) und `test-results-os-interface-pr`/`test-results-os-interface-staging` (OS-Lane, inkl. `*-flaky-retry.trx`) hochgeladen.

Regulaere Fehler gelten als Regressionsfehler. OS-Schnittstellen-Fehler werden sichtbar gemacht, blockieren PRs aber nicht automatisch, solange die Runner-Umgebung fuer Desktop, ConPTY oder Clipboard nicht stabil genug ist.

## Bekannte Fehlerbilder

- ConPTY ist in Sandbox-, Agenten- oder nicht interaktiven Desktop-Umgebungen nicht immer verfuegbar.
- Clipboard-Zugriffe koennen transient mit `CLIPBRD_E_CANT_OPEN` fehlschlagen.
- Dateisystem-Cleanup kann durch Locks oder Read-only-Attribute scheitern.
- Langsam startende eingeschraenkte Kindprozesse (JIT-/Serializer-Cold-Start unter CI-Last) koennen Host-Pollfenster ueberziehen — Host-Deadlines (`HostPhaseTimeoutSeconds`) sind bewusst groesser als das Kind-Phasenbudget (`ChildPhaseBudgetSeconds`), damit ein echter Kind-Timeout zuerst in `child-error.txt`/Exit-Code sichtbar wird.
- E2E-Tests koennen fehlschlagen, wenn parallel Build-Artefakte veraendert werden.
- Testhost- oder Build-Artefakt-Rennen sind Infrastrukturfehler und werden separat von Assertion-Fehlern bewertet.

## Env-Flags und Retry

`SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` bleibt fuer automatisierte Sandbox-Laeufe erhalten, in denen echte ConPTY-Isolation nicht funktioniert.

`SOFTWARESCHMIEDE_SKIP_WPF_E2E_TESTS=1` ueberspringt alle echten Fenster-E2E-Tests (FlaUI treibt ein reales WPF-Hauptfenster; Pruefung zentral in `WpfTestBase.LaunchApp` via `WpfE2EEnvironmentProbe`). Beide Flags sind im Job `os-interface tests` von `staging-ci.yml` und `pr-staging-ci.yml` gesetzt, weil GitHub-gehostete Runner keine interaktive Desktop-Session haben; lokal bleiben sie ungesetzt, damit die Lane die volle Breite abdeckt.

Retry ist nicht zur Stabilisierung regulaerer Tests vorgesehen. Das Einzeltest-Skript wiederholt nur erkannte Infrastruktur-/Ausfuehrungsfehler nach Rebuild-Recovery. Fachliche Assertion-Fehler regulaerer Tests werden nicht kaschiert. Ausnahme: der oben beschriebene `Flaky`-Einmal-Retry — er existiert nur in der best-effort-Lane, weil `Flaky` nur zusammen mit `OsInterface` vergeben wird.

**Job-Rerun-Konvention** (komplementaer zum Trait-Retry): Schlaegt der `os-interface tests`-Job ohne auswertbares TRX fehl (Runner-Abbruch, Testhost-Crash — der Retry-Schritt findet dann keine Fehlschlaege und endet neutral), ist genau ein `gh run rerun --job <job-id>` nur fuer diesen Job erlaubt — niemals `gh run rerun --failed` (das wuerde blockierende Jobs mit wiederholen). Die numerische Job-ID kommt aus `gh run view --job <id>` bzw. `gh run view <run-id> --json jobs`. Bleibt der Fehler bestehen, tritt er wiederholt auf oder meldet der Analyzer Schwellwert-Ueberschreitung, wird ein Quarantaene-Issue angelegt und das Fehlerbild unter „Bekannte Fehlerbilder" dokumentiert (inkl. Verweis auf Issue und Analyzer-Befund). Fehler in der regulaeren Lane werden niemals wiederholt — sie gelten immer als Regression.

## Audit-Heuristik

Wiederholbarer Suchbefehl fuer die Klassifikation ungetaggter Tests (Heuristik, Ergebnis fachlich bewerten — Kriterium „Assertion haengt am OS-Verhalten"):

```bash
grep -rnE 'Process\.Start|ScmRestrictedWindowsProcess|PseudoConsole|ConPty|FlaUI|Clipboard' src/Softwareschmiede.Tests src/Softwareschmiede.IntegrationTests --include='*.cs'
```

Treffer ohne `OsInterface`-Markierung pruefen: Helper/Fixtures ohne eigene Testmethoden, Fake-Launcher/In-Memory-Streams und reine `git`-/Platzhalter-Spawns sind Fehl-Positive und bleiben regulaer.

## Externe Runner

Fuer `/run-tests` gilt die expectede Auswertung:

- regulaere Fehler bilden die blockierende Fehlermenge
- OS-Schnittstellen-Fehler werden separat gelistet
- Infrastruktur-/Ausfuehrungsfehler werden getrennt ausgewiesen

Fuer `/lifecycle` gilt entsprechend: automatische Iterationsentscheidungen sollen regulaere Fehlschlaege beruecksichtigen. OS-Schnittstellen-Fehler werden dokumentiert, aber nicht als regulaerer Regressionsfehler gezaehlt.

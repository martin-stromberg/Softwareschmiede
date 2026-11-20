# Umsetzungsplan: Flaky Tests — `OsInterface`-Tagging, `Flaky`-Trait mit Einmal-Retry, Deadline-Angleichung, Cleanup-Härtung und Flake-Statistik

**Basisbranch:** `task/issue-428-3a03b2a950b844468c09c6a65dfc52c2-flaky-tests-umgebungssensitive` (Issue #428)

## Übersicht

Die reguläre, blockierende Test-Lane (`Category!=OsInterface`) wird wieder deterministisch: Die drei echten Prozess-/Identitäts-Tests in `ScmEventCliExchangeAccessTests.RestrictedProcess.cs` sowie zwei Junction-Tests in `Softwareschmiede.Tests` werden methodenweise mit `Category=OsInterface` markiert und laufen damit im bestehenden `os-interface tests`-CI-Job. Zusätzlich werden eingeführt: ein `Flaky`-Trait (`TestCategories.Flaky` + `FlakyAttribute`, im Integrationsprojekt als `[Trait("Category", "Flaky")]`) für nachweislich umgebungsflaky Tests, ein echter Einmal-Retry-Mechanismus in der CI (`scripts/Retry-FlakyTests.ps1` + neuer Schritt im `os-interface tests`-Job beider Workflows, lokale Parität über `Run-AllTestsIndividually.ps1`) und ein TRX-Flake-Analyzer (`scripts/Analyze-FlakyTestResults.ps1`), der Fehlschlag-then-Retry-grün-Ereignisse auswertet und bei Schwellwert ein Quarantäne-Issue nahelegt. Der Host-/Kind-Deadline-Mismatch (30 s vs. `$phaseTimeout = 90`) wird aufgelöst, das Fixture-`Dispose()` über einen geteilten Helfer `TempDirectoryCleanup` gehärtet (Kill vor Delete, Retry/Backoff, Diagnose statt Throw) und alle Konventionen in `docs/help/stabilitaet/os-interface-tests.md` dokumentiert. Keine Änderung an Produktlogik; die blockierende Lane bleibt retry-frei.

## Scope-Klärung

||| Feld | Festlegung |
|||------|------------|
||| Verarbeitungseinheit | Einzelne xUnit-Testmethoden in `Softwareschmiede.Tests` und `Softwareschmiede.IntegrationTests`, klassifiziert nach „deterministisch (regulär)" vs. „umgebungssensitiv (OS-Schnittstelle)", sowie eine Teilmenge davon als „nachweislich flaky" (`Flaky`, nur in Kombination mit `OsInterface`). Bewertungskriterium: Die Assertion hängt vom OS-Verhalten des Beteiligten ab (eingeschränkte Tokens/SIDs, `CreateProcessAsUser`, ConPTY, FlaUI-Fenster, Clipboard, NTFS-Reparse-Punkte/Junctions, lockanfällige Dateisystem-Szenarien) — nicht davon, ob irgendein `Process.Start` vorkommt |
||| Auslösende Ereignisse | Systemereignis: CI-Flake in PR #427 (`RestrictedWindowsProcess_ReadsWritesAndCompletesChannel_ButCannotWriteAppData(provider: "Devin", rename: False)` — 30-s-`probeDeadline` abgelaufen bei laufendem Kind, `IOException` im `Dispose()` als `AggregateException`-Overlay). Kein Nutzer-Trigger |
||| Erwartete Wirkung | (1) Reguläre Lane enthält keine Tests mehr, deren Assertion echte OS-Schnittstellen braucht — Fehlschläge dort sind wieder echte Regressionssignale. (2) Umgetaggte Tests laufen im bestehenden `os-interface tests`-Job (`continue-on-error`, 30 min, Env-Flags `SOFTWARESCHMIEDE_SKIP_WPF_E2E_TESTS=1`/`SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`). (3) `Flaky`-markierte Tests werden bei Fehlschlag in der CI genau einmal automatisch wiederholt; beide Ergebnisse (Primär- und Retry-TRX) bleiben in den Artefakten sichtbar. (4) Langsam startender eingeschränkter Kindprozess schlägt nicht mehr mit `Unknown`-Timeout fehl, solange er innerhalb seines Phasenbudgets arbeitet. (5) Cleanup-Fehler überdeckt nie wieder den eigentlichen Assert (kein `AggregateException`-Overlay). (6) Der Analyzer meldet Flake-Kandidaten und legt bei Schwellwert ein Quarantäne-Issue nahe; die dokumentierte `gh run rerun --job`-Konvention bleibt zusätzlich für Job-Infrastrukturfehler bestehen |
||| Umgang mit fehlenden Objekten | `OsInterface`-Attribute existieren nur in `Softwareschmiede.Tests` → im Integrationsprojekt direktes `[Trait("Category", "OsInterface")]` bzw. `[Trait("Category", "Flaky")]` (Muster `StartPs1IntegrationTests.cs`, Z. 10) wiederverwenden; kein `<Compile Include>` der Attributdateien. `Flaky`-Trait existiert nicht → neu erzeugen: `TestCategories.Flaky` + `FlakyAttribute` (ITraitAttribute, Muster `OsInterfaceAttribute`). Retry-Mechanismus existiert nicht → neu erzeugen: `scripts/Retry-FlakyTests.ps1` + CI-Schritte. Flake-Analyzer existiert nicht → neu erzeugen: `scripts/Analyze-FlakyTestResults.ps1`. Gemeinsamer Retry-Delete-Helfer fehlt → neu erzeugen (`TempDirectoryCleanup`, per Compile-Link wie `ScmDispatchFixture.cs` geteilt). Prozess-Registry auf Fixture-Ebene fehlt → neues Feld `_restrictedChildren` anlegen. TRX-Artefakte (`test-results-os-interface-*`) existieren bereits → als Datenbasis des Analyzers wiederverwenden |

### Scope-Ausschlüsse

||| # | Einschränkung / Ausschluss | Begründung | Status |
|||---|----------------------------|------------|--------|
||| 1 | Kein Klassen-Trait auf `ScmEventCliExchangeAccessTests` — nur methodenweises Tagging auf dem RestrictedProcess-Partial | Die `partial`-Klasse enthält ~90 deterministische Mock-/Evaluator-Tests (`ActualStartSpec_*`, `ConfigurationFixtures_*`, `PendingProbe_*` u. a.); ein Klassen-Trait würde sie alle in die best-effort-Lane schieben und die blockierende Lane-Abdeckung (inkl. 70-%-Coverage-Gate) verlieren | bestätigt (2026-11-19) |
||| 2 | Tests mit ausschließlich triviellem Tool-Spawn bleiben regulär: `GitInit`/`Git()`-Helfer (`git init`, `check-ignore`, `add`, `status`, `ls-files`) und `ScmDispatchFixture`-Platzhalter (`cmd.exe /c ping -n 300`, `git init` in `InitGit`) | Baseline: historisch stabil (0 Fehler in 4 Lane-Läufen). Ein Tagging würde `ScmEventExchangeServiceTests`, `ScmEventExchangeAttachmentTests` (Ctor-Git) und 25 Fixture-Nutzer-Dateien in die best-effort-Lane verschieben — Verlust des Regressions-Signals der regulären Lane. Kriterium: die Assertion hängt nicht am OS-Verhalten des Spawns, sondern am Service-Verhalten | bestätigt (2026-11-19) |
||| 3 | ~~Kein `Flaky`-Trait und kein automatisierter Retry-Mechanismus~~ | **Aufgehoben** durch Anwender-Entscheidung vom 2026-11-19 („Beides"): `Flaky`-Trait + Einmal-Retry in der CI werden umgesetzt | aufgehoben (2026-11-19) |
||| 4 | Keine inhaltliche Änderung der getesteten Produktlogik (`ScmPromptSessionAdapter`, `ScmEventExchangeProtocol`, `ScmRestrictedProcessAccess`, `ScmRestrictedWindowsProcess` u. a.) | Anforderung betrifft nur Test-Rahmen, Zeitbudgets und Kategorisierung | bestätigt (2026-11-19) |
||| 5 | Kein Blanket-Retry der Gesamtsuite und kein Retry regulärer Tests — der automatische Einmal-Retry greift ausschließlich für `Flaky`-markierte Tests in der best-effort-Lane | Bestehende Regel aus `docs/help/stabilitaet/os-interface-tests.md`; echte Regressionen dürfen nicht maskiert werden | bestätigt (2026-11-19) |
||| 6 | ConPTY-Skip bleibt das manuelle Env-Flag `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS` | Auto-Detection wurde bereits bewusst verworfen (Issue 114, false-negative-Probes in Visual Studio) | bestätigt (2026-11-19) |
||| 7 | Keine vollständige Deterministik echter UI-/Prozess-Tests | Zielbild laut Issue: „selten, lokalisiert, selbstdiagnostizierend" | bestätigt (2026-11-19) |
||| 8 | ~~Optionale TRX-Flake-Statistik wird nicht umgesetzt~~ | **Aufgehoben** durch Anwender-Entscheidung vom 2026-11-19: Analyzer `Analyze-FlakyTestResults.ps1` wird jetzt umgesetzt | aufgehoben (2026-11-19) |
||| 9 | `Flaky`-Trait darf nur in Kombination mit `OsInterface` vergeben werden → der automatische Retry existiert nur in der best-effort-Lane; die blockierende Lane erhält keinerlei Retry-Mechanik | Hält die Gate-Struktur des `build & test`-Jobs unverändert (geringstes Mechanismus-Risiko). Umgebungsflaky Tests gehören ohnehin in die OS-Lane; wird ein regulärer Test flaky, ist die dokumentierte Reaktion: zuerst nach `OsInterface`+`Flaky` umklassifizieren, nicht die reguläre Lane aufweichen. Durch Meta-Test `FlakyConventionTests` in `Softwareschmiede.Tests` abgesichert | bestätigt (2026-11-19) |
||| 10 | Der Analyzer legt Quarantäne-Issues nicht automatisch an — er meldet Kandidaten und gibt eine Empfehlung aus | Automatisches Issue-Anlegen aus CI heraus bräuchte Token-Berechtigungen und Duplikat-Erkennung; die Empfehlung plus dokumentierte Konvention reicht für den Anwendungsfall | bestätigt (2026-11-19) |

## Designentscheidungen

||| Komponente / Bereich | Gewählter Ansatz | Begründung |
|||----------------------|-----------------|------------|
||| Trait-Mechanismus im Integrationsprojekt | Direktes `[Trait("Category", "OsInterface")]` + `[Trait("Category", "Flaky")]` auf den drei Testmethoden | Vorhandenes Muster in `StartPs1IntegrationTests.cs` (Z. 10); keine csproj-Änderung für Attributdateien, keine Typ-Duplikation in einer zweiten Assembly. Verifikationsschritt prüft `--list-tests --filter "Category=OsInterface"` bzw. `"Category=Flaky"` |
||| `Flaky`-Trait | `TestCategories.Flaky = "Flaky"` + `FlakyAttribute` (`ITraitAttribute`, Method\|Class) in `src/Softwareschmiede.Tests/Infrastructure/Testing/` — **keine** `FlakyFact`/`FlakyTheory`-Varianten | `Flaky` ist eine reine Markierungs-Kategorie, die immer zusätzlich zu einem bestehenden Test-Attribut (`[OsInterfaceFact]`/`[OsInterfaceTheory]`/`[Trait]`) gesetzt wird; eigene Fact/Theory-Attribute wären redundante Vervielfältigung des OsInterface-Musters |
||| Retry-Variante | **CI-Workflow + geteiltes PowerShell-Skript:** `scripts/Retry-FlakyTests.ps1` (TRX-Auswertung → einmaliger gefilterter Re-Run nur der `Flaky`-markierten Fehlschläge), aufgerufen als `if: failure()`-Schritt im `os-interface tests`-Job von `pr-staging-ci.yml` und `staging-ci.yml`; lokale Parität durch analoge Erweiterung von `Run-AllTestsIndividually.ps1` | Geringstes Mechanismus-Risiko, das in der CI **tatsächlich greift**: `Run-AllTestsIndividually.ps1` wird von der CI nicht verwendet (die Workflows rufen `dotnet test` direkt) — ein Retry nur dort würde in der CI nie ausgelöst. Ein xUnit-seitiger Mechanismus hat keinen Bord-Retry in xUnit v3 (VSTest-Modus) und würde über Third-Party-Paket oder Custom-Testframework neue Infrastruktur erfordern — höheres Risiko; zudem würde ein runner-interner Retry den Erstfehlschlag aus dem TRX tilgen und damit das Fehlerbild maskieren. Der TRX-basierte Re-Run lässt beide Ergebnisse in den Artefakten sichtbar („selbstdiagnostizierend") und ist lokal 1:1 reproduzierbar |
||| Flaky-Retry nur in der best-effort-Lane | `Flaky` wird konventionsgemäß nur mit `OsInterface` vergeben; Retry-Schritt nur im `os-interface tests`-Job | Ein Retry im blockierenden `build & test`-Job müsste dessen Gate-Struktur umbauen (`continue-on-error` am Testschritt + nachgelagerter Bewertungsschritt) — unverhältnismäßiges Risiko; und die Fälle, die retry-würdig sind, sind per Definition umgebungssensitiv und gehören in die OS-Lane (Ausschluss 9) |
||| Cleanup-Helfer | Neue `internal static`-Klasse `TempDirectoryCleanup` in `src/Softwareschmiede.Tests/Helpers/`, Namespace `Softwareschmiede.TestSupport` (wie `ScmDispatchFixture`), per `<Compile Include … Link>` ins Integrationsprojekt | Das fragile `Directory.Delete`-Muster existiert an 6 dokumentierten Stellen in beiden Projekten; ein geteilter Helfer vermeidet sechsfache Duplikation von Kill-/Retry-/Backoff-Logik und etabliert eine einzige Konvention. Service-Layer-Analogie: zentraler Gateway für „Temp-Verzeichnis best-effort löschen" |
||| Prozess-Kill vor Delete | Neues Fixture-Feld `_restrictedChildren` (`List<Process>`) in `ScmEventCliExchangeAccessTests`; Registrierung direkt nach `ScmRestrictedWindowsProcess.Start`; `Dispose` killt Survivors best-effort | Der Test-`finally` bleibt primärer Kill-Pfad; die Registry ist das Sicherheitsnetz für Pfade, in denen der `finally` nicht oder noch nicht gewirkt hat (Handle-Release-Latenz, abgebrochene Testausführung) |
||| Host-Deadline | Neue Konstanten `ChildPhaseBudgetSeconds = 90` und `HostPhaseTimeoutSeconds = ChildPhaseBudgetSeconds + 30`; der eingebettete PowerShell-`$phaseTimeout` wird per `{{…}}`-Interpolation aus `ChildPhaseBudgetSeconds` gespeist; `probeDeadline`, `deadline` und beide `WaitAsync(TimeSpan.FromSeconds(15))` nutzen `HostPhaseTimeoutSeconds` | Macht die Kopplung Host ↔ Kind explizit statt zweier loses gekoppelter Literale (30 vs. 90); Host > Kind-Budget garantiert, dass das Kind bei echtem Timeout zuerst seinen eigenen Fehler (`child-error.txt`, Exit 1) meldet — die Diagnose bleibt führend |
||| Flake-Analyzer | PowerShell-Skript `scripts/Analyze-FlakyTestResults.ps1` (kein .NET-Tool), auswertbar lokal gegen beliebige TRX-Verzeichnisse (`test-results/`, per `gh run download` geholte CI-Artefakte) und als reiner Report-Schritt (`if: always()`, exit 0) im `os-interface tests`-Job | Passt zur bestehenden Skript-Landschaft (`Run-AllTestsIndividually.ps1` mit bereits vorhandener TRX-Parsing-Logik als Muster); kein zusätzliches Projekt, keine Assembly, keine NuGet-Abhängigkeit. CI-Einbindung nur als Sichtbarkeits-Report — das eigentliche Langzeit-Signal kommt aus der lokalen Auswertung gesammelter Artefakte |
||| Quarantäne-Schwellwert | Fenster: letzte `-MaxRuns 20` Läufe. **Quarantäne-Empfehlung**: ≥ 2 Flake-Ereignisse (Fehlschlag im Primär-TRX + grün im Retry-TRX desselben Laufs bzw. Fehlschlag→grün in aufeinanderfolgenden Primär-TRX) im Fenster ODER Fehlrate ≥ 20 % bei ≥ 5 Primär-Ausführungen. **Beobachten**: ≥ 1 Flake-Ereignis oder Fehlrate ≥ 10 % | Schwellwerte bewusst niedrig gewählt, weil Flake-Ereignisse in der best-effort-Lane selten und damit jedes einzelne aussagekräftig sind; als Startwerte in der Doku als kalibrierbar markiert |
||| Rerun-/Quarantäne-Konvention | Bleibt zusätzlich dokumentiert: `gh run rerun --job <job-id>` nur für `os-interface tests` bei Job-/Infrastrukturfehlern (z. B. Runner-Abbruch, leeres/fehlendes TRX); Quarantäne-Issue + Fehlerbild-Eintrag bei Hartnäckigkeit (jetzt analyzer-gestützt) | Der Trait-Retry behandelt Einzeltest-Flakes innerhalb des Jobs; die Job-Rerun-Konvention behandelt Ausfälle, die der TRX-Retry nicht erfassen kann (Job abgebrochen, Testhost-Crash ohne TRX-Ergebnis) — beide Ebenen ergänzen sich |

## Programmabläufe

### Lane-Einordnung umgetaggter Tests

1. Testmethode trägt `Category=OsInterface` (via `[Trait]` im Integrationsprojekt bzw. `[OsInterfaceFact]` in `Softwareschmiede.Tests`); die drei RestrictedProcess-Methoden tragen zusätzlich `Category=Flaky`.
2. CI-Job `build & test` (blockierend) filtert `--filter "Category!=OsInterface"` → Methode läuft dort nicht mehr; Fehlschläge in dieser Lane sind wieder reine Regressionssignale.
3. CI-Job `os-interface tests` (`continue-on-error`, Env-Flags gesetzt) filtert `--filter "Category=OsInterface"` → Methode läuft dort automatisch mit; kein Workflow-Umbau für die Lane nötig.
4. Lokal gleiche Trennung: `dotnet test … --filter "Category=OsInterface"` bzw. `scripts/Run-AllTestsIndividually.ps1 -TestSet OsInterface`.

Beteiligte Klassen/Komponenten: `ScmEventCliExchangeAccessTests` (partial), `ScmEventExchangeAttachmentTests`, `GitHubEventSourceTests`, `OsInterfaceFactAttribute`, `FlakyAttribute`, `TraitAttribute` (xUnit v3), `.github/workflows/pr-staging-ci.yml`/`staging-ci.yml`.

### Automatischer Flaky-Retry in der CI (`os-interface tests`-Job)

1. `dotnet test --filter "Category=OsInterface"` schreibt Primär-TRX (`test-results-os-interface-{tests,integration}.trx`).
2. Schlägt ein Testschritt fehl, läuft der neue Schritt `Retry flaky tests once` (`if: failure()`) `scripts/Retry-FlakyTests.ps1 -TestProject <csproj> -PrimaryTrx <trx>` je Testprojekt:
   - Primär-TRX parsen → `UnitTestResult outcome="Failed"`-Testnamen (Theory-Datenzeilen auf Methoden-FQN normalisieren).
   - Flaky-Menge ermitteln via `dotnet test <projekt> --no-build --list-tests --filter "Category=Flaky"` — robust gegenüber TRX-Kategorie-Serialisierung (Muster: `Get-TestTargets` in `Run-AllTestsIndividually.ps1`).
   - Schnittmenge leer → Meldung „keine Flaky-Fehlschläge", Exit 0 (kein Retry).
   - Sonst genau ein Re-Run: `dotnet test --no-build --filter "(Category=Flaky)&(FullyQualifiedName~name1|FullyQualifiedName~name2)" --logger "trx;LogFileName=<primär>-flaky-retry.trx"`.
   - Retry-TRX auswerten → `recovered` (fehlgeschlagen→bestanden) vs. `persistent`; im CI-Kontext (`GITHUB_ACTIONS`) als `::notice::`/`::warning::`-Annotationen. Exit 1 bei persistenten Fehlschlägen — ehrliches Signal, Job bleibt durch `continue-on-error` nicht-blockierend.
3. Retry-TRX wandert über das erweiterte Artefakt-Glob `test-results-os-interface*.trx` mit ins Upload-Artefakt; beide Ergebnisse bleiben auswertbar.
4. Anschließend `if: always()`-Schritt `Analyze flaky test results` → `scripts/Analyze-FlakyTestResults.ps1` über dem `TestResults`-Verzeichnis des Laufs: reiner Report (Exit 0), `::warning::` für Quarantäne-Kandidaten.
5. Persistente Flaky-Fehler oder wiederholte Flake-Ereignisse → Fehlerbild-Eintrag + Quarantäne-Issue (Konvention, analyzer-gestützt). `gh run rerun --job <job-id>` nur bei Job-/Infrastrukturfehlern, nicht als Ersatz für den Trait-Retry.

Beteiligte Klassen/Komponenten: `scripts/Retry-FlakyTests.ps1` (neu), `scripts/Analyze-FlakyTestResults.ps1` (neu), `.github/workflows/pr-staging-ci.yml`, `.github/workflows/staging-ci.yml`, `TestCategories.Flaky`.

### Lokaler Flaky-Retry (Einzeltest-Skript)

1. `Run-AllTestsIndividually.ps1` ermittelt pro Projekt zusätzlich die Flaky-Menge (`--list-tests --filter "Category=Flaky"`, wiederverwendet `Get-TestTargets`-Logik).
2. Ergibt ein Einzeltest `Fehlgeschlagen` **und** gehört sein `FilterName` zur Flaky-Menge → genau ein Re-Run (ohne Rebuild; der bestehende Rebuild+Retry bei `IsRuntimeError` bleibt unverändert und hat Vorrang).
3. Ergebnis wird als Flaky-Retry in der `summary.md` ausgewiesen (`(Flaky-Retry)`-Vermerk analog `(nach Wiederholung)`); beide TRX-Dateien bleiben im `trx/`-Verzeichnis.
4. Alternativ bleibt die manuelle Variante dokumentiert: `dotnet test … --logger trx` gefolgt von `scripts/Retry-FlakyTests.ps1`.

Beteiligte Klassen/Komponenten: `scripts/Run-AllTestsIndividually.ps1`, `TestCategories.Flaky`.

### TRX-Flake-Statistik (Analyzer)

1. Aufruf lokal: `scripts/Analyze-FlakyTestResults.ps1 -Path <verzeichnis>` über gesammelten TRX-Dateien — entweder lokale Läufe (`test-results/<zeitstempel>/trx`, `src/*/TestResults`) oder CI-Artefakte per `gh run download <run-id> -n test-results-os-interface-{pr,staging}`.
2. Gruppierung in „Läufe" über Dateinamens-Konvention: `<name>.trx` = Primär, `<name>-flaky-retry.trx` = Retry desselben Laufs; darüber hinaus zählt ein Fehlschlag im Primär-TRX des Laufs N gefolgt von grün im Primär-TRX von Lauf N+1 ebenfalls als Flake-Ereignis.
3. Pro Testname: Primär-Ausführungen, Fehlschläge, Flake-Ereignisse, Fehlrate.
4. Klassifikation: `stabil` | `beobachten` (≥ 1 Flake-Ereignis oder Fehlrate ≥ 10 %) | `Quarantäne-Kandidat` (≥ 2 Flake-Ereignisse im Fenster oder Fehlrate ≥ 20 % bei ≥ 5 Ausführungen); für Kandidaten Empfehlungstext „Quarantäne-Issue anlegen + Fehlerbild in `os-interface-tests.md` dokumentieren".
5. In der CI läuft derselbe Aufruf als Report-Schritt über dem aktuellen Lauf-Verzeichnis (sichtbar im Job-Log und als Annotationen), ohne Gate.

Beteiligte Klassen/Komponenten: `scripts/Analyze-FlakyTestResults.ps1` (neu), TRX-Artefakte `test-results-os-interface-*`/`test-results-{pr,staging}`.

### RestrictedProcess-Test mit angeglichenen Deadlines

1. Test startet via `ScmRestrictedWindowsProcess.Start` den eingeschränkten PowerShell-Kindprozess und registriert das `Process`-Objekt in `_restrictedChildren`.
2. Kind arbeitet drei sequentielle Phasen mit je `ChildPhaseBudgetSeconds` (90 s) eigenem Budget; Phasenfortschritt wird in `child-progress.txt` geschrieben (unverändert).
3. Host pollt `access.Status == Unknown` bzw. Kanaldateien bis `HostPhaseTimeoutSeconds` (120 s) — länger als das Kind-Phasenbudget, sodass ein echter Kind-Timeout zuerst in `child-error.txt`/`ExitCode 1` sichtbar wird.
4. Assert meldet wie bisher `access.Reason`, child phase/state/error — unverändert.
5. Test-`finally` killt den Kindprozessbaum (`Kill(entireProcessTree: true)` + `WaitForExitAsync`) — unverändert primärer Pfad.
6. `Dispose()` ruft `TempDirectoryCleanup.DeleteBestEffort(_root, _restrictedChildren)` — Kill-Sicherheitsnetz + Retry-Delete + Diagnose statt Throw.

Beteiligte Klassen/Komponenten: `ScmEventCliExchangeAccessTests`, `ScmRestrictedWindowsProcess`, `ScmPromptSessionAdapter`, `ScmEventExchangeProtocol`, `TempDirectoryCleanup`.

### Gehärtetes Fixture-/Temp-Verzeichnis-Cleanup

1. `TempDirectoryCleanup.DeleteBestEffort(root, processesToKill?, normalizeAttributes)`:
   - Für jeden registrierten Prozess: `if (!HasExited) { Kill(entireProcessTree: true); WaitForExit }` in try/catch (best-effort, Muster `ScmDispatchFixture.DisposeAsync`). Die Kill-Schleife muss robust gegen jede Einzelprozess-Ausnahme sein — die Testmethoden nutzen `using var process`, d. h. registrierte `Process`-Objekte sind beim `Dispose` typischerweise bereits disposed (`ObjectDisposedException`, Subtyp von `InvalidOperationException`); `Win32Exception`/ähnliches wird ebenfalls geschluckt.
   - Optional (git-Repos): `File.SetAttributes(file, FileAttributes.Normal)` für alle Dateien (übernimmt die bisher in `ScmEventExchangeServiceTests`/`…AttachmentTests` duplizierte Normalisierung).
   - `Directory.Delete(root, recursive: true)` mit Retry/Backoff bei `IOException`/`UnauthorizedAccessException` (begrenzte Versuche, kurzes Gesamtbudget ~2 s).
   - Letzter Fehlversuch: Fehler nur diagnostisch ausgeben (`Console.Error` bzw. xUnit-Diagnosekanal, soweit in `Dispose` verfügbar), **nicht** werfen → kein `AggregateException`-Overlay über dem eigentlichen Testergebnis.
2. Aufrufer: `ScmEventCliExchangeAccessTests.Dispose` (mit `_restrictedChildren`), `ScmDispatchFixture.DisposeAsync`, `ScmEventExchangeServiceTests.Dispose`, `ScmEventExchangeAttachmentTests.Dispose` (nach `RemoveReparsePoints`), `GitHubEventSourceTests`-`finally`, `StartPs1IntegrationTests.Dispose`, `TempDirectory.Dispose`, `TestTempDirectoryFixture.Dispose`.

Beteiligte Klassen/Komponenten: `TempDirectoryCleanup` (neu), alle oben genannten Dispose-/`finally`-Stellen.

### Dokumentierte Rerun-/Quarantäne-Konvention (komplementär zum Trait-Retry)

1. `os-interface tests`-Job schlägt fehl → TRX-Artefakt `test-results-os-interface-*` und Fehlermeldung gegen die Liste „Bekannte Fehlerbilder" in `os-interface-tests.md` prüfen.
2. **Einzeltest-Flake** (Test schlug fehl, TRX vorhanden): der automatische Trait-Retry hat bereits gegriffen — Ergebnis im Retry-TRX prüfen; persistent → Quarantäne-Pfad.
3. **Job-/Infrastrukturfehler** (Job abgebrochen, Testhost-Crash ohne auswertbares TRX): genau ein `gh run rerun --job <job-id>` nur für diesen Job, niemals `gh run rerun --failed` (würde blockierende Jobs mit wiederholen).
4. Fehler bleibt bestehen, tritt wiederholt auf oder der Analyzer meldet Schwellwert-Überschreitung → Quarantäne-Issue anlegen und Fehlerbild in „Bekannte Fehlerbilder" dokumentieren (inkl. Verweis auf Issue und Analyzer-Befund).
5. Fehler in der regulären Lane → niemals Rerun, immer als Regression behandeln (bestehende Regel bleibt).

Beteiligte Klassen/Komponenten: `docs/help/stabilitaet/os-interface-tests.md`, GitHub-CLI, `Analyze-FlakyTestResults.ps1`.

## Neue Klassen

||| Klasse | Typ | Zweck |
|||--------|-----|-------|
||| `TempDirectoryCleanup` | `internal static` Hilfsklasse (`src/Softwareschmiede.Tests/Helpers/TempDirectoryCleanup.cs`, Namespace `Softwareschmiede.TestSupport`; per `<Compile Include … Link="Infrastructure\TempDirectoryCleanup.cs">` ins Integrationsprojekt) | Best-effort-Cleanup temporärer Testverzeichnisse: registrierte Prozesse killen (`Kill(entireProcessTree: true)` + `WaitForExit`, Ausnahmen geschluckt), optionale Attribut-Normalisierung für git-Read-only-Dateien, `Directory.Delete` mit Retry/Backoff auf `IOException`/`UnauthorizedAccessException`, Restfehler nur Diagnoseausgabe statt Throw |
||| `FlakyAttribute` | `sealed`-Attributklasse (`src/Softwareschmiede.Tests/Infrastructure/Testing/FlakyAttribute.cs`), `ITraitAttribute` (xUnit v3), `[AttributeUsage(Method \| Class)]` | Liefert `Category = TestCategories.Flaky`; markiert einen OS-nahen Test als nachweislich umgebungsflaky → berechtigt zum einmaligen automatischen Retry; XML-Doc verweist auf die Konvention „nur mit `OsInterface` kombinieren" |
||| `scripts/Retry-FlakyTests.ps1` | PowerShell-Skript (kein .NET-Code) | TRX des Primärlaufs auswerten, fehlgeschlagene `Flaky`-Tests genau einmal gefiltert erneut ausführen, Ergebnis (recovered/persistent) ausgeben; Parameter `-TestProject`, `-PrimaryTrx`, `-Configuration` (Default `Debug`), `-RetryTrxFileName` (Default `<primärname>-flaky-retry.trx` im selben TestResults-Verzeichnis) |
||| `scripts/Analyze-FlakyTestResults.ps1` | PowerShell-Skript | Flake-Statistik über TRX-Verzeichnisse: Lauf-Gruppierung (Primär/`-flaky-retry`-Paarung), Flake-Ereignisse, Fehlraten, Schwellwert-Klassifikation (`stabil`/`beobachten`/`Quarantäne-Kandidat`); Parameter `-Path`, `-MaxRuns` (Default 20), Schwellwert-Parameter mit dokumentierten Defaults; Exit immer 0 (reiner Report) |
||| `FlakyConventionTests` | Testklasse (`Softwareschmiede.Tests`, reguläre Lane, deterministisch) | Meta-Test per Reflection über die eigene Assembly: jede Methode/Klasse, deren `ITraitAttribute`-Attribute `Category=Flaky` liefern, muss zusätzlich `Category=OsInterface` liefern — sichert Ausschluss 9 ab |

## Änderungen an bestehenden Klassen

### `TestCategories` (statische Konstantenklasse, `Softwareschmiede.Tests/Infrastructure/Testing/`)

- **Neue Member:** `Flaky` (`public const string = "Flaky"`) — zentraler Kategoriename für `--filter "Category=Flaky"`.

### `ScmEventCliExchangeAccessTests` (Testklasse, `ScmEventCliExchangeAccessTests.cs`, Integrationsprojekt)

- **Neue Felder:** `_restrictedChildren` (`List<Process>`) — Registry der gestarteten eingeschränkten Kindprozesse für Kill-vor-Delete (xUnit führt Tests einer Klasse sequentiell aus; `List` genügt).
- **Geänderte Methoden:** `Dispose()` — statt nacktem `Directory.Delete(_root, recursive: true)` (Z. 329): `TempDirectoryCleanup.DeleteBestEffort(_root, _restrictedChildren)`.

### `ScmEventCliExchangeAccessTests` (Partial, `ScmEventCliExchangeAccessTests.RestrictedProcess.cs`, Integrationsprojekt)

- **Neue Attribute:** `[Trait("Category", "OsInterface")]` **und** `[Trait("Category", "Flaky")]` zusätzlich auf `RestrictedWindowsProcess_ReadsWritesAndCompletesChannel_ButCannotWriteAppData` (`[Theory]`, 6 Fälle), `RestrictedWindowsProcess_WithoutDedicatedCapability_DeniesChannel` (`[Theory]`, 3 Fälle), `RestrictingSid_RejectsAccountsAndBroadPrincipals` (`[Fact]`, nutzt `WindowsIdentity.GetCurrent()`). Die `Flaky`-Markierung ist der dokumentierte PR-#427-Flake-Kandidat.
- **Neue Konstanten:** `ChildPhaseBudgetSeconds = 90`, `HostPhaseTimeoutSeconds = ChildPhaseBudgetSeconds + 30` — mit Kommentar, der die Kopplung an das eingebettete Kind-Budget dokumentiert.
- **Geänderte Methoden:**
  - `RestrictedWindowsProcess_ReadsWritesAndCompletesChannel_ButCannotWriteAppData` — `$phaseTimeout = 90` im eingebetteten Skript per `{{ChildPhaseBudgetSeconds}}`-Interpolation gespeist; `probeDeadline` (Z. 137) und `deadline` (Z. 154) auf `HostPhaseTimeoutSeconds`; `WaitAsync(TimeSpan.FromSeconds(15))` (Z. 176) auf `HostPhaseTimeoutSeconds` (gleiche Mismatch-Klasse: Kind hat in Phase 3 bis zu 90 s Budget, Host wartete nur 15 s); `process` nach `Start` in `_restrictedChildren` registrieren.
  - `RestrictedWindowsProcess_WithoutDedicatedCapability_DeniesChannel` — `WaitAsync(TimeSpan.FromSeconds(15))` (Z. 238) auf `HostPhaseTimeoutSeconds`; `process` registrieren.
- **Unverändert:** Diagnose-Ausgabe (child phase/state/error), Test-`finally`-Kill, alle Asserts.

### `ScmEventExchangeAttachmentTests` (Testklasse, `Softwareschmiede.Tests`)

- **Geänderte Attribute:** `Collect_RejectsReparsePointInDeclaredPath` — `[Fact]` → `[OsInterfaceFact]` (echter `cmd.exe /c mklink /J`-Junction-Spawn; Assertion hängt an NTFS-Reparse-Verhalten); `using Softwareschmiede.Tests.Infrastructure.Testing;` ergänzen. **Kein** `Flaky` — kein dokumentiertes Flake-Ereignis.
- **Geänderte Methoden:** `Dispose()` — `RemoveReparsePoints(_root)` unverändert beibehalten, dann `TempDirectoryCleanup.DeleteBestEffort(_root, normalizeAttributes: true)` statt Normalisierungs-Schleife + `Directory.Delete`.

### `ScmEventExchangeServiceTests` (Testklasse, `Softwareschmiede.Tests`)

- **Geänderte Methoden:** `Dispose()` (Z. 494–499) — Normalisierungs-Schleife + `Directory.Delete` → `TempDirectoryCleanup.DeleteBestEffort(_root, normalizeAttributes: true)`.
- **Kein Trait** (nur `git`-Spawns als Fixture-Plumbing, s. Scope-Ausschluss 2).

### `GitHubEventSourceTests` (Testklasse, `Softwareschmiede.Tests`)

- **Geänderte Attribute:** `CommentAttachments_ReparsePath_PostsSkippedNote` (Z. 637 ff.) — `[Fact]` → `[OsInterfaceFact]` (echter Junction-Spawn); `using Softwareschmiede.Tests.Infrastructure.Testing;` ergänzen. **Kein** `Flaky`.
- **Geänderte Methoden:** `finally`-Block der Methode (Z. 670–675) — beide `Directory.Delete`-Aufrufe → `TempDirectoryCleanup.DeleteBestEffort` (Junction-Pfad ohne Rekursion nötig; Kommentar „Junction selbst löschen" beibehalten).

### `ScmDispatchFixture` (Test-Fixture, `Softwareschmiede.Tests`, per Compile-Link im Integrationsprojekt)

- **Geänderte Methoden:** `DisposeAsync()` — bestehende Kill-Schleife über `_processes` unverändert; `if (_ownsRoot) Directory.Delete(Root, true);` (Z. 340) → `TempDirectoryCleanup.DeleteBestEffort(Root)`.

### `StartPs1IntegrationTests` (Testklasse, Integrationsprojekt)

- **Geänderte Methoden:** `Dispose()` (Z. 320–326) — je Verzeichnis `TempDirectoryCleanup.DeleteBestEffort(directory)` statt `Directory.Delete(directory, recursive: true)`; `using Softwareschmiede.TestSupport;` ergänzen.

### `TempDirectory` / `TestTempDirectoryFixture` (Hilfsklassen, `Softwareschmiede.Tests/Helpers/`)

- **Geänderte Methoden:** `Dispose()` — `Directory.Delete` → `TempDirectoryCleanup.DeleteBestEffort` (gleiche Fragilität, identische Härtung; für alle Konsumenten wirksam).

### `Softwareschmiede.IntegrationTests.csproj`

- **Neuer Eintrag:** `<Compile Include="..\Softwareschmiede.Tests\Helpers\TempDirectoryCleanup.cs" Link="Infrastructure\TempDirectoryCleanup.cs" />` in der bestehenden `ItemGroup` neben dem `ScmDispatchFixture`-Link.

### `scripts/Run-AllTestsIndividually.ps1`

- **Neue Logik:** Pro Projekt einmalig die Flaky-Menge ermitteln (`dotnet test --no-build --list-tests --filter "Category=Flaky"`, wiederverwendet die Parsing-Logik von `Get-TestTargets`).
- **Geänderte Hauptschleife:** Bei `Status = Fehlgeschlagen` eines Tests, dessen `FilterName` in der Flaky-Menge liegt (und der nicht bereits den Runtime-Error-Retry durchlaufen hat): genau ein Re-Run via `Invoke-SingleTest` ohne Rebuild; `Retried`-Flag bzw. neues `FlakyRetried`-Flag im Ergebnisobjekt; `summary.md` weist `(Flaky-Retry)` aus.
- **Doc-Kommentar (Synopsis):** Flaky-Retry-Verhalten ergänzen.

### `.github/workflows/pr-staging-ci.yml` und `.github/workflows/staging-ci.yml` (Job `os-interface tests`, in beiden Dateien identisch)

- **Neuer Schritt** `Retry flaky tests once` (`if: failure()`, `shell: pwsh`): je Testprojekt `scripts/Retry-FlakyTests.ps1 -TestProject <csproj> -PrimaryTrx <test-results-os-interface-*.trx>` aufrufen; Exit-Code aggregieren (persistente Flaky-Fehler → Schritt schlägt fehl, Job bleibt `continue-on-error` best-effort). `if: failure()` ist bewusst weit gefasst — bei Build-Fehlschlag/fehlendem TRX meldet das Skript „keine Flaky-Fehlschläge" und endet mit 0, sodass der eigentliche Fehlschritt führend bleibt.
- **Neuer Schritt** `Analyze flaky test results` (`if: always()`): `scripts/Analyze-FlakyTestResults.ps1 -Path <TestResults-Verzeichnisse beider Projekte>` — reiner Report, Exit 0.
- **Geänderte Artefakt-Pfade** im `Upload test results`-Schritt: `test-results-os-interface-tests.trx`/`…-integration.trx` → Glob `test-results-os-interface*.trx`, damit `*-flaky-retry.trx` mit hochgeladen wird.

### `docs/help/stabilitaet/os-interface-tests.md` (Dokumentation)

- **Neuer Abschnitt Klassifizierungskriterium:** Was zählt als OS-Schnittstelle (Assertion hängt am OS-Verhalten: eingeschränkte Tokens/SIDs, `CreateProcessAsUser`, ConPTY, Fenster/FlaUI, Clipboard, Reparse-Punkte/Junctions, lockanfällige Dateisystem-Szenarien) vs. triviales Fixture-Plumbing (`git`, `ping`-Platzhalter) — letzteres bleibt regulär.
- **Neuer Abschnitt `Flaky`-Kategorie:** Bedeutung, Vergaberegeln (nur mit `OsInterface`; nur bei dokumentiertem Flake-Ereignis oder Analyzer-Befund; Entfernen, sobald die Ursache behoben ist), Schreibweisen (`[Flaky]` in `Softwareschmiede.Tests`, `[Trait("Category", "Flaky")]` im Integrationsprojekt).
- **Neuer Abschnitt Automatischer Retry:** Ablauf des CI-Schritts (`Retry-FlakyTests.ps1`, `if: failure()`, einmaliger gefilterter Re-Run nur `Flaky`-Fehlschläge, recovered/persistent-Auswertung), lokale Varianten (`Run-AllTestsIndividually.ps1`-Erweiterung, manueller Skriptaufruf).
- **Neuer Abschnitt Flake-Statistik:** Analyzer-Aufruf lokal (inkl. `gh run download`-Beispiel für CI-Artefakte) und als CI-Report-Schritt; Schwellwerte und ihre Kalibrierbarkeit.
- **Geänderter Abschnitt CI-Verhalten:** Veraltete `test.yml`-Referenzen und Schrittnamen auf die tatsächlichen Jobnamen `build & test` / `os-interface tests` in `pr-staging-ci.yml`/`staging-ci.yml` und TRX-Artefaktnamen `test-results-os-interface-*` aktualisieren (Doc-Drift, in der Bestandsaufnahme vermerkt) — inkl. der neuen Schritte `Retry flaky tests once`/`Analyze flaky test results`.
- **Geänderter Abschnitt Env-Flags und Retry:** Bestehende Regel „kein Retry regulärer Tests" bleibt; um den `Flaky`-Einmal-Retry und die Job-Rerun-Konvention (`gh run rerun --job` nur für `os-interface tests` bei Job-/Infrastrukturfehlern) präzisiert.
- **Neuer Abschnitt Audit-Heuristik:** Wiederholbarer Suchbefehl (Heuristik `Process.Start`, `ScmRestrictedWindowsProcess`, `PseudoConsole`, `ConPty`, `FlaUI`, `Clipboard` über beide Testprojekte) plus Bewertungsregel „Assertion hängt am OS-Verhalten" — macht das Audit ohne neuen Code wiederholbar.
- Stil der Datei beibehalten (ASCII ohne Umlaute, wie bisher).

## Datenbankmigrationen

Keine.

## Validierungsregeln

||| Feld / Objekt | Regel | Fehlerfall |
|||---------------|-------|------------|
||| `Flaky`-Trait-Vergabe | Nur in Kombination mit `OsInterface` | Meta-Test `FlakyConventionTests` (Reflection über `Softwareschmiede.Tests`-Assembly) schlägt fehl; im Integrationsprojekt durch Doku-Konvention + Review |
||| `Retry-FlakyTests.ps1` Eingaben | Fehlendes/unauswertbares Primär-TRX → „keine Flaky-Fehlschläge", Exit 0 (kein Wurf, damit `if: failure()`-Aufruf bei Build-Fehlschlag nichts verschleiert) | — |
||| `Analyze-FlakyTestResults.ps1` Eingaben | Leeres/fehlendes Verzeichnis oder keine TRX → leerer Report, Exit 0 | — |

## Konfigurationsänderungen

Keine. Steuerung über bestehende Mechanismen: xUnit-Kategorie-Filter (`Category=OsInterface`/`!=OsInterface`, neu zusätzlich `Category=Flaky`), Env-Flags `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS`/`SOFTWARESCHMIEDE_SKIP_WPF_E2E_TESTS` (nur CI-Job). Die neuen Zeitbudgets `ChildPhaseBudgetSeconds`/`HostPhaseTimeoutSeconds` sowie die Analyzer-Schwellwerte sind Code-/Skript-Parameter, keine Nutzer-Settings.

## Seiteneffekte und Risiken

- **Coverage-Gate (Gate 5, ≥ 70 %):** Die 10 RestrictedProcess-Fälle und 2 Junction-Tests entfallen aus der coverage-liefernden regulären Lane; ihr bisheriger Coverage-Beitrag (Plugin-Startspecs, `ScmPromptSessionAdapter`, `ScmEventExchangeProtocol`, `ScmCliAccessEvaluator`-Pfade) geht verloren. Risiko klein, aber zu beobachten: fällt die Zeilenabdeckung unter 70 %, muss der Ausfall anderweitig kompensiert werden.
- **Längere Worst-Case-Laufzeit OS-naher Tests:** `probeDeadline`/`deadline`/`WaitAsync` von 15–30 s auf 120 s erhöht die Zeit bis zum Fehlschlag bei echten Deadlocks; verkraftbar im 30-min-Budget des `os-interface tests`-Jobs.
- **Retry verlängert den best-effort-Job im Fehlerfall:** Ein zusätzlicher `dotnet test`-Lauf (gefiltert, klein) plus `--list-tests`-Aufruf je Projekt — Größenordnung Sekunden bis wenige Minuten; im 30-min-Budget unkritisch.
- **Retry kann Regressionen kaschieren, falls `Flaky` missbräuchlich vergeben wird:** Mitigation über Vergaberegeln (nur mit `OsInterface`, nur bei dokumentiertem Ereignis), `FlakyConventionTests`, Review-Pflicht in der Doku und Analyzer-Sichtbarkeit (ein wiederholt „geretteter" Test wird als Quarantäne-Kandidat gemeldet statt still zu grünen).
- **Stiller Temp-Müll:** `DeleteBestEffort` schluckt Restfehler bewusst — dauerhafte Löschversagen hinterlassen Temp-Verzeichnisse; nur über die Diagnoseausgabe sichtbar. Gewollter Trade-off zugunsten ehrlicher Asserts.
- **xUnit-v3-Trait-Übernahme auf Methode:** `[Trait]` neben `[Theory]`/`[Fact]` muss die `Category` auf die Testfälle übernehmen — früh mit `--list-tests --filter "Category=OsInterface"` bzw. `"Category=Flaky"` verifizieren.
- **`Run-AllTestsIndividually.ps1`:** Die umgetaggten Tests wechseln automatisch vom `Regular`- ins `OsInterface`-TestSet; das Skript erhält zusätzlich die Flaky-Retry-Pfade — bestehende Rebuild+Retry-Logik bleibt unverändert und hat Vorrang.
- **Klassen-Konstruktoren mit `git init`:** `ScmEventExchangeServiceTests`/`ScmEventExchangeAttachmentTests` führen `Git("init")` im Ctor aus — auch für den getaggten Junction-Test; unproblematisch, aber dokumentiert, dass der Ctor-Spawn bewusst regulär bleibt.
- **CI-Verifikationsaufwand:** Issue verlangt mehrere komplette Actions-Durchläufe als Nachweis — verlängert die Gesamtdauer der Aufgabe (Fail-fast-Polling je Lauf, siehe globale Regeln).

## Umsetzungsreihenfolge

1. **`Flaky`-Trait-Infrastruktur anlegen**
   - Voraussetzungen: Keine.
   - Beschreibung: `TestCategories.Flaky`-Konstante und `FlakyAttribute` (`ITraitAttribute`) in `src/Softwareschmiede.Tests/Infrastructure/Testing/` nach dem `OsInterfaceAttribute`-Muster anlegen.
2. **`TempDirectoryCleanup`-Helfer anlegen + Compile-Link im Integrationsprojekt**
   - Voraussetzungen: Keine.
   - Beschreibung: Neue Datei `src/Softwareschmiede.Tests/Helpers/TempDirectoryCleanup.cs` (Namespace `Softwareschmiede.TestSupport`) mit `DeleteBestEffort`; `<Compile Include>`-Eintrag in `Softwareschmiede.IntegrationTests.csproj` nach dem `ScmDispatchFixture`-Muster.
3. **`ScmEventCliExchangeAccessTests` härten (Registry + Dispose + Deadlines + Tagging)**
   - Voraussetzungen: Schritte 1 und 2.
   - Beschreibung: `_restrictedChildren`-Feld, Prozess-Registrierung nach beiden `ScmRestrictedWindowsProcess.Start`-Aufrufen, `Dispose()` auf `DeleteBestEffort`, Konstanten `ChildPhaseBudgetSeconds`/`HostPhaseTimeoutSeconds`, Interpolation von `$phaseTimeout`, Angleichung `probeDeadline`/`deadline`/beide `WaitAsync`, `[Trait("Category", "OsInterface")]` + `[Trait("Category", "Flaky")]` auf den drei Methoden.
4. **Junction-Tests in `Softwareschmiede.Tests` taggen**
   - Voraussetzungen: Keine.
   - Beschreibung: `[OsInterfaceFact]` auf `ScmEventExchangeAttachmentTests.Collect_RejectsReparsePointInDeclaredPath` und `GitHubEventSourceTests.CommentAttachments_ReparsePath_PostsSkippedNote`; `using Softwareschmiede.Tests.Infrastructure.Testing;` ergänzen.
5. **Cleanup-Härtung auf die dokumentierten Schwester-Stellen anwenden**
   - Voraussetzungen: Schritt 2.
   - Beschreibung: `ScmEventExchangeServiceTests.Dispose`, `ScmEventExchangeAttachmentTests.Dispose`, `GitHubEventSourceTests`-`finally`, `ScmDispatchFixture.DisposeAsync`, `StartPs1IntegrationTests.Dispose`, `TempDirectory.Dispose`, `TestTempDirectoryFixture.Dispose` auf `TempDirectoryCleanup` umstellen.
6. **`Retry-FlakyTests.ps1` anlegen**
   - Voraussetzungen: Schritt 1 (Flaky-Filter muss Tests finden können).
   - Beschreibung: Skript mit TRX-Parsing (Muster `Get-TrxResult`), Flaky-Discovery per `--list-tests --filter "Category=Flaky"`, Schnittmenge, einmaliger gefilterter Re-Run, recovered/persistent-Auswertung mit GitHub-Annotationen.
7. **`Analyze-FlakyTestResults.ps1` anlegen**
   - Voraussetzungen: Keine.
   - Beschreibung: TRX-Verzeichnis-Scan, Lauf-Gruppierung inkl. `-flaky-retry`-Paarung, Flake-Ereignisse/Fehlraten, Schwellwert-Klassifikation und Empfehlungstext.
8. **CI-Workflows erweitern**
   - Voraussetzungen: Schritte 6 und 7.
   - Beschreibung: In `pr-staging-ci.yml` und `staging-ci.yml` im Job `os-interface tests`: Schritt `Retry flaky tests once` (`if: failure()`), Schritt `Analyze flaky test results` (`if: always()`), Artefakt-Pfad auf `test-results-os-interface*.trx` erweitern.
9. **`Run-AllTestsIndividually.ps1` erweitern**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Flaky-Menge pro Projekt einmalig ermitteln; bei `Fehlgeschlagen` eines Flaky-Tests genau ein Re-Run ohne Rebuild; `FlakyRetried`-Ausweisung in `summary.md`; Doc-Kommentar ergänzen.
10. **Dokumentation `os-interface-tests.md` aktualisieren**
    - Voraussetzungen: Keine.
    - Beschreibung: Klassifizierungskriterium, `Flaky`-Kategorie + Vergaberegeln, automatischer Retry, Flake-Statistik + Schwellwerte, CI-Jobnamen/TRX-Namen (Doc-Drift), komplementäre `gh run rerun --job`-Konvention, Audit-Heuristik.
11. **Neue Tests schreiben**
    - Voraussetzungen: Schritte 1–3.
    - Beschreibung: `TempDirectoryCleanupTests` (reguläre Lane, deterministisches Datei-I/O: Read-only-Löschung, kein Wurf bei gesperrtem/fehlendem Verzeichnis) und `FlakyConventionTests` (Reflection: jedes `Flaky`-Trait impliziert `OsInterface`-Trait in `Softwareschmiede.Tests`).
12. **Lokale Verifikation**
    - Voraussetzungen: Schritte 1–11.
    - Beschreibung: Voller Build beider Testprojekte; `dotnet format Softwareschmiede.slnx --verify-no-changes`; `--list-tests`-Nachweis je Projekt (die drei RestrictedProcess-Methoden unter `Category=OsInterface` **und** `Category=Flaky`, die zwei Junction-Tests nur unter `OsInterface`); beide Lanes je Projekt ausführen — regulär mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`, OS-Lane zusätzlich mit `SOFTWARESCHMIEDE_SKIP_WPF_E2E_TESTS=1` (wie CI-Job). Skript-Verifikation: `Retry-FlakyTests.ps1` gegen ein TRX mit bekanntem Fehlschlag (bzw. synthetisches TRX im Temp-Verzeichnis) — kein Flaky-Fehlschlag → Exit 0 ohne Lauf; `Analyze-FlakyTestResults.ps1` gegen vorhandene `test-results/`-TRX. `dotnet test` nur synchron/foreground, nie im Hintergrund (Projektregel: Build-Lock-Hooks).
13. **CI-Verifikation (verbindlich aus dem Issue)**
    - Voraussetzungen: Schritt 12, Push/PR.
    - Beschreibung: Mehrere komplette `PR CI for Staging`-Durchläufe eigenständig wiederholen — ein einzelner grüner Lauf gilt nicht als Nachweis. Pro Lauf fail-fast-Beobachtung (`gh pr checks --watch --fail-fast` oder Polling mit `fail`-Abbruch); `os-interface tests`-Ergebnis und TRX-Artefakte prüfen (umgetaggte Tests laufen dort; reguläre Lane bleibt frei davon); Sichtbarkeit der neuen Schritte `Retry flaky tests once`/`Analyze flaky test results` im Job-Log bestätigen (auch wenn ohne Flake-Ereignis nur „keine Flaky-Fehlschläge" erscheint).
14. **Repo-übergreifende Issues**
    - Voraussetzungen: Schritt 13 erfolgreich.
    - Beschreibung: `gh repo list <owner>` auswerten; gleichartige Issues in allen übrigen Repositories des Owners anlegen (gleiche Maßnahmenliste, je Projekt angepasst an ConPTY/FlaUI/Playwright-Gegebenheiten).

## Tests

### Neue Tests

||| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|||--------------------|------------|-------------------------------------|
||| `DeleteBestEffort_ExistingDirectory_DeletesIncludingReadOnlyFiles` | `TempDirectoryCleanupTests` (`Softwareschmiede.Tests`) | Verzeichnis mit Read-only-Datei wird mit `normalizeAttributes: true` vollständig entfernt |
||| `DeleteBestEffort_LockedDirectory_DoesNotThrow` | `TempDirectoryCleanupTests` | Exklusiv geöffnete Datei (`FileShare.None`) lässt alle Retries scheitern → Methode kehrt ohne Exception zurück (Kern-Nachweis: Cleanup kann keinen Assert überdecken) |
||| `DeleteBestEffort_MissingDirectory_DoesNotThrow` | `TempDirectoryCleanupTests` | Nicht vorhandener Pfad → kein Wurf |
||| `FlakyMarkedTests_AlwaysCarryOsInterface` | `FlakyConventionTests` (`Softwareschmiede.Tests`, reguläre Lane) | Reflection über die eigene Assembly: jede Methode/Klasse mit `Category=Flaky`-Trait liefert auch `Category=OsInterface` — schlägt fehl, sobald jemand `Flaky` in der regulären Lane vergibt |

### Betroffene bestehende Tests

||| Test / Testklasse | Grund der Anpassung |
|||-------------------|---------------------|
||| `RestrictedWindowsProcess_ReadsWritesAndCompletesChannel_ButCannotWriteAppData` (6 Fälle), `RestrictedWindowsProcess_WithoutDedicatedCapability_DeniesChannel` (3 Fälle), `RestrictingSid_RejectsAccountsAndBroadPrincipals` | Neue `Category=OsInterface`- + `Category=Flaky`-Traits (Lane-Wechsel, retry-berechtigt), angeglichene Deadlines, Prozess-Registrierung — Asserts unverändert |
||| `ScmEventExchangeAttachmentTests.Collect_RejectsReparsePointInDeclaredPath`, `GitHubEventSourceTests.CommentAttachments_ReparsePath_PostsSkippedNote` | `[OsInterfaceFact]` (Lane-Wechsel); Dispose/`finally` auf `TempDirectoryCleanup` |
||| `ScmEventExchangeServiceTests`, `ScmDispatchFixture`-Nutzer (25 Dateien), `StartPs1IntegrationTests`, alle `TempDirectory`/`TestTempDirectoryFixture`-Konsumenten | Nur geändertes Cleanup-Verhalten (Retry/Backoff, Fehler geschluckt statt geworfen) — keine Assert-/Signatur-Änderung |
||| Erwartete Lane-Zahlen (Verifikationsanker, keine Garantie): `Softwareschmiede.Tests` regulär 2692−2(+2 neue Meta-/Cleanup-Tests), OS-Lane 51+2; `Softwareschmiede.IntegrationTests` regulär 366−10, OS-Lane 9+10; `Category=Flaky` im Integrationsprojekt = 10 | Baseline aus `inventory/tests.md` |

### E2E-Tests (primärer Funktionsnachweis)

Keine neuen E2E-Tests erforderlich und keine bestehenden betroffen: Die Anforderung ändert ausschließlich Testinfrastruktur, CI-Kategorisierung und Ausführungsskripte — es gibt keinen neuen oder geänderten Benutzerfluss und keine über UI/Nutzeraktion erreichbare Funktion. Die getestete Produktlogik (`ScmPromptSessionAdapter`, `ScmEventExchangeProtocol`, `ScmRestrictedProcessAccess`) bleibt fachlich unverändert; der Nachweis liegt in den Lane-Filter-Ergebnissen (`--list-tests`/TRX), dem Retry-/Analyzer-Verhalten in der CI und den deterministischen neuen Tests. FlaUI-E2E würde keinen zusätzlichen Benutzerfluss abdecken.

## Akzeptanzszenarien

||| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|||---|----------|----------|------------------|----------|-------------------|----------|
||| 1 | Happy Path: Lane-Trennung (CI) | Push/PR → `PR CI for Staging` | Umgetaggte OS-nahe Tests tragen `Category=OsInterface`; `os-interface tests`-Job existiert | `build & test` führt `Category!=OsInterface` aus → `os-interface tests` führt `Category=OsInterface` aus | Reguläre Lane enthält die 12 OS-nahen Tests nicht mehr; sie erscheinen im `os-interface tests`-TRX (`test-results-os-interface-*`) und laufen dort grün oder ehrlich | CI-Lauf + `--list-tests`-Nachweis + TRX-Artefakte |
||| 2 | Flaky-Fehlschlag wird einmal automatisch wiederholt | `Flaky`-markierter Test (z. B. RestrictedProcess) schlägt im `os-interface tests`-Job umgebungsbedingt fehl | Test trägt `Category=OsInterface`+`Category=Flaky`; Primär-TRX enthält `Failed` | Schritt `Retry flaky tests once` (`if: failure()`) → `Retry-FlakyTests.ps1`: TRX-Parser → Flaky-Schnittmenge → einmaliger gefilterter Re-Run | Zweite TRX `*-flaky-retry.trx` im Artefakt; Job-Log zeigt „recovered" (`::notice::`) bei grünem Retry bzw. „persistent" (`::warning::`) bei anhaltendem Fehler; kein zweiter Retry, reguläre Lane unberührt | CI-Lauf mit Flake-Ereignis (nicht deterministisch erzwingbar) + Skript-Codeprüfung; lokal reproduzierbar gegen reales/synthetisches TRX |
||| 3 | Langsamer Kind-Start (Deadline) | RestrictedProcess-Test in OS-Lane | Kindprozess braucht > 30 s für JIT/Serializer-Cold-Start, bleibt aber unter 90 s Phasenbudget | Host pollt bis `HostPhaseTimeoutSeconds` (120 s) statt 30 s → Kind vollendet Probe-Phase | `access.Status == Supported` statt `Unknown`-Timeout; im Fehlerfall führt child phase/state/error die Diagnose | Mehrere CI-Läufe ohne `Unknown`-Timeout + Codeprüfung (nicht deterministisch erzwingbar) |
||| 4 | Cleanup-Lock überdeckt nicht (Dispose) | Testfehlschlag mit noch laufendem/lockendem Kindprozess | Kind in `_restrictedChildren` registriert; Dateilock aktiv | `Dispose` → `DeleteBestEffort`: Kill-Sicherheitsnetz → Retry/Backoff → Restfehler nur Diagnose | Testreport zeigt den eigentlichen Assert; kein `AggregateException` aus `Dispose` | `TempDirectoryCleanupTests.DeleteBestEffort_LockedDirectory_DoesNotThrow` + Code-Review |
||| 5 | Flake-Statistik meldet Quarantäne-Kandidaten | Mehrere CI-Läufe mit wiederholtem Fehlschlag→Retry-grün desselben Tests | Gesammelte TRX-Artefakte (`gh run download` oder lokale `test-results/`) | `Analyze-FlakyTestResults.ps1 -Path <dir>` wertet Läufe aus → Klassifikation | Report zeigt Test als `Quarantäne-Kandidat` (≥ 2 Flake-Ereignisse oder Fehlrate ≥ 20 %) mit Empfehlung „Quarantäne-Issue + Fehlerbild-Eintrag"; kein automatisches Issue | Lokaler Skriptlauf über gesammelten Artefakten + CI-Report-Schritt im Job-Log |
||| 6 | OS-Job-Infrastrukturfehler (Konvention) | `os-interface tests`-Job schlägt ohne auswertbares TRX fehl (Runner-Abbruch, Testhost-Crash) | Retry-Schritt findet keine TRX-Fehlschläge und endet neutral | Job-Rerun-Konvention: genau ein `gh run rerun --job <job-id>` nur für diesen Job; bei Hartnäckigkeit Quarantäne-Issue + Doku-Eintrag | Reguläre Lane nie wiederholt; dokumentierte, reproduzierbare Behandlung | Dokumentierter Abschnitt in `os-interface-tests.md` |
||| 7 | Regulärer Lane-Fehler bleibt Regression (Konvention, negativ) | `build & test` schlägt fehl | Beliebig | Kein Rerun, kein `gh run rerun --failed`, kein Flaky-Retry (kein Flaky in der regulären Lane, abgesichert durch `FlakyConventionTests`) — Analyse wie bisher | Signal der stabilen Lane unverwässert | Konvention + `FlakyConventionTests` + Review |
||| 8 | `Flaky`-Vergabe ohne `OsInterface` wird verhindert | Entwickler markiert regulären Test mit `Category=Flaky` (z. B. `[Flaky]` auf `[Fact]` in `Softwareschmiede.Tests`) | Neues `Flaky`-Trait ohne `OsInterface` | Regulärer Testlauf → `FlakyConventionTests.FlakyMarkedTests_AlwaysCarryOsInterface` schlägt fehl | Verstoß wird als blockierender Testfehler sichtbar | `FlakyConventionTests` in der regulären Lane |

## Offene Punkte

Keine — alle Punkte geklärt. Der einzige verbliebene Punkt (welche „übrigen Repositories des Owners" für die repo-übergreifenden Folge-Issues gemeint sind) wurde wie empfohlen entschieden: **nach erfolgreicher CI-Verifikation** `gh repo list <owner>` auswerten und pro Repo eine angepasste Maßnahmenliste ableiten (ConPTY → Windows-Job, FlaUI → Desktop-Runner, Playwright → `--retries`-Policy, `Flaky`-Trait+Retry analog zur CI-Variante dieses Repos). Verankert als Schritt 14 in der Umsetzungsreihenfolge; bewusst nachgelagert, weil die Issue-Verifikation („mehrere CI-Läufe") Voraussetzung ist.

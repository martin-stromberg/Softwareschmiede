# Übersetzte Anforderung: Flaky Tests — umgebungssensitive Tests ohne `OsInterface`-Trait und fragile Deadlines/Cleanup

## Fachliche Zusammenfassung

Die Testinfrastruktur wird gehärtet, damit die reguläre, blockierende Test-Lane (`Category!=OsInterface`) deterministisch bleibt. Konkret: (1) umgebungssensitive Tests in `src/Softwareschmiede.IntegrationTests/Services/ScmEventCliExchangeAccessTests.RestrictedProcess.cs`, die echte eingeschränkte Windows-Prozesse (`ScmRestrictedWindowsProcess` mit dedizierter SID, PowerShell-Kind, ACL-gesichertes Dateisystem) starten, werden mit dem `OsInterface`-Trait markiert und laufen damit im separaten `os-interface tests`-CI-Job; ein Audit aller Testdateien stellt sicher, dass keine weiteren OS-nahen Tests ungetaggt in der stabilen Lane laufen. (2) Der Deadline-Mismatch zwischen Host-Polling (30 s) und Kind-Budget (`$phaseTimeout = 90`) wird aufgelöst. (3) Das Fixture-`Dispose()` wird gegen `IOException` durch Kindprozess-Locks gehärtet (Kill vor Delete, Retry/Backoff, best-effort-Logging statt Werfen). (4) Eine Quarantäne-/Retry-Konvention für bekannt-flaky OS-nahe Tests wird festgelegt, ohne das Signal der stabilen Lane zu verwässern.

## Fachlicher Scope

- **Verarbeitungseinheit:** Einzelne xUnit-Testmethoden bzw. -Klassen in den Projekten `Softwareschmiede.Tests` und `Softwareschmiede.IntegrationTests`, klassifiziert nach „deterministisch (regulär)" vs. „umgebungssensitiv (OS-Schnittstelle)". Bewertet wird pro Test, ob er echte Betriebssystem-Schnittstellen berührt (Prozess-Spawn, ConPTY, Clipboard, FlaUI-Fenster, eingeschränkte Tokens/SIDs, lockanfällige Dateisystem-Szenarien).
- **Auslösende Ereignisse:** Systemereignis — fehlschlagender CI-Lauf (konkret: `ScmEventCliExchangeAccessTests.RestrictedWindowsProcess_ReadsWritesAndCompletesChannel_ButCannotWriteAppData(provider: "Devin", rename: False)` in PR #427, Timeout nach 30 s `probeDeadline` bei noch laufendem Kind, anschließend `IOException` im `Dispose()`, die den eigentlichen Fehler als `AggregateException` überlagerte). Kein Nutzer-Trigger; der Ablauf ist eine Umorganisation/Härtung der Testausführung.
- **Erwartete Wirkung:**
  - Die reguläre Lane (`dotnet test … --filter "Category!=OsInterface"`) enthält keine Tests mehr, die echte Prozesse/ConPTY/Clipboard/FlaUI benötigen — CI-Fehlschläge in dieser Lane sind wieder echte Regressionssignale.
  - Der `os-interface tests`-Job in `pr-staging-ci.yml` (bestehend, `continue-on-error`, 30-min-Timeout, Env-Flags `SOFTWARESCHMIEDE_SKIP_WPF_E2E_TESTS=1` / `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`) führt die umgetaggten Tests aus.
  - Ein langsam startender eingeschränkter Kindprozess (JIT/Serializer-Cold-Start auf CI-Runnern) führt nicht mehr zu `Unknown`-Timeout-Fehlschlägen, solange er noch innerhalb seines eigenen Phasen-Budgets arbeitet.
  - Ein Cleanup-Fehler überdeckt nie wieder den eigentlichen Assert — die vorhandene Diagnose-Ausgabe (child phase/state/error) bleibt die führende Fehlerinformation.
  - Die gewählte Retry-/Quarantäne-Konvention ist dokumentiert und wiederholbar anwendbar.
- **Umgang mit fehlenden Objekten:**
  - `OsInterface`-Attribute (`OsInterfaceAttribute`, `OsInterfaceFactAttribute`, `OsInterfaceTheoryAttribute`, `TestCategories.OsInterface`) existieren nur in `Softwareschmiede.Tests` (`src/Softwareschmiede.Tests/Infrastructure/Testing/`); `Softwareschmiede.IntegrationTests` hat **keine** Projektreferenz darauf. Pro Integrations-Testprojekt ist zu klären: Attributdateien per `<Compile Include="…" Link="…">` einbinden (Muster: `ScmDispatchFixture.cs` im csproj) oder `[Trait("Category", "OsInterface")]` direkt verwenden (Muster: `StartPs1IntegrationTests.cs`, Zeile 10) — wiederverwenden, nicht neu erfinden.
  - Ein `Flaky`-Trait existiert noch nicht — bei Wahl dieser Variante neu zu erzeugen (analog `OsInterfaceAttribute`), sonst dokumentierte Konvention ohne neuen Code.
- **Scope-Ausschlüsse:**
  - Kein Blanket-Retry der Gesamtsuite und kein Retry zur „Stabilisierung" regulärer Tests — echte Regressionen dürfen nicht maskiert werden (bestehende Regel aus `docs/help/stabilitaet/os-interface-tests.md` bleibt).
  - Keine inhaltliche Änderung der getesteten Produktlogik (`ScmPromptSessionAdapter`, `ScmEventExchangeProtocol`, `ScmRestrictedProcessAccess` u. a. bleiben fachlich unverändert); nur Test-Rahmen, Zeitbudgets und Kategorisierung.
  - Keine vollständige Deterministik echter UI-/Prozess-Tests — Zielbild ist „selten, lokalisiert, selbstdiagnostizierend".
  - Der automatische Skip-Mechanismus für ConPTY bleibt das manuelle Env-Flag `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS` (Auto-Detection wurde bereits bewusst verworfen, wird nicht erneut eingeführt).
  - Die Flake-Statistik aus TRX-Artefakten (Fehlschlag-then-Rerun-grün-Rate, Schwellwert → Quarantäne-Issue) ist im Issue ausdrücklich als optional markiert — kein Pflichtbestandteil.

## Betroffene Klassen und Komponenten

- **Testdateien (zu taggen/zu härten):**
  - `src/Softwareschmiede.IntegrationTests/Services/ScmEventCliExchangeAccessTests.RestrictedProcess.cs` — Methoden `RestrictedWindowsProcess_ReadsWritesAndCompletesChannel_ButCannotWriteAppData` (Theory, 6 Fälle), `RestrictedWindowsProcess_WithoutDedicatedCapability_DeniesChannel` (Theory, 3 Fälle), `RestrictingSid_RejectsAccountsAndBroadPrincipals` (Fact, nutzt `WindowsIdentity.GetCurrent()`); Host-Deadlines `probeDeadline` (≈ Zeile 137) und `deadline` (≈ Zeile 154), jeweils 30 s.
  - `src/Softwareschmiede.IntegrationTests/Services/ScmEventCliExchangeAccessTests.cs` — `Dispose()` (Zeile 329): nacktes `Directory.Delete(_root, recursive: true)`; Fixture-Feld `_root`.
  - `src/Softwareschmiede.IntegrationTests/Services/ScmEventCliExchangeAccessTests.PendingProbe.cs` — partial-Klasse, deterministische Mock-Tests (betroffen nur, falls das Trait auf Klassenebene gesetzt wird — siehe Offene Fragen).
  - `src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests_Startpruefung.cs` — Audit-Kandidat (enthält `Process.Start`-Verwendung).
  - `src/Softwareschmiede.IntegrationTests/Services/ScmRestrictedWindowsProcess.cs` — Test-Helfer, startet die echten eingeschränkten Prozesse (Referenzpunkt des Audits, ggf. selbst kein Test).
- **Audit-Zielmenge (Heuristik aus dem Issue):** alle Testdateien in `Softwareschmiede.Tests` und `Softwareschmiede.IntegrationTests` mit `Process.Start`, `ScmRestrictedWindowsProcess`, `PseudoConsole`, `ConPty`, `FlaUI`, `Clipboard` ohne `OsInterface`-Markierung. Erste Trefferliste (zu verifizieren, nicht abschließend): u. a. `TerminalControlTests.cs`, `CliProcessManagerTests_LaufStatus.cs`, `E2E_*`-Dateien, `AppStartupLogInspector.cs`, `ScmE2EFixture.cs` — viele davon nur indirekt OS-nah (Helper/Fixtures), Fehl-Positive sind auszusortieren.
- **Bestehende Infrastruktur (wiederverwenden):**
  - `src/Softwareschmiede.Tests/Infrastructure/Testing/OsInterfaceAttribute.cs`, `OsInterfaceFactAttribute.cs`, `OsInterfaceTheoryAttribute.cs`, `TestCategories.cs` (`TestCategories.OsInterface = "OsInterface"`).
  - `.github/workflows/pr-staging-ci.yml` — getrennte Jobs `build & test` (`Category!=OsInterface`, blockierend) und `os-interface tests` (`Category=OsInterface`, `continue-on-error`, TRX-Upload `test-results-os-interface-*`).
  - `scripts/Run-AllTestsIndividually.ps1 -TestSet OsInterface` (lokale Lane).
  - `docs/help/stabilitaet/os-interface-tests.md` — bestehende Lane-/Retry-Konvention, hier ist die neue Quarantäne-Regel zu dokumentieren.
- **Neu (je nach gewählter Variante):**
  - `Flaky`-Trait-Attribut + `TestCategories.Flaky` (nur bei Wahl der Retry-Variante) sowie ein Retry-Ausführungsmechanismus mit genau einem Wiederholungslauf nur für markierte Tests, oder
  - dokumentierte `gh run rerun --failed`-Konvention beschränkt auf den `os-interface tests`-Job.
- **Tests:** Die Umsetzung ist selbst Testinfrastruktur; „Tests" heißt hier: angepasste Testmethoden/Traits plus ggf. ein neuer Meta-Test oder Skript-Check, der das Audit wiederholbar macht (Annahme — im Issue nicht explizit gefordert, als Option zu bewerten).
- **CI/Workflow:** `.github/workflows/pr-staging-ci.yml` nur falls die Retry-Konvention Job-Anpassungen erfordert; sonst keine Änderung nötig (Lane existiert).

## Implementierungsansatz

1. **Tagging:** Die drei echten Prozess-/Identitäts-Tests in `…RestrictedProcess.cs` mit `OsInterface` markieren — entweder methodenweise (`[Trait("Category", "OsInterface")]` bzw. verlinktes `OsInterfaceTheory`/`OsInterfaceFact`) oder auf Klassenebene. Da `ScmEventCliExchangeAccessTests` eine `partial`-Klasse mit vielen deterministischen Mock-Tests ist, ist methodenweises Tagging die naheliegende Annahme, um die reguläre Lane-Abdeckung nicht zu verlieren (zu bestätigen, siehe Offene Fragen). Für das Trait im Integrationsprojekt das `ScmDispatchFixture`-Link-Muster oder das direkte `[Trait]`-Muster aus `StartPs1IntegrationTests.cs` verwenden.
2. **Audit:** Heuristische Suche (`Process.Start`, `ScmRestrictedWindowsProcess`, `PseudoConsole`, `ConPty`, `FlaUI`, `Clipboard`) über beide Testprojekte; jeden Treffer fachlich prüfen (echter OS-Kontakt vs. Helper/Indirektion) und fehlende Tags ergänzen.
3. **Deadline-Angleichung:** Die beiden Host-Pollfenster (`probeDeadline`, `deadline`, jeweils `AddSeconds(30)`) auf mindestens das Kind-Phasenbudget angleichen (≥ ~90 s, konsistent zu `$phaseTimeout = 90` im PowerShell-Kind-Skript). Die bestehende Diagnose-Ausgabe (child phase/state/error) unverändert erhalten.
4. **Cleanup-Härtung:** In `Dispose()` bzw. Test-`finally`: sicherstellen, dass der Kindprozessbaum beendet ist (`Kill(entireProcessTree: true)` ist vorhanden, muss aber vor dem Delete garantiert wirken — ggf. gestartete Prozesse auf Fixture-Ebene registrieren), `Directory.Delete` mit Retry/Backoff ausführen und Restfehler nur protokollieren statt werfen, damit kein `AggregateException`-Overlay entsteht.
5. **Retry-/Quarantäne-Konvention:** Eine der beiden Issue-Varianten festlegen und in `docs/help/stabilitaet/os-interface-tests.md` dokumentieren; kein generelles Retry.
6. **Verifikation (verbindlich aus dem Issue):** Mehrere komplette GitHub-Actions-Durchläufe selbstständig wiederholen; ein einzelner grüner Lauf gilt nicht als Nachweis. Bei Erfolg gleichartige Issues (angepasste Maßnahmenliste) in allen übrigen Repositories des Owners anlegen.

## Konfiguration

Keine Anwendungs-/Laufzeitkonfiguration erforderlich. Verhalten wird über bestehende Mechanismen gesteuert: xUnit-Kategorie-Filter (`Category=OsInterface`/`!=OsInterface`), Env-Flags `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS` / `SOFTWARESCHMIEDE_SKIP_WPF_E2E_TESTS` (nur im CI-Job gesetzt), ggf. neues `Flaky`-Trait als Markierung (kein Nutzer-Setting). Zeitbudgets (`$phaseTimeout`, Host-Deadlines) werden als konsistente Konstanten im Testcode geführt — ein gemeinsamer benannter Wert für Host- und Kind-Budget ist zweckmäßig (Annahme).

## Offene Fragen

1. **Tagging-Granularität:** Methodenweises `OsInterface`-Tagging nur für die echten Prozess-Tests (erhält reguläre Lane-Abdeckung der deterministischen Teilklasse) oder Klassen-Trait auf `ScmEventCliExchangeAccessTests` (einfacher, schiebt aber auch Mock-basierte Tests in die best-effort-Lane)? Empfehlung: methodenweise.
2. **Trait-Mechanismus im Integrationsprojekt:** `<Compile Include>`-Link der Attribute aus `Softwareschmiede.Tests` (konsistente `OsInterfaceTheory`-Schreibweise) vs. direktes `[Trait("Category", "OsInterface")]` (bestehendes Muster in `StartPs1IntegrationTests.cs`)? Empfehlung: dem vorhandenen `[Trait]`-Muster folgen oder die Attributdateien verlinken — zu entscheiden.
3. **Retry-Variante:** `Flaky`-Trait mit genau einem kontrollierten Retry (benötigt neuen Mechanismus im Test-Skript/CI) oder dokumentierte `gh run rerun --failed`-Konvention nur für den `os-interface tests`-Job (kein Code)? Issue lässt beides zu.
4. **Tests mit `git init` als einzigem OS-Kontakt:** Zählt das reale `git`-Spawn in `GitInit` (hilfsweise `Process.Start`) als OS-Schnittstelle im Sinne des Audits, oder gilt die Einstufung nur für eingeschränkte/ConPTY-/UI-Prozesse? Empfehlung: `git init` nicht als Kriterium, da historisch stabil — zu bestätigen.
5. **Optionale Flake-Statistik:** Soll die TRX-basierte Statistik in dieser Aufgabe umgesetzt oder als Folge-Issue vertagt werden?
6. **Repo-übergreifende Issues:** Welche konkreten Repositories des Owners sind gemeint und wie sieht die angepasste Maßnahmenliste je Projekt aus (ConPTY/FlaUI/Playwright)? Wird erst nach erfolgreicher Verifikation im eigenen Repo fällig.

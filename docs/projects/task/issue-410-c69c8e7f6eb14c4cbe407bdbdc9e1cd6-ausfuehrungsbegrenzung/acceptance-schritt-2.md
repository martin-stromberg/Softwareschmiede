# Abnahmeprüfung – Entwicklungsschritt 2

Geprüft: Ausführungsbegrenzung mit Priorisierung (#410, Teil 1), **Runde 2 nach Korrektur**, Branch
`task/issue-410-c69c8e7f6eb14c4cbe407bdbdc9e1cd6-ausfuehrungsbegrenzung-schritt-2-ausfuehrungslimits`
(Commits `999220b0` plan, `75be5e22` feat, `ec022835` fix), Diff-Basis
`task/issue-410-c69c8e7f6eb14c4cbe407bdbdc9e1cd6-ausfuehrungsbegrenzung`.
Runde 1 (`acceptance-schritt-2.1.md`) fand alle Akzeptanzkriterien erfüllt plus eine Abweichung:
nicht-atomare Zulassungskontrolle (check-then-act) zwischen Limit-Prüfung und Prozess-Handle-Registrierung.

## Ergebnis

**Status:** Anforderung vollständig erfüllt

## Abweichungen

- [x] **Zulassungskontrolle ist nicht atomar — nebenläufige Dispatches verschiedener Aufgaben können das Limit transient überschreiten.** *(Aus Runde 1 übernommen; durch `ec022835` geschlossen — Nachweis unten.)*

## Hinweise

### Nachweis der Abweichungsschließung

**Atomare Zulassung:** `ScmEventExecutionLimitService.PruefeUndReserviereStartPlatzAsync`
(`src/Softwareschmiede/Application/Services/ScmEventExecutionLimitService.cs:85-110`) liest die Grenzen,
betritt das prozessweite `ZulassungsGate` (`:64`, static `SemaphoreSlim(1,1)`) und wertet
`WirksameBelegungAsync` (`:178-199`) — Handle-Belegung plus offene Reservierungen — erst **innerhalb**
der Sperre aus; die Momentaufnahme kann damit zwischen Prüfung und Platzbelegung nicht veralten.
Der Claim (Eintrag in die static `Reservierungen`-Map, `:66,102-103`) erfolgt unter derselben Sperre
und zählt ab sofort für jeden weiteren Zulassungsversuch inkl. der reinen Prüfung
(`GetStartBlockReasonAsync`, `:123-137`, selbes Gate, selbe Belegungsquelle — konsistent gehalten).

**Reservierungs-Scope im Orchestrator:** `DispatchCoreAsync` hält die Reservierung per
`using (zulassung.Reservierung)` (`src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs:198-208`)
über Plugin-Auflösung (`:200`), Health-Check (`:201`) und Prozessstart (`:203-207`) — exakt das in
Runde 1 benannte Überschreitungsfenster. Exit-Pfade lückenlos: Wartegrund-Pfad (`:191-197`) erhält per
Konstruktion nie eine Reservierung (`Wartegrund` setzen ⇒ `Reservierung` = `null`, `:98-99`); Exception
innerhalb des `using` (Health-Check-Fehlschlag → `InvalidOperationException`, Provider-/IO-Fehler,
OCE) disposed vor dem Unwind in die Catches `:276-289` bzw. den `finally`-Block `:291-297`; Erfolg
disposed am Blockende — die Handle-Registrierung (`KiAusfuehrungsService.cs:226` bzw. `:375` im
Terminal-Session-Pfad, beide innerhalb der aufgerufenen `CliNeustartenAsync`/`ProzessStartenUndCliStartenAsync`)
liegt zeitlich **vor** der Freigabe, die Übergabe an die echte Laufzählung ist also lückenlos.
Kein Pfad kann eine Reservierung dauerhaft gezählt lassen: vergessene Freigabe (hängender Start)
verfällt nach `ReservierungsLebensdauer` = 5 min (`:57`), Verfall-Prüfung und Entfernung laufen vor
jeder Zählung in `WirksameBelegungAsync` (`:182-188`) über den injizierten `TimeProvider`.

**Restfenster-Analyse:** Ein Start kann das Limit nur noch überschreiten, wenn das Startfenster die
5-min-Reservierungsdauer überschreitet (z. B. extremer Klon in `ProzessStartenUndCliStartenAsync` oder
hängender Health-Check): dann verfällt der Platz, ein zweiter Dispatch wird zugelassen, und der
verspätet fertige erste Start registriert ein zusätzliches Handle. Das ist eine bewusste, dokumentierte
Restreichweite (Commit-Message, `review-code.md`, `business-rules.md`) — die Alternative (kein Verfall)
würde bei jedem hängenden Start den Platz permanent verlieren. Das in Runde 1 beanstandete reale
Szenario (nebeneinanderlaufende Dispatches im Sekunden-Fenster zwischen Prüfung und Handle) ist damit
geschlossen.

### Substanz der Korrektur-Tests

- `PruefeUndReserviereStartPlatzAsync_ParalleleVersuche_NurEineReservierung`
  (`src/Softwareschmiede.Tests/Application/Services/ScmEventExecutionLimitServiceTests.cs:284-313`):
  8 parallele Aufrufe auf **8 getrennten Service-Instanzen mit eigenen DbContexts** (simuliert eigene
  DI-Scopes), globales Limit 1 → exakt eine Reservierung, alle übrigen erhalten den Limit-Wartegrund.
  Beweist die prozessweite Wirkung des static Gate über Instanzgrenzen hinweg — genau der Runde-1-Befund.
- `..._Reservierung_BelegtGlobalenPlatz` / `..._BelegtNurEigenesPluginLimit` (`:245-278`): Reservierung
  zählt sofort global bzw. nur fürs eigene Plugin-Limit, Freigabe stellt den Platz wieder her.
- `..._Reservierung_VerfaelltNachLebensdauer` (`:318-333`): `FakeTimeProvider` treibt den Verfall
  deterministisch; danach ist der Platz ohne Dispose wieder nutzbar.
- `Dispatch_ConcurrentStartDuringHealthCheck_GlobalLimitOne_ExactlyOneStarts`
  (`src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests_Limits.cs:263-301`):
  deterministischer Race über `TaskCompletionSource` am `CheckHealthAsync`-Mock — der erste Dispatch
  hält die Reservierung im echten Startfenster (kein Handle registriert), der zweite Dispatch einer
  **anderen Aufgabe** wartet mit Limit-Grund statt zu starten (`Assert.Empty(fixture.Sessions)` belegt,
  dass kein zweiter Prozessstart versucht wurde). Ohne die Korrektur liefe der zweite Dispatch in
  denselben Health-Check und das Limit würde überschritten.
- `Dispatch_StartFails_ReservationReleasedForNextDispatch` (`:306-326`): Health-Check-Fehlschlag →
  `Wartend` mit ehrlichem Grund; der nächste Dispatch belegt den freigegebenen Platz sofort —
  belegt die Freigabe im Fehlerpfad.

### Neue-Probleme-Prüfung (Korrektur-spezifisch)

- **Deadlocks:** Sperrordnung ist strikt einseitig: `ScmTaskCoordinator`-Aufgabensperre →
  `ZulassungsGate` → `ReservierungenSync`. `ReservierungenSync` wird niemals über einen `await`
  hinweg gehalten (`:182-188` synchroner Block; DB-Abfrage vor dem Lock); `Dispose` (`:236-240`)
  wartet nie auf `ZulassungsGate` (non-blocking by design). `ZulassungsGate` selbst wird nur über
  die kurze Belegungsabfrage (eine EF-Query + synchrone Handle-Reads) gehalten — `SemaphoreSlim`
  ist async-fähig, kein Thread-Blocking; verschachtelte Aufgabensperren sind über `AsyncLocal`
  reentrant (`ScmTaskCoordinator.cs:9,17`), und das globale Gate ist zum Zeitpunkt der
  Start-Unteraufrufe (`CliNeustartenAsync` → erneut `ScmTaskCoordinator.RunAsync`) bereits freigegeben.
- **Static Gate vs. scoped Service:** bewusste und notwendige Entscheidung — Reservierungen müssen
  über alle DI-Scopes sichtbar sein; `TimeProvider` ist pro Instanz injiziert (Prod:
  `TimeProvider.System`, `App.xaml.cs:305`; Fixture: `ScmDispatchFixture.cs:155`).
- **UI-Thread:** kein synchrones Warten; `Dispose` hält `ReservierungenSync` nur für ein
  Dictionary-Remove — auf dem Dispatcher-Kontext unkritisch.
- **Shutdown:** Reservierungen sind rein prozessinterner Static-State; bei App-Ende verfallen sie
  mit dem Prozess — kein Persistenz-Leak, kein Folgezustand.
- **Test-Isolation des Static-State:** Unit-Assembly läuft mit `DisableTestParallelization`
  (`src/Softwareschmiede.Tests/AssemblyInfo.cs`), `ReservierungenZuruecksetzen` in Ctor+Dispose.
  In der Integration-Assembly konfiguriert ausschließlich `ScmEventDispatchTests_Limits` Limits;
  alle übrigen Dispatches verlassen `PruefeUndReserviereStartPlatzAsync` bereits bei
  `GetLimitsAsync` (`:87-89`) und berühren weder Gate noch Reservierungsbestand — kein
  Cross-Class-Einfluss trotz paralleler Testklassen.

### Randbeobachtungen (keine Abweichungen)

- **Konservatives Doppelzählen im Übergabefenster:** Zwischen Handle-Registrierung und
  `using`-Ende zählen Reservierung und Handle denselben Platz doppelt — eine nebenläufige Prüfung
  in diesem Millisekunden-Fenster sieht einen Platz zu viel und wartet einen Poll-Zyklus zu früh.
  Wirkt nur richtung Sicherheit (unter- statt Überzulassung), selbstkorrigierend.
- **Test-Robustheit:** In `Dispatch_ConcurrentStartDuringHealthCheck_...` hat der `await
  fixture.DispatchAsync(secondId)` (`:288`) keinen eigenen Timeout — bei einer Regression würde der
  zweite Dispatch im blockierten Health-Check hängen und erst das Framework-Timeout entscheiden.
  Das Prüfsignal bleibt korrekt (Timeout = Fail), nur langsamer.
- **`GetStartBlockReasonAsync` ohne Produktionsaufrufer:** die reine Prüfung wird nur noch von
  Tests genutzt (Produktionspfad: ausschließlich `PruefeUndReserviereStartPlatzAsync`,
  `ScmEventOrchestrator.cs:190`); Semantik ist deckungsgleich gehalten (Gate + Reservierungen).
- **Bewusst unlimitierte Pfade unverändert:** Starts ohne Zulassungskontrolle (manueller CLI-Start
  im Aufgabenfenster, zeitgesteuerte Prompts) unterliegen dem Limit weiterhin nicht — dokumentierte
  Entscheidung (`ScmEventExecutionLimitService.cs:93-96`, `review-code.md`), bereits Runde-1-Scope.
- **Limit-Lesen außerhalb des Gate:** `GetLimitsAsync` (`:87`) läuft vor `ZulassungsGate.WaitAsync` —
  korrekt, da die Grenzen Konfiguration sind; ein zeitgleich geändertes Limit gilt ab dem nächsten
  Versuch (TOCTOU auf Config-Ebene, wie zuvor).

### Stichprobe Gesamtanforderung (Regression gegen Runde-1-AK)

Der Fix-Diff (`75be5e22..ec022835`) berührt außer Doku/Tests ausschließlich
`ScmEventExecutionLimitService.cs` und den Zulassungs-Callsite in `ScmEventOrchestrator.cs` —
Priorisierung (`ScmDispatchPrioritaet`), Settings-Persistenz/-UI, `BlockReason`, Polling-Batch und
Hostaktionen-/Reconcile-Pfade sind unverändert. Stichproben: AK 2 — Wartegrund-Pfad mit
dedupliziertem `SystemMeldung`-Eintrag identisch (`:191-197`); AK 5 — Zulassung steht weiterhin
ausschließlich im Neu-Start-Zweig (`adapter is null || !adapter.IsAlive`, `:162`), Hostaktionen
laufen vor der Prüfung, Reconcile daran vorbei; AK 1 — `ParseLimit`/Schlüssel unverändert;
AK 3/4 — keinerlei Diff-Berührung.

### Eigenverifikation

Zeilenprüfung von `ScmEventExecutionLimitService.cs` (komplett) und `ScmEventOrchestrator.cs`
(`DispatchCoreAsync` inkl. aller Exit-Pfade, `MutateAndDispatchAsync`, `OpenFromQueueAsync`),
`KiAusfuehrungsService.cs:135-143,185-255,284-418` (Handle-Registrierung/-Entfernung),
`EntwicklungsprozessService.cs:126-307` (Start-Unteraufrufe inkl. Reentrant-Sperre),
`ScmTaskCoordinator.cs` (AsyncLocal-Reentranz), `ScmEventMonitoringService.cs:229-258`,
`ScmEventExchangeService.cs:31`, Fixture `ScmDispatchFixture.cs` und sämtlicher neuer/geänderter
Tests. Teil-Build verifiziert: `dotnet build src/Softwareschmiede/Softwareschmiede.csproj` →
0 Warnungen, 0 Fehler. Dokumentierte Läufe nach dem Fix (`test-results.md`): reguläre Lane
2644/2647 (3 Skips), Integration 324/324, `dotnet format` sauber; die Voll-Lane wurde nicht
eigenständig wiederholt (~20 min).

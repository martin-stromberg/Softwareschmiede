# Umsetzungsplan: Shutdown bei Abschluss

## Übersicht

Die Seitenleisten-Fußzeile des `MainWindow` zeigt die Anzahl fachlich „laufender" Automatisierungen (Klassifikation `KiAusfuehrungsZustand.Laeuft`) an; bei aktiven Läufen kann der Anwender einen automatischen OS-Shutdown mit Toleranzzeit-Countdown und „Herunterfahren erzwingen"-Schalter aktivieren. Der vorhandene, aber todgewaltete `AutoShutdownOrchestrator` wird um den Countdown, Abbruch-Neuauswertung bei Statuswechseln und Force-Weitergabe erweitert und samt `SystemShutdownService` per DI verdrahtet; Force-Flag und Toleranzzeit werden über `AppEinstellungService` persistiert. Ein aufzeichnender `ISystemShutdownService`-Fake im Testmodus verhindert, dass Unit- und E2E-Tests jemals ein echtes OS-Shutdown auslösen.

## Scope-Klärung

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Die Menge der aktiven Aufgaben (`IAktiveAufgabenService.GetAktiveAufgabenAsync` — Status `Gestartet`/`Wartend` plus Lauf-Belege), klassifiziert über `KiAusfuehrungsStatusKlassifikation`; „laufend" = ausschließlich Zustand `KiAusfuehrungsZustand.Laeuft` (Live-Zweig: laufender gehandelter Prozess ohne `WartetAufEingabe`; Persistenz-Zweig: aktiver Lauf-Beleg mit frischem Heartbeat und `LaufStatus != WartetAufEingabe`). `Wartet`, `Pausiert`, `PromptInWartestellung`, `Bereit` zählen nicht. |
| Auslösende Ereignisse | `IRunningAutomationStatusSource.RunningCountChanged` (Prozess-Start/Ende), `AufgabeLaufdatenChangedNotifier.LaufdatenChanged` (Substatus-/Laufdaten-Wechsel inkl. `Laeuft`↔`WartetAufEingabe`), periodischer Sicherheitstick des Orchestrators (zeitbasierte Übergänge ohne Event: Pausenende, Heartbeat-Timeout); Nutzeraktionen: Aktivierungs-Checkbox, Toleranz-Eingabe, Force-Checkbox; Countdown-Ende → `ISystemShutdownService.RequestShutdownAsync`. |
| Erwartete Wirkung | Fußzeile zeigt laufende Anzahl (immer, bei ausgeklappter Navigation); Checkbox nur bei Anzahl > 0, aktiviertem Zustand oder laufendem Countdown; bei aktivierter Checkbox Toleranz-Eingabefeld + Force-Checkbox; während des Countdowns sichtbarer Restzeit-Text; bei Countdown-Ende OS-Shutdown (Windows `shutdown /s [/f] /t 0`, Linux/macOS unverändert); Force + Toleranz beim nächsten Start vorausgewählt. |
| Umgang mit fehlenden Objekten | *Keine persistierten Werte (Erststart):* Defaults — Force `false`, Toleranz `5` Minuten; `AppEinstellung`-Datensätze werden erst beim ersten Speichern angelegt (Key-Value-Entität, keine Migration). *Aktivierungszustand:* pro Sitzung neu, nicht persistiert (verwerfen). *Countdown bei Statuswechsel:* verworfen und per Neuauswertung ggf. neu armiert (kein Fortsetzen eines veralteten Restwerts). *Countdown bei App-Ende vor Ablauf:* verworfen, kein Shutdown (Anforderung). |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Die Aktivierungs-Checkbox (Auto-Shutdown ein/aus) wird **nicht** persistiert — nur Force-Checkbox und Toleranzzeit werden vorausgewählt. | Anforderung nennt Persistenz explizit nur für die zweite Checkbox und die Toleranzzeit; der Aktivierungszustand ist bewusst pro Sitzung neu zu setzen (Sicherheitsaspekt: kein „vergessenes" Shutdown-Arming nach Neustart). | bestätigt (2026-11-13; vorgelegt per Issue-Kommentar, Default im automatisierten Lauf) |
| 2 | Der Countdown startet auch, wenn noch physisch laufende CLI-Prozesse im Zustand `WartetAufEingabe` existieren (fachliche Zählung = 0). Diese wartenden Sessions verhindern den Shutdown nicht und werden beim OS-Shutdown mit beendet (bei Force zwangsweise). | „Laufend" ist in der Anforderung über die Klassifikation definiert (`Wartet` ≠ laufend); würde ein physisch wartender Prozess den Start blockieren, könnte eine dauerhaft auf Eingabe wartende Session den Shutdown unbegrenzt verhindern — die Funktion wäre in ihrem Haupteinsatzfall wirkungslos. | bestätigt (2026-11-13; vorgelegt per Issue-Kommentar, Default im automatisierten Lauf) |
| 3 | Eine pausierte Aufgabe mit noch laufendem CLI-Prozess zählt nicht als laufend (Klassifikation `Pausiert` überlagert `Laeuft`) — der Countdown kann trotz noch laufendem Prozess starten. | Die Anforderung verankert „laufend" wörtlich an der Klassifikation; Pause-Ende wird durch den Sicherheitstick erkannt und bricht den Countdown dann ab. Nutzer könnten erwarten, dass ein physisch laufender Prozess stets blockiert. | bestätigt (2026-11-13; vorgelegt per Issue-Kommentar, Default im automatisierten Lauf) |
| 4 | Kein Abbruch-Dialog/Nachfrage unmittelbar vor dem OS-Shutdown und kein `shutdown /a`-Abbrechen nach Auslösung. | In der Anforderung explizit ausgeschlossen; die Toleranzzeit ist die vorgesehene Abbruchmöglichkeit. | bestätigt (2026-11-13; vorgelegt per Issue-Kommentar, Default im automatisierten Lauf) |
| 5 | Keine Behandlung anderer laufender App-Aktivitäten (gerade laufendes Programmupdate, geöffnete Dialoge) und kein geordnetes App-Beenden vor dem OS-Shutdown — nur ein Warn-Logeintrag. | In der Anforderung nicht gefordert; das OS signalisiert Anwendungen den Shutdown ohnehin selbst. | bestätigt (2026-11-13; vorgelegt per Issue-Kommentar, Default im automatisierten Lauf) |
| 6 | Das Force-Flag wirkt nur unter Windows (`/f`); unter Linux/macOS bleiben die bestehenden Kommandos unverändert (kein direktes Force-Äquivalent). | Es existiert kein plattformübergreifendes Force-Konzept; das Ändern der Unix-Kommandos auf härtere Varianten (z. B. `poweroff -f`) wäre ein riskanteres, nicht gefordertes Verhalten. | bestätigt (2026-11-13; vorgelegt per Issue-Kommentar, Default im automatisierten Lauf) |
| 7 | Bei eingeklappter Navigation (48 px) wird der komplette Auto-Shutdown-Block ausgeblendet — analog den übrigen Fußzeilentexten. | Etabliertes Muster der Fußzeile (`IsNavigationExpanded` + `BoolToVisibilityConverter`); in 48 px Breite sind Texte und Checkboxen ohnehin nicht sinnvoll darstellbar. | bestätigt (2026-11-13; vorgelegt per Issue-Kommentar, Default im automatisierten Lauf) |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Fachliche „läuft"-Zählung | `KiAusfuehrungsZustand` und `KiAusfuehrungsStatusKlassifikation` werden aus `AppConverters.cs` (App-Projekt, `internal`) in das `Softwareschmiede`-Assembly verschoben (Enum → `Domain/Enums`, Klassifikation → `Application/Services`, beide `public`); der App-Klassifikator bleibt als dünner Adapter erhalten, der `Aufgabe`/`AktiveAufgabePanelItem` auf das gemeinsame `KiAusfuehrungsStatusDaten`-Record mappt. Ein neuer `LaufendeAutomatisierungenZaehlung`-Service (Application-Layer) wendet dieselbe Klassifikation auf `GetAktiveAufgabenAsync`-Kandidaten an. | Die Anforderung verlangt exakt die UI-Klassifikationssemantik auch für den Countdown-Auslöser. Duplizieren der Regel im Application-Layer würde bei jeder Klassifikationsänderung divergieren; Zählen nur über Live-Handles (`GetRunningCount`/`GetLiveLaufStatus`) ließe den Heartbeat-/Persistenz-Zweig, `Pausiert`, `PromptInWartellung` und `Wartend` unberücksichtigt und widerspräche der fachlichen Definition. Alternativer Push-Ansatz (ViewModel meldet die Anzahl an den Orchestrator) wurde verworfen: Der Orchestrator hinge dann an der Lebensdauer eines ViewModels und die Abbruch-/Neuauswertungs-Kette würde racy (Event → Abbruch sofort, neue Anzahl erst nach async Refresh). |
| Countdown-Abbruch bei Statuswechsel | Jedes Statuswechsel-Ereignis löst eine koaleszierte `EvaluiereAsync` aus (Abbruch + Neuauswertung). Ergibt die Neuauswertung weiterhin Anzahl 0, läuft ein bereits armiertes Countdown mit dem **bestehenden** Endzeitpunkt weiter (kein Zurücksetzen); nur wenn die Anzahl zwischendurch > 0 war oder die Toleranz geändert wurde, wird ein neuer voller Countdown armiert. | Wörtliches „jedes Ereignis setzt den Countdown zurück" führt zu Verhungern: Solange ein Prozess läuft (z. B. eine wartende Session), feuert `CliProcessManager` alle 30 s `LaufdatenChanged` (Heartbeat-Persistenz in `ExecuteWithAufgabeServiceAsync`, Z. 435) — eine Toleranz > 30 s käme nie zum Zuge, obwohl fachlich nichts läuft. Die gewählte Semantik erfüllt den Kern der Anforderung („Abbruch + Neuauswertung") und verhindert Shutdown auf veraltetem Zustand, ohne Noise-Ereignisse zu einem ewig verlängernden Grace-Period zu machen. |
| Countdown-Mechanik | `TimeProvider.CreateTimer` (`ITimer`) mit `timer.Change(...)`-Armierung — Muster `PromptZeitVersandService`; zusätzlich ein periodischer Re-Evaluierungs-Timer (5 s, nur solange aktiviert). | `TimeProvider` ist DI-registriert (`App.xaml.cs` Z. 295) und mit `FakeTimeProvider` deterministisch testbar. Der periodische Tick deckt zeitbasierte Übergänge ohne Event ab (Pausenende → `Pausiert`→`Laeuft`, Heartbeat-Timeout → Wegfall des Lauf-Belegs) — dieselbe Lücke, für die das ViewModel bereits einen 5-s-`DispatcherTimer` hat. |
| Orchestrator-Auswertung | Pull-Modell: Der Orchestrator wertet bei jedem Trigger selbst über `ILaufendeAutomatisierungenZaehlung` aus (serielle, koaleszierte `EvaluiereAsync` mit Semaphore + Erneut-Flag). | Zähler und Orchestrator liegen beide im Application-Layer, sind ohne UI testbar und benötigen keinen Lebenszyklus-Contract mit dem ViewModel. |
| Shutdown-Testmodus | Neuer `AufzeichnenderSystemShutdownService` (Infrastructure-Layer, Muster `AufzeichnenderProzessStarter`): registriert in `App.xaml.cs`, sobald `SOFTWARESCHMIEDE_TEST_DB_PATH` gesetzt ist (gleiche Bedingung wie der `IProzessStarter`-Testmodus, Z. ~271–280); schreibt eine Zeile in eine Logdatei neben der Test-DB. | Jeder E2E-Start setzt `SOFTWARESCHMIEDE_TEST_DB_PATH` — damit kann **kein** FlaUI-Test (auch kein Update-E2E, das diese Variable ohnehin voraussetzt) ein echtes OS-Shutdown auslösen. `SystemShutdownService` benutzt `Process.Start` direkt (nicht `IProzessStarter`), daher reicht der vorhandene Prozessstarter-Fake nicht aus; ein eigener Fake auf Interface-Ebene ist Pflicht (Anforderung). Die dateibasierte Aufzeichnung folgt dem `prozess-starts.log`-Muster und ist mit einfachem Polling prüfbar. |
| Persistenz-Anbindung im ViewModel | Neuer `AutoShutdownEinstellungenService` (App-Layer, Singleton, `IServiceScopeFactory` → scoped `AppEinstellungService`) — Muster `AufgabenListenModusService`; Injektion über `MainWindowOptionaleDienste`. | Singleton→Scoped-Muster ist etabliert; `MainWindowOptionaleDienste` ist der vorgesehene Erweiterungspunkt für optionale VM-Abhängigkeiten und hält den Konstruktor testkompatibel (Tests instanziieren das Record gezielt). |
| Toleranz-Änderung während Countdown | Wirkt sofort: `SetOptionen` mit geänderter Toleranz re-armiert den Countdown ab jetzt mit dem neuen Wert. | Erwartbares Nutzerverhalten („Ich verkürze die Frist und es dauert trotzdem noch volle alte Zeit" wäre irritierend); technisch identisch zum Armierungs-Pfad und E2E-freundlich (Toleranz 0 → sofortiger Nachweis). |
| Reaktivierung bei ausgeblendeter Checkbox (E2E-Schrittfolge) | Die E2E-Schrittfolge wird korrigiert: Nach dem Deaktivieren bei Anzahl 0 wird die CLI erneut gestartet, bevor reaktiviert wird. Die Sichtbarkeitsregel `AutoShutdownUiSichtbar` = `Anzahl > 0 ∨ aktiviert ∨ Countdown` bleibt unverändert. | Nach dem Deaktivieren bei Anzahl 0 ist die Checkbox per Regel korrekt ausgeblendet — ein Reaktivieren ohne laufende Automatisierung ist fachlich gar nicht vorgesehen (die Anforderung begrenzt die Aktivierbarkeit auf „wenn es solche gibt"). Eine „sticky" Sichtbarkeit nach erstmaligem Anzeigen würde die Regel weiter vom Anforderungswortlaut entfernen und unerklärtes UI-Verhalten erzeugen. Die korrigierte Sequenz bildet dagegen das reale Nutzerverhalten ab: Wer den Auto-Shutdown bei Anzahl 0 ausgeschaltet hat, kann ihn erst wieder einschalten, wenn wieder etwas läuft. |

## Programmabläufe

### Fachliche Lauf-Zählung (gemeinsam für Anzeige und Orchestrator)

1. `LaufendeAutomatisierungenZaehlung.ZaehleAsync` öffnet einen DI-Scope (`IServiceScopeFactory`) und lädt Kandidaten via `IAktiveAufgabenService.GetAktiveAufgabenAsync` — dieselbe Kandidatenmenge wie die Seitenleistenliste.
2. Pro Aufgabe wird ein `KiAusfuehrungsStatusDaten` aus `Aufgabe.Status`, `AusfuehrungsStatus`, `AktiveRunId`, `LastHeartbeatUtc`, `LaufStatus`, `PausiertBisUtc` plus `IRunningAutomationStatusSource.GetLiveLaufStatus(id)` und `PromptZeitVersandService.GetScheduledPromptStatus(id) is not null` befüllt.
3. `KiAusfuehrungsStatusKlassifikation.Klassifiziere(statusDaten)` liefert je Aufgabe den `KiAusfuehrungsZustand`; gezählt werden ausschließlich `Laeuft`.
4. `MainWindowViewModel` wendet dieselbe Klassifikation (über den verbleibenden App-Adapter) auf die bereits gemappte `AktiveAufgabenListe` an und setzt `LaufendeAutomatisierungenAnzahl` — identisches Ergebnis ohne zweite DB-Abfrage.

Beteiligte Klassen/Komponenten: `LaufendeAutomatisierungenZaehlung`, `KiAusfuehrungsStatusKlassifikation`, `KiAusfuehrungsStatusDaten`, `IAktiveAufgabenService`, `IRunningAutomationStatusSource`, `PromptZeitVersandService`, `IServiceScopeFactory`, `MainWindowViewModel`.

### Auto-Shutdown aktivieren und Optionen setzen (UI → Orchestrator)

1. Nutzer aktiviert die Checkbox → `AutoShutdownAktiviert` (VM) → `_autoShutdownOrchestrator.SetEnabled(true)` → Orchestrator stößt sofort eine `EvaluiereAsync` an (bei Anzahl 0 armiert der Countdown sofort).
2. Nutzer deaktiviert → `SetEnabled(false)` → Orchestrator disarmiert einen laufenden Countdown und meldet `ZustandGeaendert`.
3. Nutzer ändert Toleranz-Eingabefeld → VM validiert (`int.TryParse`, 0–1440) → bei gültigem Wert: `AutoShutdownEinstellungenService.SpeichereToleranzMinutenAsync` + `SetOptionen(toleranz, erzwingen)`; bei ungültigem Wert: `AutoShutdownToleranzFehler`-Text wird sichtbar, nichts wird angewendet/persistiert.
4. Nutzer schaltet Force-Checkbox → `AutoShutdownEinstellungenService.SpeichereErzwingenAsync` + `SetOptionen(...)`.
5. `SetOptionen` speichert die Werte im Orchestrator; bei laufendem Countdown und geänderter Toleranz wird `CountdownEndeUtc = now + neueToleranz` neu armiert und `ZustandGeaendert` ausgelöst. Force wird erst beim Auslösen ausgewertet (keine Re-Armiierung nötig).

Beteiligte Klassen/Komponenten: `MainWindowViewModel`, `IAutoShutdownOrchestrator`, `AutoShutdownEinstellungenService`, `AppEinstellungService`.

### Countdown-Lebenszyklus (Orchestrator)

1. Trigger-Quellen: `RunningCountChanged`, `AufgabeLaufdatenChangedNotifier.LaufdatenChanged`, periodischer 5-s-Sicherheitstick (`TimeProvider`-Timer, nur armiert solange `SetEnabled(true)`), `SetEnabled`/`SetOptionen`-Aufrufe. Alle Trigger laufen in eine koaleszierte `EvaluiereAsync` (Semaphore + Erneut-Flag: parallele Trigger während einer laufenden Auswertung erzeugen höchstens einen Nachlauf).
2. `EvaluiereAsync`: `count = await _zaehlung.ZaehleAsync(ct)`; unter `_syncLock`:
   - `count > 0` → `_shutdownAusgeloest = false`, Countdown disarmieren (`CountdownEndeUtc = null`, Timer entschärfen), `ZustandGeaendert` melden.
   - `enabled && count == 0 && !_shutdownAusgeloest && CountdownEndeUtc == null` → `CountdownEndeUtc = timeProvider.GetUtcNow() + toleranz`, Countdown-Timer auf Restlaufzeit armieren, `ZustandGeaendert` melden.
   - sonst → keine Änderung (bestehender Countdown läuft mit unverändertem Endzeitpunkt weiter; Shutdown bleibt pro Null-Übergang einmalig).
3. Countdown-Timer-Callback → `OnCountdownAbgelaufenAsync`: Final-Recheck — `enabled` noch gesetzt **und** `ZaehleAsync() == 0` → `_shutdownAusgeloest = true`, `CountdownEndeUtc = null`, `ZustandGeaendert`, Warn-Log, `ISystemShutdownService.RequestShutdownAsync(erzwingen)` (Exceptions werden wie bisher geloggt und verschluckt). Recheck negativ → erneute `EvaluiereAsync`.
4. `Dispose`: Abmeldung von `RunningCountChanged` und `LaufdatenChanged`, beide `ITimer` entsorgen.

Beteiligte Klassen/Komponenten: `AutoShutdownOrchestrator`, `IRunningAutomationStatusSource`, `AufgabeLaufdatenChangedNotifier`, `ILaufendeAutomatisierungenZaehlung`, `ISystemShutdownService`, `TimeProvider`, `ITimer`.

### Sichtbarkeit und Countdown-Anzeige (ViewModel/XAML)

1. `AktiveAufgabenAktualisierenAsync` setzt nach dem Merge `LaufendeAutomatisierungenAnzahl` (Klassifikation der Liste) → Fußzeilen-Text „Laufende Automatisierungen: {n}".
2. `AutoShutdownUiSichtbar` = `Anzahl > 0 || AutoShutdownAktiviert || CountdownEndeUtc != null` → steuert Checkbox + Optionsbereich (bleibt im Countdown sichtbar, damit Deaktivieren möglich ist).
3. `AutoShutdownOptionenSichtbar` = `AutoShutdownAktiviert` → steuert Toleranz-Feld + Force-Checkbox.
4. Orchestrator-Event `ZustandGeaendert` → VM liest `CountdownEndeUtc` per `_dispatcherInvoke`: `!= null` → `AutoShutdownCountdownText` setzen (Format „Herunterfahren in mm:ss") und einen 1-s-`DispatcherTimer` für die Restzeit-Ticks starten; `== null` → Timer stoppen, Text leeren. Timer-Tick rechnet `CountdownEndeUtc - now` neu; Rest ≤ 0 → Timer-Callback des Orchestrators übernimmt (VM wartet auf `ZustandGeaendert`).
5. Beim VM-Start: `AutoShutdownEinstellungenLadenAsync` (SafeFireAndForget, Race-Guard analog `GespeichertenAufgabenListenModusLadenAsync`) → gespeicherte Force-/Toleranz-Werte in die Properties setzen (ohne Enabled) und per `SetOptionen` an den Orchestrator durchreichen.
6. `Dispose`: `ZustandGeaendert` abmelden, Countdown-`DispatcherTimer` stoppen.

Beteiligte Klassen/Komponenten: `MainWindowViewModel`, `IAutoShutdownOrchestrator`, `AutoShutdownEinstellungenService`, `DispatcherTimer`, `MainWindow.xaml`.

### OS-Shutdown-Auslösung

1. `SystemShutdownService.RequestShutdownAsync(erzwingen, ct)` ruft `ResolveShutdownCommand(erzwingen)` (internal für Tests) auf.
2. Windows: `("shutdown", "/s /f /t 0")` wenn `erzwingen`, sonst `("shutdown", "/s /t 0")`; Linux/macOS: bestehende Kommandos unverändert (Force entfällt, siehe Scope-Ausschluss 6).
3. Prozessstart (`UseShellExecute = false`, `CreateNoWindow = true`) + `WaitForExitAsync`; `ExitCode != 0` → `InvalidOperationException` (vom Orchestrator geloggt/verschluckt).
4. Vor dem Aufruf: `LogWarning` mit Force-Flag.

Beteiligte Klassen/Komponenten: `ISystemShutdownService`, `SystemShutdownService`, `AutoShutdownOrchestrator`.

### Testmodus-Shutdown-Aufzeichnung

1. `App.xaml.cs` `ConfigureServices`: Ist `TerminalSessionService.TestDatenbankPfadVariable` (`SOFTWARESCHMIEDE_TEST_DB_PATH`) gesetzt, wird `ISystemShutdownService` → `AufzeichnenderSystemShutdownService` registriert (analog dem `IProzessStarter`-Zweig, Z. ~271–280), sonst `SystemShutdownService`.
2. `AufzeichnenderSystemShutdownService.RequestShutdownAsync` hängt eine Zeile (`erzwingen={true|false}|{utc}|pid`) an `system-shutdowns.log` im Verzeichnis der Test-DB an (`ResolveLogDateiPfad(testDbPath)`, statische Methode + `LogDateiName`-Konstante wie `AufzeichnenderProzessStarter`); kein Prozessstart, kein OS-Kontakt.
3. Der Update-E2E-Zweig benötigt keine eigene Registrierung — `SOFTWARESCHMIEDE_TEST_DB_PATH` ist dort ohnehin Pflicht.

Beteiligte Klassen/Komponenten: `AufzeichnenderSystemShutdownService`, `ISystemShutdownService`, `App.xaml.cs` `ConfigureServices`.

### Persistenz-Wiederherstellung beim App-Start

1. `AutoShutdownEinstellungenService.LadeAsync` liest per Scope `AppEinstellungService.GetAutoShutdownErzwingenAsync` (Default `false`) und `GetAutoShutdownToleranzMinutenAsync` (Default `5`, Clamp 0–1440).
2. `MainWindowViewModel` setzt `AutoShutdownErzwingen` und `AutoShutdownToleranzMinuten` auf die geladenen Werte (ohne Persistenz-Write-Back, ohne Enabled) und ruft `SetOptionen` am Orchestrator.
3. `AutoShutdownAktiviert` bleibt `false` (Scope-Ausschluss 1) — erst ein Nutzer-Check armiert die Funktion.

Beteiligte Klassen/Komponenten: `AutoShutdownEinstellungenService`, `AppEinstellungService`, `MainWindowViewModel`, `IAutoShutdownOrchestrator`.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `KiAusfuehrungsZustand` | Enum (verschoben aus `AppConverters.cs` nach `Softwareschmiede/Domain/Enums`, `public`) | Fachlicher Zustand `Pausiert`/`PromptInWartestellung`/`Laeuft`/`Wartet`/`Bereit`, geteilt zwischen App-Konvertern und Orchestrator-Zählung. |
| `KiAusfuehrungsStatusDaten` | Datenmodellklasse (Record, `public`, Application-Layer) | Parameterbündel der Klassifikation (Status, AusfuehrungsStatus, AktiveRunId, LastHeartbeatUtc, LaufStatus, LiveLaufStatus, HasScheduledPrompt, PausiertBisUtc) — bisher `private sealed record StatusDaten` innerhalb des App-Klassifikators. |
| `KiAusfuehrungsStatusKlassifikation` | Klasse (verschoben nach `Softwareschmiede/Application/Services`, `public static`) | `Klassifiziere(KiAusfuehrungsStatusDaten, out TimeSpan pausiertRestzeit)` — unveränderte Regellogik. |
| `KiAusfuehrungsStatusKlassifikation` (Adapter, App) | Klasse (`internal static`, bleibt in `AppConverters.cs`) | `Klassifiziere(object value, out TimeSpan)` — mappt `Aufgabe`/`AktiveAufgabePanelItem` auf `KiAusfuehrungsStatusDaten` und delegiert an die gemeinsame Klassifikation; öffentliche Signatur der Konverter bleibt unverändert. |
| `ILaufendeAutomatisierungenZaehlung` | Interface (Application-Layer) | `Task<int> ZaehleAsync(CancellationToken ct = default)` — Abstraktion für Mocking in Orchestrator-Tests. |
| `LaufendeAutomatisierungenZaehlung` | Klasse (Application-Layer, Singleton) | Klassifiziert `GetAktiveAufgabenAsync`-Kandidaten mit Live-/Prompt-Belegen und zählt `Laeuft`. Deps: `IServiceScopeFactory`, `IRunningAutomationStatusSource`, `PromptZeitVersandService`. |
| `AutoShutdownEinstellungenService` | Klasse (App-Layer `Softwareschmiede.App/Services`, Singleton) | `LadeAsync`/`SpeichereErzwingenAsync`/`SpeichereToleranzMinutenAsync` über `IServiceScopeFactory` → `AppEinstellungService` (Muster `AufgabenListenModusService`). |
| `AufzeichnenderSystemShutdownService` | Klasse (Infrastructure-Layer `Softwareschmiede/Infrastructure/Services`) | `ISystemShutdownService`-Fake für den Testmodus; schreibt aufgezeichnete Aufrufe (inkl. Force-Flag) in `system-shutdowns.log` neben der Test-DB; Konstanten `LogDateiName` + `ResolveLogDateiPfad`. |

## Änderungen an bestehenden Klassen

### `ISystemShutdownService` (Interface, `src/Softwareschmiede/Domain/Interfaces/`)

- **Geänderte Methoden:** `RequestShutdownAsync` — neuer Parameter `bool erzwingen` vor `CancellationToken`: `Task RequestShutdownAsync(bool erzwingen, CancellationToken cancellationToken = default)`.

### `SystemShutdownService` (Klasse, `src/Softwareschmiede/Infrastructure/Services/`)

- **Geänderte Methoden:** `RequestShutdownAsync` — Signatur um `erzwingen` erweitert, wird an `ResolveShutdownCommand` weitergereicht; Warn-Log enthält das Flag. `ResolveShutdownCommand` — Parameter `bool erzwingen`; Windows-Zweig liefert `/s /f /t 0` nur bei `erzwingen == true`, sonst `/s /t 0`; Sichtbarkeit `private` → `internal` für Argument-Mapping-Tests (`InternalsVisibleTo` für Tests existiert bereits).

### `IAutoShutdownOrchestrator` (Interface, `src/Softwareschmiede/Application/Services/`)

- **Neue Methoden:** `SetOptionen(int toleranzMinuten, bool erzwingen)` — setzt atomar Toleranz und Force-Flag.
- **Neue Eigenschaften:** `DateTimeOffset? CountdownEndeUtc` — `null` = kein Countdown aktiv; `bool IstAktiviert` — aktueller Enabled-Zustand (für UI-/Testdiagnose).
- **Neue Events:** `event Action? ZustandGeaendert` — feuert bei Armierung, Disarmierung, Auslösung und Options-/Enabled-Wechseln.

### `AutoShutdownOrchestrator` (Klasse, `src/Softwareschmiede/Application/Services/`)

- **Neue Abhängigkeiten (Konstruktor):** `AufgabeLaufdatenChangedNotifier`, `ILaufendeAutomatisierungenZaehlung`, `TimeProvider` (zusätzlich zu `IRunningAutomationStatusSource`, `ISystemShutdownService`, `ILogger`).
- **Neue Methoden:** `SetOptionen` (siehe Interface), intern `EvaluiereAsync` (koalesziert), `OnCountdownAbgelaufenAsync` (Final-Recheck + `RequestShutdownAsync(erzwingen)`), `OnLaufdatenChanged`, `OnSicherheitsTick`.
- **Geänderte Methoden:** `SetEnabled` — löst jetzt bei `false` Disarmierung + Event und bei `true` eine sofortige `EvaluiereAsync` aus. `OnRunningCountChanged` — ersetzt die bisherige Sofort-Shutdown-Logik durch Anstoß der `EvaluiereAsync` (Idempotenz-Guard `_shutdownRequestedForCurrentZeroTransition` wird zu `_shutdownAusgeloest` pro Null-Phase).
- **Neue interne Zustände:** `_toleranzMinuten`, `_erzwingen`, `_countdownTimer` (`ITimer`), `_sicherheitsTimer` (`ITimer`, 5 s), `CountdownEndeUtc`, `_evalSemaphore`/`_evalErneutAngefordert`.
- **Geänderte Abonnements:** zusätzlich `AufgabeLaufdatenChangedNotifier.LaufdatenChanged`; `Dispose` meldet beide ab und entsorgt beide Timer.

### `AppEinstellungService` (Klasse, `src/Softwareschmiede/Application/Services/`)

- **Neue Konstanten:** `AutoShutdownErzwingenKey = "autoshutdown.force"`, `AutoShutdownToleranzMinutenKey = "autoshutdown.toleranz.minuten"`, `AutoShutdownDefaultToleranzMinuten = 5` (und ggf. `AutoShutdownMaxToleranzMinuten = 1440`).
- **Neue Methoden:** `GetAutoShutdownErzwingenAsync` (Default `false`), `SetAutoShutdownErzwingenAsync(bool)`, `GetAutoShutdownToleranzMinutenAsync` (Default `5`, `Math.Clamp(wert, 0, 1440)`), `SetAutoShutdownToleranzMinutenAsync(int)`.

### `MainWindowOptionaleDienste` (Record, `src/Softwareschmiede.App/ViewModels/`)

- **Neue Parameter (ans Ende, mit Defaults):** `IAutoShutdownOrchestrator? AutoShutdownOrchestrator = null`, `AutoShutdownEinstellungenService? AutoShutdownEinstellungenService = null`, `TimeProvider? TimeProvider = null` — Testkompatibilität der bestehenden Konstruktor-Aufrufe bleibt erhalten.

### `MainWindowViewModel` (Klasse, `src/Softwareschmiede.App/ViewModels/`)

- **Neue Eigenschaften:** `LaufendeAutomatisierungenAnzahl` (int), `AutoShutdownAktiviert` (bool, Setter → `SetEnabled` + Sichtbarkeits-Notifications), `AutoShutdownToleranzMinuten` (string, Setter → Validierung + Persistenz + `SetOptionen`), `AutoShutdownErzwingen` (bool, Setter → Persistenz + `SetOptionen`), `AutoShutdownCountdownText` (string?), `AutoShutdownToleranzFehler` (string?), `AutoShutdownUiSichtbar` (bool), `AutoShutdownOptionenSichtbar` (bool), `AutoShutdownCountdownAktiv` (bool).
- **Neue Methoden:** `AutoShutdownEinstellungenLadenAsync` (öffentlich awaitbar wie `GespeichertenAufgabenListenModusLadenAsync`), intern `OnAutoShutdownZustandGeaendert`, `OnAutoShutdownCountdownTick`, `AutoShutdownCountdownAnzeigeAktualisieren`.
- **Geänderte Methoden:** `AktiveAufgabenAktualisierenAsync` — setzt nach dem Listen-Merge `LaufendeAutomatisierungenAnzahl` (Anzahl der als `Laeuft` klassifizierten Items) + abhängige Sichtbarkeiten. Konstruktor — abonniert `ZustandGeaendert`, legt den 1-s-Countdown-`DispatcherTimer` an (gestoppt), startet `AutoShutdownEinstellungenLadenAsync`. `Dispose` — Event-Abmeldung + Timer-Stop.
- **Null-Toleranz:** Ist `AutoShutdownOrchestrator`/`AutoShutdownEinstellungenService` nicht injiziert (Tests), bleibt der Block inert (Anzeige der Anzahl funktioniert weiterhin).

### `MainWindow.xaml` (View, `src/Softwareschmiede.App/Views/`)

- **Neuer Fußzeilen-Block** im `StackPanel` `Grid.Row="2"` direkt nach der Trennlinie (vor `AppVersionText`), gesamter Block in einem Container mit `Visibility="{Binding IsNavigationExpanded, Converter=BoolToVisibilityConverter}"`:
  - `TextBlock` `{Binding LaufendeAutomatisierungenAnzahl, StringFormat=...}` — AutomationId `AutoShutdownLaufendeAnzahl`, immer sichtbar.
  - `CheckBox` `IsChecked="{Binding AutoShutdownAktiviert}"` — AutomationProperties.Name „Auto-Shutdown" + AutomationId `AutoShutdownAktivieren`, `Visibility` an `AutoShutdownUiSichtbar`.
  - Zeile mit `TextBlock` „Toleranz (Minuten):" + `TextBox` `Text="{Binding AutoShutdownToleranzMinuten, UpdateSourceTrigger=PropertyChanged}"` — AutomationId `AutoShutdownToleranzMinuten`, `Visibility` an `AutoShutdownOptionenSichtbar`.
  - `CheckBox` `IsChecked="{Binding AutoShutdownErzwingen}"` — „Herunterfahren erzwingen", AutomationId `AutoShutdownErzwingen`, `Visibility` an `AutoShutdownOptionenSichtbar`.
  - `TextBlock` `{Binding AutoShutdownCountdownText}` — AutomationId `AutoShutdownCountdown`, `Visibility` an `AutoShutdownCountdownAktiv`.
  - `TextBlock` `{Binding AutoShutdownToleranzFehler}` — AutomationId `AutoShutdownToleranzFehler`, `Visibility` via `NullOrEmptyToVisibilityConverter`, Fehler-Stil (Vordergrund wie `FehlerMeldung`).

### `App.xaml.cs` (Composition Root)

- **Neue Registrierungen (Produktivzweig):** `AddSingleton<LaufendeAutomatisierungenZaehlung>` + `ILaufendeAutomatisierungenZaehlung` → gleiche Instanz; `AddSingleton<AutoShutdownOrchestrator>` + `IAutoShutdownOrchestrator` → gleiche Instanz; `AddSingleton<AutoShutdownEinstellungenService>`; `MainWindowOptionaleDienste`-Factory (Z. ~326–331) um die drei neuen Parameter erweitert.
- **Bedingte Registrierung:** `ISystemShutdownService` — bei gesetztem `SOFTWARESCHMIEDE_TEST_DB_PATH` → `AufzeichnenderSystemShutdownService` (Log-Pfad via `ResolveLogDateiPfad(dbPath)`), sonst `SystemShutdownService` (symmetrisch zum `IProzessStarter`-Zweig).
- **Aktivierung:** `StartupAsync` — `_host.Services.GetRequiredService<AutoShutdownOrchestrator>()` im selben try/catch-Stil wie `CliProcessManager` (Z. 84–91), damit das Event-Abo ab App-Start steht.

### `docs/help/einstellungen/beschreibung.md` (Dokumentation)

- **Geänderter Abschnitt „Automatisches Herunterfahren" (Z. ~148–150):** Beschreibung an die tatsächliche Funktion anpassen (Seitenleisten-Steuerung, Klassifikation, Toleranz-Countdown, Abbruch bei Statuswechsel, Force-Option, Persistenz von Force+Toleranz, Aktivierung pro Sitzung) — der aktuelle Text beschreibt noch den Blazor-Ist-Zustand und ist fachlich falsch.

## Datenbankmigrationen

Keine. Die Persistenz nutzt die bestehende Key-Value-Entität `AppEinstellung` (`AppEinstellungen`-Tabelle) über `AppEinstellungService` — neue Schlüssel entstehen zur Laufzeit beim ersten `SetSettingAsync`.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `AutoShutdownToleranzMinuten` (TextBox, VM-String) | Ganzzahl (`int.TryParse`, invariant), Wertebereich 0–1440 Minuten | Ungültig/leer/außerhalb → `AutoShutdownToleranzFehler`-Text „Toleranz muss eine ganze Zahl zwischen 0 und 1440 Minuten sein." sichtbar; Wert wird weder persistiert noch an den Orchestrator gereicht (bisheriger Wert bleibt wirksam). |
| `SetAutoShutdownToleranzMinutenAsync` (Service) | `Math.Clamp(wert, 0, 1440)` beim Lesen (Muster `GetScmTaskCompletionWaitMinutesAsync`) | Gespeicherter Wert außerhalb → auf Grenzwert geclampt, nie Exception. |
| `SetOptionen` (Orchestrator) | Negative Toleranzwerte werden auf 0 normalisiert (defensiv) | Nicht erreichbar über die UI-Validierung; dient als Auffanglinie für direkte Aufrufer. |

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `autoshutdown.force` (`AppEinstellungService.AutoShutdownErzwingenKey`) | bool | `false` | „Herunterfahren erzwingen" — persistiert, beim Start vorausgewählt. |
| `autoshutdown.toleranz.minuten` (`AppEinstellungService.AutoShutdownToleranzMinutenKey`) | int (Minuten) | `5` (`AutoShutdownDefaultToleranzMinuten`), Clamp 0–1440 | Toleranzzeit des Shutdown-Countdowns — persistiert, beim Start vorausgewählt; `0` = sofort auslösen. |

Nicht persistiert (bewusst): `AutoShutdownAktiviert` (Scope-Ausschluss 1).

## Seiteneffekte und Risiken

- **`ISystemShutdownService`-Signaturänderung:** Alle Implementierungen und Aufrufer müssen angepasst werden — `SystemShutdownService`, `AutoShutdownOrchestrator`, `FakeSystemShutdownService` in `AutoShutdownOrchestratorTests`, neuer `AufzeichnenderSystemShutdownService`. Compiler-zwingend, kein stiller Bruch.
- **`KiAusfuehrungsStatusKlassifikation`-Verschiebung:** Enum und Klassifikation werden `public` in einem anderen Assembly; der App-Adapter behält die bisherige Aufrufsignatur, sodass `KiAusfuehrungsStatusConverter`/`KiAusfuehrungsStatusSymbolConverter` und deren Tests unverändert weiterlaufen. Risiko: versehentliche API-Aufweitung — durch `internal`-Adapter in der App begrenzt.
- **Testmodus-Registrierung:** `AufzeichnenderSystemShutdownService` ist bei **jedem** E2E-Start aktiv (TEST_DB_PATH-Gate). Positiver Nebeneffekt: Selbst ein versehentlicher Shutdown-Aufruf aus Produktcode heraus kann in Tests nie das System herunterfahren.
- **`LaufdatenChanged` als Abbruchquelle:** Feuert auch für Heartbeat- und sonstige Laufdaten-Schreibvorgänge; die Neuauswertungs-Semantik (Endzeitpunkt bleibt bei unveränderter Anzahl) verhindert daraus folgende Verzögerungen. `EvaluiereAsync` läuft pro Ereignis mit einer DB-Abfrage — bei typischen Ereignisraten (Heartbeat 30 s, Statuswechsel) unkritisch.
- **Fußzeilen-Platz:** Der neue Block vergrößert den `Auto`-Bereich `Grid.Row="2"` um bis zu 5 Zeilen; bei sehr kleinen Fensterhöhen kann die `ScrollViewer`-Liste (Row 1) enger werden — wie bei `UpdateHinweis` bestehendes Verhalten.
- **Klassischer CLI-Start ohne Session:** `GetLiveLaufStatus` liefert `Laeuft` (kein WartetAufEingabe möglich) — Zählung und Countdown funktionieren konsistent.
- **Heartbeat-Beleg ohne Prozess:** Nach unbehandeltem Prozessende zählt eine Aufgabe bis zu 5 min (Heartbeat-Timeout) weiter als `Laeuft` — deckt sich mit der Anzeige auf den Kacheln; der Countdown startet entsprechend später. Gewollt konsistent.
- **Dokumentation:** `docs/help/einstellungen/beschreibung.md` wird korrigiert (bisherige Beschreibung war ohnehin falsch gegenüber dem DI-Ist-Stand).

## Umsetzungsreihenfolge

1. **Baseline-Testlauf erheben**
   - Voraussetzungen: .NET Desktop Workload; `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` inline setzen.
   - Beschreibung: Vollständiger Build, dann `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"` (Vordergrund, nie im Hintergrund). Ausgangszustand dokumentieren — die Bestandsaufnahme hat ihn bewusst nicht erhoben.

2. **Klassifikation in den Shared-Layer verschieben**
   - Voraussetzungen: Keine.
   - Beschreibung: `KiAusfuehrungsZustand` (→ `Domain/Enums`, public) und `KiAusfuehrungsStatusKlassifikation` samt `StatusDaten`→`KiAusfuehrungsStatusDaten` (→ `Application/Services`, public) verschieben; in `AppConverters.cs` einen `internal` Adapter belassen, der `Aufgabe`/`AktiveAufgabePanelItem` mappt. Konverter-Tests müssen ohne Anpassung grün bleiben.

3. **`ISystemShutdownService` Force-Parameter + `SystemShutdownService`-Mapping**
   - Voraussetzungen: Keine.
   - Beschreibung: `RequestShutdownAsync(bool erzwingen, CancellationToken)`; `ResolveShutdownCommand(bool erzwingen)` internal; Windows-Argumente mit/ohne `/f`. Bestehende Test-Fakes mitziehen (Signatur).

4. **`AufzeichnenderSystemShutdownService` (Testmodus-Fake)**
   - Voraussetzungen: Schritt 3 (neue Signatur).
   - Beschreibung: Neue Klasse in `Infrastructure/Services` nach `AufzeichnenderProzessStarter`-Muster (`LogDateiName = "system-shutdowns.log"`, `ResolveLogDateiPfad`), zeilenbasierte Aufzeichnung inkl. Force-Flag, kein Prozessstart.

5. **`AppEinstellungService`-Schlüssel und typsichere Get/Set**
   - Voraussetzungen: Keine.
   - Beschreibung: Konstanten + `Get/SetAutoShutdownErzwingenAsync`, `Get/SetAutoShutdownToleranzMinutenAsync` mit Default 5 und Clamp 0–1440 (Muster `ScmTaskCompletionWaitMinutes*`).

6. **`ILaufendeAutomatisierungenZaehlung` + `LaufendeAutomatisierungenZaehlung`**
   - Voraussetzungen: Schritt 2 (Shared-Klassifikation).
   - Beschreibung: Interface + Implementierung im Application-Layer; Kandidaten via `IAktiveAufgabenService` aus eigenem Scope; Live-Substatus via `IRunningAutomationStatusSource`; Prompt-Beleg via `PromptZeitVersandService`.

7. **`IAutoShutdownOrchestrator`/`AutoShutdownOrchestrator` Countdown-Umbau**
   - Voraussetzungen: Schritte 3, 6.
   - Beschreibung: Interface-Erweiterung (`SetOptionen`, `CountdownEndeUtc`, `IstAktiviert`, `ZustandGeaendert`); Umbau auf koaleszierte `EvaluiereAsync`, `TimeProvider`-Countdown-Timer + 5-s-Sicherheitstick, `LaufdatenChanged`-Abo, Force-Durchreichung, Semantik gemäß Designentscheidungen.

8. **`AutoShutdownEinstellungenService` (App-Layer)**
   - Voraussetzungen: Schritt 5.
   - Beschreibung: Singleton mit `IServiceScopeFactory` → `AppEinstellungService`; `LadeAsync`/`SpeichereErzwingenAsync`/`SpeichereToleranzMinutenAsync`.

9. **`MainWindowOptionaleDienste` + `MainWindowViewModel`-Erweiterung**
   - Voraussetzungen: Schritte 2, 7, 8.
   - Beschreibung: Record um drei optionale Parameter erweitern; VM-Properties, Sichtbarkeiten, Toleranz-Validierung, Countdown-Anzeige-Timer, Einstellungs-Laden, Orchestrator-Anbindung, Dispose-Aufräumen.

10. **`MainWindow.xaml` Fußzeilen-UI**
    - Voraussetzungen: Schritt 9 (bindbare Properties).
    - Beschreibung: Auto-Shutdown-Block im Fußzeilen-`StackPanel` mit AutomationIds, Sichtbarkeitsbindungen und Collapsed-Navigation-Verhalten.

11. **`App.xaml.cs` DI-Verdrahtung + Aktivierung**
    - Voraussetzungen: Schritte 4, 6, 7, 8, 9.
    - Beschreibung: Bedingte `ISystemShutdownService`-Registrierung, Singleton-Registrierungen, `MainWindowOptionaleDienste`-Factory-Erweiterung, Orchestrator-`GetRequiredService` in `StartupAsync`.

12. **Unit-Tests anpassen und ergänzen**
    - Voraussetzungen: Schritte 1–11.
    - Beschreibung: `AutoShutdownOrchestratorTests` auf Countdown-Semantik umbauen (`FakeTimeProvider`, Fake-`ILaufendeAutomatisierungenZaehlung`); neue Testdateien für `LaufendeAutomatisierungenZaehlung`, `AufzeichnenderSystemShutdownService`, `SystemShutdownService`-Argument-Mapping, `AppEinstellungService`-Schlüssel; `MainWindowViewModelTests`-Erweiterungen.

13. **E2E-Test + Hilfsmethoden**
    - Voraussetzungen: Schritte 1–12, `WpfTestBase`, `MenuView`, `TaskDetailView`, `SetupProjectMitNeuerAufgabe`.
    - Beschreibung: `WpfTestBase` um `ResolveSystemShutdownLogPfad`/`WaitForSystemShutdownEintragAsync` erweitern; `MenuView` um Auto-Shutdown-Getter/-Setter; `E2E_AutoShutdown.cs` mit einer konsolidierten Testmethode; Einbindung in `MainTest.cs` als eigener `[SkippableFact]` (eigener App-Lifecycle inkl. `RestartAppPreservingDatabase`).

14. **Dokumentation + Format**
    - Voraussetzungen: Schritte 1–13.
    - Beschreibung: `docs/help/einstellungen/beschreibung.md` korrigieren; `dotnet format Softwareschmiede.slnx --verify-no-changes` als Abschlussprüfung (Pre-Push-Spiegel des CI-Schritts).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `CountdownStartet_WennAnzahlAufNullFaellt` | `AutoShutdownOrchestratorTests` (umgebaut) | Bei aktiviert + Übergang auf 0 wird `CountdownEndeUtc` gesetzt und `ZustandGeaendert` gefeuert; kein sofortiger `RequestShutdownAsync`. |
| `ShutdownErstNachToleranzablauf` | `AutoShutdownOrchestratorTests` | `FakeTimeProvider.Advance` bis Toleranzende → genau ein `RequestShutdownAsync` mit korrektem Force-Flag. |
| `CountdownBleibtBeiUnveraenderterAnzahl` | `AutoShutdownOrchestratorTests` | `LaufdatenChanged`-Ereignis während Countdown mit weiterhin 0 → `CountdownEndeUtc` unverändert, kein Neustart. |
| `CountdownAbbruchBeiWiederanlauf` | `AutoShutdownOrchestratorTests` | `RunningCountChanged`/`LaufdatenChanged` mit Anzahl >0 → Countdown disarmiert; Rückkehr auf 0 → neuer voller Countdown. |
| `DeaktivierungWaehrendCountdown` | `AutoShutdownOrchestratorTests` | `SetEnabled(false)` während armiertem Countdown → disarmiert, kein Shutdown nach Advance. |
| `ToleranzAenderungReArmiert` | `AutoShutdownOrchestratorTests` | `SetOptionen` mit neuer Toleranz während Countdown → `CountdownEndeUtc` = now + neuer Wert. |
| `FinalRecheckVerhindertShutdown` | `AutoShutdownOrchestratorTests` | Zum Timer-Callback ist die Anzahl wieder >0 → kein `RequestShutdownAsync`. |
| `EinmalProNullPhase` | `AutoShutdownOrchestratorTests` | Nach Auslösung kein erneuter Shutdown, bis Anzahl >0 → 0 erneut. |
| `ExceptionImShutdownServiceWirdGefangen` | `AutoShutdownOrchestratorTests` | Wie bisher: Exception → Log, kein Propagieren. |
| `DisposeMeldetAlleAbosUndTimer` | `AutoShutdownOrchestratorTests` | Abmeldung beider Events, Timer disposed, keine Reaktion mehr. |
| `ZaehleAsync_KlassifiziertAlleZustaende` | `LaufendeAutomatisierungenZaehlungTests` (neu) | Pro Zustand (`Laeuft` live, `WartetAufEingabe` live, `Wartend`, `Pausiert`, `PromptInWartestellung`, `Bereit`, Heartbeat-Beleg) korrekte Zählung; Kandidatenmenge = `GetAktiveAufgabenAsync`. |
| `Get/Set-DefaultsUndClamp` | `AppEinstellungServiceTests_*` (Erweiterung) | `autoshutdown.force` Default `false`; `autoshutdown.toleranz.minuten` Default `5`; Clamp 0–1440; Roundtrip. |
| `ZeichnetAufrufMitForceFlagAuf` | `AufzeichnenderSystemShutdownServiceTests` (neu) | Zeile enthält Force-Wert; kein Prozessstart; `ResolveLogDateiPfad` neben Test-DB. |
| `WindowsArgsMitUndOhneForce` | `SystemShutdownServiceTests` (neu) | `ResolveShutdownCommand(true|false)` → `/f` nur bei Force (Windows-Branch; auf dem Windows-Test-Runner auswertbar). |
| `LaufendeAnzahlUndSichtbarkeiten` | `MainWindowViewModelTests` (Erweiterung) | `LaufendeAutomatisierungenAnzahl` aus `AktiveAufgabenListe` (Läuft vs. Wartet vs. Pausiert); `AutoShutdownUiSichtbar`/`AutoShutdownOptionenSichtbar`/`AutoShutdownCountdownAktiv` in allen Zustandskombinationen. |
| `EinstellungenWerdenGeladenNichtAktiviert` | `MainWindowViewModelTests` | Persistierte Force/Toleranz werden in Properties + `SetOptionen` übernommen; `AutoShutdownAktiviert` bleibt `false`. |
| `ToleranzValidierung` | `MainWindowViewModelTests` | Ungültige Eingabe → `AutoShutdownToleranzFehler` gesetzt, kein `SetOptionen`/Persistenz; gültige Eingabe → Fehler leer, `SetOptionen` aufgerufen. |
| `DeaktivierenRuftSetEnabled` | `MainWindowViewModelTests` | Checkbox-Wechsel → `SetEnabled(false/true)` am Orchestrator-Mock; Countdown-Event → `AutoShutdownCountdownText` gefüllt/geleert. |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `AutoShutdownOrchestratorTests` (alle 8 Methoden) | Semantikwechsel: kein Sofort-Shutdown mehr; neue Konstruktor-Abhängigkeiten (`AufgabeLaufdatenChangedNotifier`, `ILaufendeAutomatisierungenZaehlung`, `TimeProvider`); `FakeSystemShutdownService` an neue Signatur `RequestShutdownAsync(bool, CancellationToken)` anpassen. |
| `MainWindowViewModelTests` | `MainWindowOptionaleDienste`-Record erhält neue optionale Parameter — bestehende Aufrufe bleiben durch Default-Werte kompilierbar; `CreateSut` um die neuen optionalen Parameter erweitern. |
| `KiAusfuehrungsStatusConverterTests`, `KiAusfuehrungsStatusSymbolConverterTests` (+`_ZeitgesteuerterPrompt`) | Keine Anpassung nötig, aber **Regressionsschutz**: Der App-Adapter muss nach der Verschiebung identische Ergebnisse liefern — diese Tests müssen unverändert grün bleiben. |
| `CliProcessManagerTests_*` | Keine Anpassung nötig — berührt lediglich dasselbe `LaufdatenChanged`-Ereignis, das der Orchestrator nun zusätzlich abonniert (kein Verhaltensbruch am Manager). |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | **Konsolidierter Auto-Shutdown-Fluss** (eine Testmethode, ein App-Lifecycle): Aufgabe mit KiSimulator-CLI starten → Fußzeile zeigt „1 laufend" → Aktivierungs-Checkbox erscheint → aktivieren → Toleranzfeld + Force-Checkbox erscheinen → Toleranz „5" + Force setzen → CLI stoppen → Countdown-Text sichtbar → CLI erneut starten → Countdown verschwindet (Abbruch) → erneut stoppen → Countdown erneut → Checkbox deaktivieren → Countdown weg **und Checkbox bei Anzahl 0 ausgeblendet** (Sichtbarkeitsregel) → CLI erneut starten → Fußzeile zeigt wieder „1 laufend", Checkbox wird wieder sichtbar → reaktivieren → Toleranz auf „0" setzen → CLI stoppen → Countdown mit Toleranz 0 löst unmittelbar aus → `system-shutdowns.log` enthält Eintrag mit `erzwingen=true` → `RestartAppPreservingDatabase` → Force gesetzt + Toleranz „0" wiederhergestellt, Aktivierungs-Checkbox **aus**. | `src/Softwareschmiede.Tests/E2E/E2E_AutoShutdown.cs` — `AutoShutdown_CountdownAbbruchAufzeichnungUndPersistenz_E2E`, eingebunden als eigener `[SkippableFact]` in `MainTest.cs` | Akzeptanzszenarien 1–4 (Happy Path vom Checkbox-Auslöser bis zur aufgezeichneten Shutdown-Aufforderung, Abbruch bei Statuswechsel, Deaktivierung inkl. Ausblendung bei Anzahl 0, Persistenz-/Wiederherstellungs-Nachweis) | Der komplette Benutzerfluss (UI-Sichtbarkeiten → Event-Kette über echten Prozessstart/-ende → Countdown → Service-Aufruf → DB-Persistenz über Neustart) ist nur im laufenden System überprüfbar; Unit-Tests decken Einzelbausteine, nicht die Verdrahtung DI→Orchestrator→UI. Der Aufzeichnungs-Fake beweist zugleich, dass kein echtes OS-Shutdown erfolgt. |

Geplante E2E-Hilfsmittel: `WpfTestBase.ResolveSystemShutdownLogPfad()` + `WaitForSystemShutdownEintragAsync(substring, timeout)` (Polling-Muster `WaitForProzessStartEintragAsync`); `MenuView`-Zugriffsmethoden für die neuen AutomationIds (Anzahl-Text, Checkbox-Visibility/-Toggle, Toleranz-Setzen, Force-Toggle, Countdown-Text). Konsolidierungsregel beachtet: genau **eine** Testmethode deckt alle Aspekte an möglichst wenigen App-Instanzen ab (ein `LaunchApp` + ein `RestartAppPreservingDatabase`). Start und Stopp der KiSimulator-CLI erfolgen über den Prozesslebenszyklus (UI-Stop-Pfad bzw. Prozessende) — **ohne** ConPTY-Befehlseingabe, damit der Test in dieser Sandbox lauffähig bleibt (`SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` greift nur für die markierten ConPty-Tests).

Welche bestehenden E2E-Tests müssen angepasst werden? Keine — die neuen Fußzeilen-Elemente sind additiv; durch die Fake-Registrierung kann kein bestehender Test versehentlich ein OS-Shutdown auslösen (der Auto-Shutdown ist ohne Nutzeraktion ohnehin inert).

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Happy Path: Auto-Shutdown nach Abschluss | Nutzer aktiviert Checkbox | Mind. eine Automatisierung im Zustand `Laeuft`; keine persistierten Werte nötig | CLI läuft → Checkbox sichtbar → aktivieren → Toleranz (z. B. 5 Min.) + ggf. Force setzen → letzte laufende Automatisierung endet | Countdown-Text „Herunterfahren in mm:ss" erscheint und läuft herunter; bei 0 wird `ISystemShutdownService.RequestShutdownAsync(erzwingen)` aufgerufen (produktiv: OS-Shutdown; Testmodus: Eintrag in `system-shutdowns.log`) | E2E `AutoShutdown_..._E2E` (Aufzeichnungs-Eintrag statt echtem Shutdown) |
| 2 | Countdown-Abbruch durch neuen Lauf | Neue Automatisierung startet während Countdown | Countdown armiert (Anzahl war 0) | Statuswechsel 0→>0 (`RunningCountChanged`/`LaufdatenChanged`) → `EvaluiereAsync` | Countdown-Text verschwindet; kein Shutdown; fällt die Anzahl später wieder auf 0, startet ein neuer voller Countdown | Unit-Test `CountdownAbbruchBeiWiederanlauf` + E2E-Teilschritt (CLI-Neustart) |
| 3 | Deaktivierung während Countdown | Nutzer entfernt Häkchen während Countdown | Countdown armiert | `SetEnabled(false)` → Disarmierung | Countdown-Text verschwindet sofort; kein Shutdown auch nach Toleranzablauf; bei Anzahl 0 blendet sich der Auto-Shutdown-Block anschließend aus — eine erneute Aktivierung ist erst möglich, sobald wieder eine Automatisierung läuft (Sichtbarkeitsregel) | Unit-Test `DeaktivierungWaehrendCountdown` + E2E-Teilschritt |
| 4 | Persistenz über Neustart | Nutzer setzt Force + Toleranz, beendet App später | Einstellungen wurden geändert | App-Neustart → `AutoShutdownEinstellungenLadenAsync` | Force-Checkbox gesetzt und Toleranzfeld mit letztem Wert vorausgewählt; Aktivierungs-Checkbox aus (pro Sitzung neu) | E2E-Teilschritt nach `RestartAppPreservingDatabase` + VM-Test |
| 5 | Erststart ohne gespeicherte Werte | App-Start, frische DB | Keine `AppEinstellung`-Datensätze für `autoshutdown.*` | Laden liefert Defaults | Force aus, Toleranz „5"; es wird kein Datensatz vorbelegt | `AppEinstellungServiceTests` Defaults + VM-Test |
| 6 | Wartende Session blockiert nicht | Countdown-Bedingung geprüft | CLI-Prozess lebt, Substatus `WartetAufEingabe` (klassifiziert `Wartet`) | `EvaluiereAsync` → Anzahl 0 | Countdown startet trotz laufendem Prozess (Scope-Ausschluss 2); Statuswechsel zurück zu `Laeuft` bricht ab | `LaufendeAutomatisierungenZaehlungTests` + Orchestrator-Tests |
| 7 | Ungültige Toleranz-Eingabe | Nutzer tippt „abc" oder „-3" | Aktivierungs-Checkbox an | Eingabe → VM-Validierung | `AutoShutdownToleranzFehler`-Text sichtbar; weder Persistenz noch `SetOptionen`; vorheriger Wert bleibt wirksam | `MainWindowViewModelTests` (E2E optional innerhalb des konsolidierten Tests) |
| 8 | Wartezustand während Countdown (kein Verhungern) | Heartbeat-/Laufdaten-Ereignisse feuern periodisch | Countdown armiert, wartende Session lebt | `LaufdatenChanged` alle ~30 s → `EvaluiereAsync` → Anzahl weiter 0 | Countdown-Endzeitpunkt bleibt unverändert; Shutdown erfolgt zum ursprünglichen Zeitpunkt | Unit-Test `CountdownBleibtBeiUnveraenderterAnzahl` |

## Offene Punkte

Keine. Die acht offenen Fragen der Anforderung wurden durch Codebase-Recherche bzw. konservative Default-Entscheidungen geklärt:

1. „Laufend" = ausschließlich `KiAusfuehrungsZustand.Laeuft` (pausierte Aufgabe mit laufendem Prozess zählt **nicht** → Scope-Ausschluss 3).
2. Countdown startet bei fachlicher Anzahl 0 — wartende Prozesse blockieren nicht (→ Scope-Ausschluss 2).
3. Neuauswertung nach Abbruch: bei weiterhin 0 läuft der Countdown automatisch weiter bzw. neu — keine erneute manuelle Aktivierung nötig.
4. Alle zählungsrelevanten Ereignisse (`RunningCountChanged`, `LaufdatenChanged`, Sicherheitstick) lösen die Neuauswertung aus; unveränderte Anzahl setzt den Endzeitpunkt nicht zurück (Verhungern-Schutz, Designentscheidung).
5. Toleranz: Default 5 Minuten, Bereich 0–1440, nur ganze Minuten; Änderung während Countdown wirkt sofort (Re-Armiierung).
6. Force nur unter Windows (`/f`); Linux/macOS unverändert (→ Scope-Ausschluss 6).
7. Checkbox/Optionsbereich bleiben während des Countdowns sichtbar (`AutoShutdownUiSichtbar` = Anzahl>0 ∨ aktiviert ∨ Countdown); Deaktivieren bricht ab.
8. Vor OS-Shutdown nur Warn-Logeintrag, kein geordnetes App-Beenden (→ Scope-Ausschluss 5).

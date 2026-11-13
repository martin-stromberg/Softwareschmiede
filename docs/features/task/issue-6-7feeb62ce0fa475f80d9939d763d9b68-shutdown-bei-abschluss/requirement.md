# Übersetzte Anforderung: Shutdown bei Abschluss

**Quelle:** Issue #6 / Aufgaben-ID `7feeb62c-e0fa-475f-80d9-939d763d9b68`
**Branch:** `task/issue-6-7feeb62ce0fa475f80d9939d763d9b68-shutdown-bei-abschluss`

## Fachliche Zusammenfassung

Am unteren Rand der Seitenleiste („Menüleiste") des `MainWindow` soll die Anzahl aktuell laufender Automatisierungen angezeigt werden — wobei „laufend" der bereits in der Aufgabenliste implementierten Statusklassifikation folgt und eine Aufgabe im Zustand „Wartet" nicht als laufend zählt. Solange mindestens eine Automatisierung läuft, kann der Anwender per Checkbox einen automatischen System-Shutdown aktivieren; dabei erscheint ein Eingabefeld für eine Toleranzzeit in Minuten sowie eine zweite Checkbox „Herunterfahren erzwingen". Sobald keine CLI-Ausführung mehr aktiv ist, startet ein sichtbarer Countdown über die Toleranzzeit; jeder Statuswechsel einer Aufgabe während des Countdowns bricht ihn ab und löst eine Neuauswertung aus. Erreicht der Countdown 0, fährt das Betriebssystem herunter (optional erzwungen). Force-Flag und Toleranzzeit werden persistiert und beim nächsten Programmstart vorausgewählt.

Zum Ist-Stand: Aus der früheren Blazor-Version der Anwendung existieren bereits `AutoShutdownOrchestrator` (reagiert auf `RunningCountChanged`-Übergänge >0 → 0 und ruft `ISystemShutdownService.RequestShutdownAsync`), `SystemShutdownService` (Windows `shutdown /s /f /t 0`, Linux, macOS) und `IRunningAutomationStatusSource` (implementiert durch `KiAusfuehrungsService`, in `App.xaml.cs` registriert). Orchestrator und Shutdown-Service sind in der WPF-App jedoch **weder im DI-Container registriert noch an eine UI angebunden** — Toleranzzeit/Countdown, Abbruch bei Statuswechsel, Force-Schalter und Persistenz fehlen vollständig.

## Fachlicher Scope

- **Verarbeitungseinheit:** Die Menge der *laufenden Automatisierungen* — d. h. Aufgaben, deren KI-/CLI-Ausführung nach der Klassifikation der Aufgabenliste als „Läuft" gilt (`KiAusfuehrungsStatusKlassifikation` in `src/Softwareschmiede.App/Converters/AppConverters.cs` → `KiAusfuehrungsZustand.Laeuft`). Eine Aufgabe im Zustand `KiAusfuehrungsZustand.Wartet` (`AufgabeLaufStatus.WartetAufEingabe` oder `AufgabeStatus.Wartend`) gilt nicht als laufend. **Achtung:** Die bestehende Methode `IRunningAutomationStatusSource.GetRunningCount()` (`KiAusfuehrungsService`, Zeile ~111) zählt alle gehandhabten, nicht beendeten Prozesse — inklusive solcher im „Wartet"-Substatus — und weicht damit von der geforderten fachlichen Zählung ab.
- **Auslösende Ereignisse:**
  - Systemereignis „Anzahl laufender Automatisierungen ändert sich" (Quellen: `IRunningAutomationStatusSource.RunningCountChanged`, `AufgabeLaufdatenChangedNotifier.LaufdatenChanged`, Session-`RuntimeStatusChanged` für Läuft↔Wartet-Wechsel, `DispatcherTimer`-Zyklus des `MainWindowViewModel`).
  - Nutzeraktion „Checkbox Auto-Shutdown aktivieren/deaktivieren" (nur sichtbar, wenn laufende Automatisierungen existieren).
  - Nutzeraktion „Toleranzzeit eingeben" und „Force-Checkbox umschalten".
  - Systemereignis „Status mindestens einer Aufgabe wechselt während des Countdowns" → Countdown-Abbruch + Neuauswertung.
  - Systemereignis „Countdown erreicht 0" → OS-Shutdown via `ISystemShutdownService`.
- **Erwartete Wirkung:**
  - Sichtbar am unteren Rand der Seitenleiste: Anzahl laufender Automatisierungen; bei >0 die Aktivierungs-Checkbox; bei aktivierter Checkbox das Toleranzzeit-Eingabefeld und die Force-Checkbox; während der Nachlaufphase der verbleibende Countdown.
  - Nach Ablauf des Countdowns fährt das Host-Betriebssystem herunter (mit oder ohne Erzwingen).
  - Beim nächsten Programmstart sind Force-Checkbox und Toleranzzeit mit den zuletzt gesetzten Werten vorausgewählt.
- **Umgang mit fehlenden Objekten:**
  - *Keine persistierten Einstellungswerte* (Erststart): Standardwerte verwenden — Force aus, Toleranzzeit Standardwert (Höhe: offene Frage); kein Datensatz wird vorbelegt erzeugt, erst beim ersten Speichern (`AppEinstellung`-Key-Value-Entität wird bei Bedarf angelegt).
  - *Aktivierungs-Checkbox beim App-Start:* Wird bewusst **nicht** persistiert — die Anforderung nennt explizit nur die zweite Checkbox und die Toleranzzeit. Der Auto-Shutdown ist damit pro Sitzung erneut zu aktivieren (wiederverwenden der persistierten Nebenwerte, verwerfen des Aktivierungszustands).
  - *Countdown läuft und Statuswechsel tritt ein:* Countdown verwerfen, Stati neu auswerten (kein Wiederaufnehmen des alten Restwerts).
- **Scope-Ausschlüsse:**
  - Die erste Checkbox (Auto-Shutdown aktiviert) wird **nicht** persistiert — explizit fordert die Anforderung Persistenz nur für Force-Checkbox und Toleranzzeit.
  - Kein Abbruch-Dialog/Nachfrage unmittelbar vor dem OS-Shutdown (die Toleranzzeit ist die vorgesehene Abbruchmöglichkeit); ein `shutdown /a`-Abbrechen nach Auslösung ist nicht gefordert.
  - Keine Behandlung anderer laufender App-Aktivitäten (z. B. gerade laufendes Programmupdate, geöffnete Dialoge) beim Shutdown — nicht gefordert.
  - Kein Shutdown, wenn die App selbst beendet wird, bevor der Countdown abläuft.

## Betroffene Klassen und Komponenten

### Bestehende, voraussichtlich zu ändernde Artefakte

- `src/Softwareschmiede.App/Views/MainWindow.xaml` — Seitenleisten-Fußbereich (`StackPanel` in `Grid.Row="2"`, Zeilen ~170–228): Erweiterung um Anzeige der laufenden Automatisierungen, Aktivierungs-Checkbox, Toleranzzeit-Eingabefeld, Force-Checkbox und Countdown-Anzeige. Beachte: eingeklappte Navigation (`IsNavigationExpanded`/`BoolToVisibilityConverter`) — Verhalten bei 48-px-Breite festlegen (Annahme: ausblenden wie übrige Texte).
- `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs` — neue bindbare Eigenschaften (Running-Count, Checkbox-Zustände, Toleranzzeit, Countdown-Restzeit, Sichtbarkeiten) und Anbindung an den Orchestrator; oder Auslagerung in ein eigenes ViewModel/Control. Bestehende Aktualisierungspipeline (`OnRunningCountChanged` → `AktiveAufgabenAktualisierenAsync`, Zeilen ~452–470) liefert bereits die Datenbasis (`AktiveAufgabenListe` mit `LiveLaufStatus`, `LaufStatus`, `Status`, `PausiertBisUtc`).
- `src/Softwareschmiede/Application/Services/AutoShutdownOrchestrator.cs` und `IAutoShutdownOrchestrator.cs` — Erweiterung um: Toleranzzeit-Countdown (statt Sofort-Shutdown beim Übergang auf 0), Abbruch + Neuauswertung bei Statuswechsel beliebiger Aufgaben, Force-Flag-Weitergabe, sowie Oberfläche für Countdown-Zustand/-Fortschritt (z. B. Event oder abfragbarer Restwert für die UI, idealerweise über `TimeProvider` testbar — Muster: `PromptZeitVersandService` mit `ITimer`).
- `src/Softwareschmiede/Domain/Interfaces/ISystemShutdownService.cs` und `src/Softwareschmiede/Infrastructure/Services/SystemShutdownService.cs` — `RequestShutdownAsync` um einen Force-Parameter erweitern; Windows-Kommando aktuell fest `/s /f /t 0` (Force immer an) → Mapping ohne Force auf `shutdown /s /t 0`; Linux/macOS-Semantik von „erzwingen" klären (offene Frage).
- `src/Softwareschmiede/Domain/Interfaces/IRunningAutomationStatusSource.cs` / `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs` — `GetRunningCount()`/`GetLiveLaufStatus()`/`GetLaufendeAufgabenIds()` als Datenbasis; fachliche „läuft"-Zählung (ohne `WartetAufEingabe`) ggf. als neue Methode ergänzen oder im Verbraucher per `GetLiveLaufStatus` ableiten.
- `src/Softwareschmiede.App/Converters/AppConverters.cs` — `KiAusfuehrungsStatusKlassifikation` ist `internal` im App-Projekt; für eine fachliche Zählung im Application-Layer (Orchestrator) müsste die Klassifikation verschoben/geteilt oder die Zählung in der UI-Schicht (`MainWindowViewModel` über `AktiveAufgabenListe`) erfolgen — Entwurfsentscheidung.
- `src/Softwareschmiede.App/App.xaml.cs` — DI-Registrierung fehlt: `ISystemShutdownService` → `SystemShutdownService`, `AutoShutdownOrchestrator`/`IAutoShutdownOrchestrator` als Singleton sowie dessen Aktivierung (analog `AddHostedService`-Muster der Monitoring-Services oder explizite Initialisierung).
- `src/Softwareschmiede/Application/Services/AppEinstellungService.cs` — zwei neue Schlüssel-Konstanten plus typsichere Get/Set-Methoden (Muster: `ScmTaskCompletionWaitMinutesKey`/`ScmTaskCompletionDefaultWaitMinutes` inkl. `Math.Clamp`-Begrenzung): z. B. `autoshutdown.force` (bool) und `autoshutdown.toleranz.minuten` (int).
- `src/Softwareschmiede.Tests/Application/Services/AutoShutdownOrchestratorTests.cs` — bestehende Tests (Übergang >0→0, Deaktivierung, Unsubscribe) an die neue Countdown-Semantik anpassen/erweitern.

### Neu zu erstellende Artefakte (voraussichtlich, Namen sind Vorschläge)

- Ggf. eigenes UI-Element/ViewModel für den Seitenleisten-Block (z. B. `AutoShutdownPanelItem` oder Control analog `ActiveTasksListControl`), falls der `MainWindowViewModel` nicht direkt erweitert werden soll.
- Test-Fake/-Adapter für `ISystemShutdownService` im E2E-/Testmodus (Muster: `RecordingApplicationShutdownService` + `UpdateE2ETestKontext` in `src/Softwareschmiede.App/Services/Testing/`), damit Tests **keinen echten OS-Shutdown** auslösen.

### Tests

- `AutoShutdownOrchestratorTests`: Countdown-Start bei 0 laufenden Automatisierungen, Shutdown erst bei Countdown-Ende, Abbruch bei Statuswechsel + Neuauswertung, Deaktivierung während Countdown, Force-Flag-Durchreichung, doppelte 0-Übergänge.
- `SystemShutdownService`-Tests: Argument-Mapping mit/ohne Force (plattformabhängig, ggf. `Category=OsInterface` bzw. Kommando-Auflösung isoliert testen).
- `AppEinstellungService`-Tests: Lesen/Schreiben der beiden neuen Schlüssel, Defaults, Clamping.
- `MainWindowViewModel`-Tests: Sichtbarkeitslogik (Checkbox nur bei Running-Count > 0), Anzeige Countdown, Laden persistierter Werte beim Start.
- E2E: Absicherung, dass kein echter Shutdown ausgelöst wird (Fake-Service wie oben).

## Implementierungsansatz

- **Zählung „laufend":** Aus der bereits gemappten `AktiveAufgabenListe` bzw. der gemeinsamen Klassifikation `KiAusfuehrungsStatusKlassifikation.Klassifiziere` ableiten (Zustand `Laeuft` = laufend). Nicht `GetRunningCount()` wortwörtlich wiederverwenden — siehe Scope-Hinweis oben und offene Frage 1.
- **Countdown:** Im Orchestrator einen abbrechbaren Timer aufsetzen (`TimeProvider`-basiert, analog `PromptZeitVersandService`), der bei jedem Statuswechsel einer Aufgabe (Events: `CliProcessStatusChanged`, `AufgabeLaufdatenChangedNotifier.LaufdatenChanged`, ggf. `RuntimeStatusChanged`/erneute Auswertung des `IRunningAutomationStatusSource`) abgebrochen und die Stati neu ausgewertet wird. Countdown-Restwert für die UI bereitstellen (Event oder periodischer Tick via `DispatcherTimer`).
- **Persistenz:** `AppEinstellungService` (Key-Value-Tabelle `AppEinstellungen`, EF-Core) — kein neues Entity/Migration nötig.
- **Wiring:** `App.xaml.cs` — `SystemShutdownService` und Orchestrator registrieren; im Update-/E2E-Testmodus (Zweig um Zeile ~382–410) einen aufzeichnenden Fake registrieren, damit E2E-Tests das System nicht herunterfahren.
- **Abhängigkeiten:** `IRunningAutomationStatusSource` (`KiAusfuehrungsService`), `AufgabeLaufdatenChangedNotifier`, `AppEinstellungService` (Scoped-Zugriff aus Singleton ggf. via `IServiceScopeFactory`, Muster `PromptZeitVersandService`), `TimeProvider`, `IDialogService` (falls Rückfragen), `DispatcherInvokeFactory` für UI-Aktualisierung.

## Konfiguration

- `autoshutdown.toleranz.minuten` (int, Minuten): Toleranzzeit des Countdowns; persistiert via `AppEinstellungService`; Default-Wert und zulässiger Wertebereich zu klären (Vorschlag analog `GetScmTaskCompletionWaitMinutesAsync` mit `Math.Clamp`).
- `autoshutdown.force` (bool): Herunterfahren erzwingen; persistiert via `AppEinstellungService`; Default `false`.
- Nicht persistiert: Aktivierungszustand der ersten Checkbox (siehe Scope-Ausschlüsse).

## Offene Fragen

1. **Definition „laufend" vs. `GetRunningCount()`:** Zählt „laufend" nur Aufgaben im Klassifikations-Zustand `Laeuft` (d. h. `WartetAufEingabe`, `Wartend`, `Pausiert`, `PromptInWartestellung` zählen nicht), oder alle aktiven Aufgaben außer explizit „Wartet"? Insbesondere: Gilt eine pausierte Aufgabe mit noch laufendem CLI-Prozess als laufend?
2. **Countdown-Start trotz wartender Prozesse:** „Keine CLI-Ausführung mehr aktiv" — startet der Countdown auch dann, wenn noch physische CLI-Prozesse im Zustand „Wartet" laufen (die dann ggf. erzwungen beendet würden)? Oder erst bei wirklich null laufenden Prozessen (was ein ewig wartendes „Wartet" den Shutdown dauerhaft verhindern ließe)?
3. **Neuauswertung nach Countdown-Abbruch:** Wenn nach dem abgebrochenen Countdown weiterhin keine Automatisierung läuft — startet der Countdown automatisch neu, oder bleibt der Auto-Shutdown bis zur erneuten manuellen Aktivierung aus?
4. **Welche Statuswechsel lösen den Abbruch aus:** Jede Änderung (Start/Ende eines CLI-Prozesses, `AufgabeStatus`-Transition, `WartetAufEingabe`-Flattern der Session, Pausieren)? Ein Läuft↔Wartet-Flattern einer *laufenden* Aufgabe würde den Countdown sonst wiederholt abbrechen — ist das gewollt?
5. **Toleranzzeit:** Default-Wert, Minimal-/Maximalwert und Eingabevalidierung (nur ganze Minuten?)? Änderung während eines laufenden Countdowns — wirkt sie sofort (Countdown neu berechnen) oder erst beim nächsten Start?
6. **„Erzwingen" plattformübergreifend:** Windows: `shutdown /s /t 0` vs. `/s /f /t 0`. Was ist das gewünschte Verhalten unter Linux/macOS, wo kein direktes Force-Äquivalent existiert?
7. **Checkbox-Sichtbarkeit:** „Wenn es solche gibt" — verschwindet die Checkbox bei Countdown-Start (Running-Count = 0) komplett und nur der Countdown bleibt sichtbar? Kann der Anwender den aktivierten Auto-Shutdown während des Countdowns noch deaktivieren (Annahme: ja — Deaktivieren bricht den Countdown ab)?
8. **App-Verhalten beim OS-Shutdown:** Soll vor dem OS-Shutdown noch ein geordnetes App-Beenden/Protokolleintrag erfolgen (die CLI-Kindprozesse sterben mit dem System ohnehin)?

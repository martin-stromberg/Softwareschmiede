# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### UnteragentGovernanceMonitoringService.cs (UnteragentGovernanceMonitoringService)

- **Fehlerbehandlung / Fehlerisolation** — In `PruefeUnteragentAsync` (Zeilen 84–98) ist der `await db.SaveChangesAsync(ct)`-Aufruf innerhalb des `catch (UnteragentAbbruchException ex)`-Blocks nicht selbst gegen Fehler abgesichert. Der try-Block schützt nur den Aufruf von `governance.ValidiereFehlerBedingungAsync(...)`; wirft `SaveChangesAsync` (z. B. `DbUpdateConcurrencyException`, weil der Unteragent zwischen dem initialen `ToListAsync` in `RunOnceAsync` und diesem Aufruf bereits durch einen anderen Vorgang geändert/gelöscht wurde, oder ein transienter DB-Fehler), propagiert die Exception ungefangen aus `PruefeUnteragentAsync` heraus. Das bricht die `foreach`-Schleife in `RunOnceAsync` (Zeile 72–75) ab und überspringt damit die Governance-Prüfung für **alle** noch nicht geprüften aktiven Unteragenten desselben Durchlaufs — genau das Verhalten, das laut Klassenkommentar/Anforderung vermieden werden soll ("Fehler bei einem Unteragenten darf die Prüfung der übrigen im selben Durchlauf nicht abbrechen"). Der äußere Catch-All in `ExecuteAsync` fängt die Exception zwar ab und verhindert einen Absturz des BackgroundService, das ändert aber nichts daran, dass innerhalb des betroffenen Durchlaufs verbleibende aktive Unteragenten ungeprüft bleiben (erst im nächsten Durchlauf in ca. 1 Minute).

  Empfehlung: `db.SaveChangesAsync(ct)` (bzw. den gesamten Body von `PruefeUnteragentAsync`) in einen eigenen try/catch nehmen, der Fehler beim Persistieren loggt statt sie zu propagieren — analog zum bereits vorhandenen generischen `catch (Exception ex)`-Zweig für `ValidiereFehlerBedingungAsync`.

- **Fachliche Lücke in der Definition "aktiver Unteragent"** — Der Filter in `RunOnceAsync` (Zeilen 67–69) prüft nur `UnteragentSpezifikation.Status in {Erzeugt, Ausgefuehrt}` UND `AutonomAufgabe.Aufgabe.AusfuehrungsStatus == AufgabeAusfuehrungsStatus.AutonomAufgabe`. Er berücksichtigt nicht `Aufgabe.SessionPauseUtc` (siehe `src/Softwareschmiede/Application/Services/SessionManagementService.cs`, gesetzt u. a. über `AutonomAufgabeDetailViewModel.StoppeAgentAsync` → `PauseAufgabeBeiBudgetLimitAsync`, XML-Doc: "Stoppt (pausiert) den Projektleiter-Agenten."). Wird die übergeordnete Autonome Aufgabe vom Nutzer pausiert/gestoppt, bleibt `AusfuehrungsStatus` unverändert auf `AutonomAufgabe` — nur `SessionPauseUtc` wird gesetzt. Bereits laufende bzw. erzeugte Unteragenten (Status `Erzeugt`/`Ausgefuehrt`) gelten deshalb weiterhin als "aktiv" und werden vom neuen Monitor unverändert weitergeprüft. Da `UnteragentGovernanceService.ValidiereFehlerBedingungAsync` das Laufzeitlimit rein anhand der Wanduhrzeit (`DateTimeOffset.UtcNow - state.StartedUtc`) berechnet, läuft dieses Limit während einer bewussten Nutzer-Pause unbeeindruckt weiter — ein absichtlich pausierter Unteragent kann dadurch fälschlich als `Fehler` (Laufzeitlimit überschritten) markiert werden, obwohl keine echte Governance-Verletzung vorliegt.

  Empfehlung: Filter um eine Prüfung auf `!u.AutonomAufgabe.Aufgabe.SessionPauseUtc.HasValue` (bzw. äquivalent) ergänzen, oder – falls das Verhalten bewusst so gewollt ist – dies explizit im Klassen-/Methodenkommentar dokumentieren und mit einem Test absichern.

### UnteragentGovernanceMonitoringServiceTests.cs (UnteragentGovernanceMonitoringServiceTests)

- **Testqualität / fehlende Abdeckung** — Keiner der drei Tests deckt den zweiten Teil der "aktiver Unteragent"-Definition ab (`Aufgabe.AusfuehrungsStatus == AufgabeAusfuehrungsStatus.AutonomAufgabe`). Alle drei Tests nutzen `ProjektleiterAgentServiceTestDatenFactory.ErstelleAufgabeUndKonfiguration`, das `AusfuehrungsStatus` fest auf `AutonomAufgabe` setzt, und variieren nur `UnteragentSpezifikation.Status`. Der dritte Test (`RunOnceAsync_TutNichts_OhneAktivenUnteragenten`) verifiziert ausschließlich die Status-Halbseite des Filters (`Abgeschlossen`). Würde die `AusfuehrungsStatus`-Bedingung im Produktivcode versehentlich entfernt, invertiert oder falsch verknüpft (z. B. `OR` statt `AND`), würde keiner der drei Tests dies bemerken.

  Empfehlung: Einen zusätzlichen Testfall ergänzen, der einen Unteragenten mit Status `Ausgefuehrt`/`Erzeugt` anlegt, dessen zugehörige Aufgabe aber `AusfuehrungsStatus` ungleich `AutonomAufgabe` hat (z. B. `Beendet`), und der ein überschrittenes Tokenlimit in `task_state.json` hinterlegt — Erwartung: `ValidiereFehlerBedingungAsync` wird nicht aufgerufen, Status bleibt unverändert.

### UnteragentGovernanceMonitoringService.cs / PullRequestMonitoringService.cs (Doppelter Code)

- **Doppelter Code** — `UnteragentGovernanceMonitoringService.ExecuteAsync` (Zeilen 35–54) ist bis auf die Log-Meldung eine wortgleiche Kopie von `PullRequestMonitoringService.ExecuteAsync` (While-Schleife mit try/catch für `OperationCanceledException`/`Exception` und `Task.Delay(PollingInterval, _timeProvider, stoppingToken)`). Damit existiert dieses Polling-Skelett jetzt zweimal identisch im Code; ein drittes ähnliches BackgroundService würde die Duplikation weiter erhöhen.

  Empfehlung: Gemeinsames Polling-Skelett (While-Schleife + Fehlerbehandlung + `Task.Delay` via `TimeProvider`) in eine wiederverwendbare abstrakte Basisklasse (z. B. `PollingBackgroundServiceBase`) extrahieren, die `RunOnceAsync` als abstrakte Methode vorgibt; beide Services darauf umstellen. Kein Blocker für diesen Change, aber bei der nächsten Gelegenheit sinnvoll.

## Geprüfte Dateien

- `src/Softwareschmiede/Application/Services/UnteragentGovernanceMonitoringService.cs`
- `src/Softwareschmiede.App/App.xaml.cs` (nur die DI-Registrierung für `UnteragentGovernanceMonitoringService`)
- `src/Softwareschmiede.Tests/Application/Services/UnteragentGovernanceMonitoringServiceTests.cs`

Ergänzend zum Kontext gelesen (keine Änderungen, nur zur Einordnung der Befunde): `UnteragentGovernanceService.cs`, `PullRequestMonitoringService.cs`, `UnteragentAbbruchException.cs`, `UnteragentSpezifikation.cs`, `AutonomAufgabeKonfiguration.cs`, `AufgabeAusfuehrungsStatus.cs`, `UnteragentStatus.cs`, `SessionManagementService.cs`, `AutonomAufgabeDetailViewModel.cs`, `ProjektleiterAgentServiceTestDatenFactory.cs`, `TestDbContextFactory.cs`.

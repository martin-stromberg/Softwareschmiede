# Umsetzungsplan: Einheitlicher Pass-Einstieg für „Aufgabe abschließen"

## Übersicht

`EntwicklungsprozessService.AbschliessenAsync` wird zum einzigen produktiven
Pass-Einstieg für den Geschäftsvorgang „Aufgabe abschließen": unter der
`ScmTaskCoordinator`-Aufgabensperre fährt er die vollständige Sequenz
CLI-Stopp → Prompt-Abbruch → To-do-Prüfung → Klon-Löschung → Status `Beendet` →
Protokoll. UI- und Monitoring-Aufrufer werden auf den gemeinsamen Einstieg
umgestellt; `ProzessStartenAsync` wird als testgenutzter Einzelschritt auf
`internal` eingeschränkt.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Pass-Einstieg | Bestehende Methode `AbschliessenAsync` auf `EntwicklungsprozessService` zum koordinierten Pass erweitern (`RunAsync` + private `AbschliessenCoordinatedAsync`) statt neuen Lebenszyklus-Service | Issue nennt beide Varianten („z. B."); das bestehende Muster `ProzessStarten*`/`CliNeustarten*` im selben Service ist identisch und minimiert die API-Fläche |
| Methodenname | `AbschliessenAsync` beibehalten | Konsistent mit der bestehenden Benennung (`CliNeustartenAsync`, `CommitDurchfuehrenAsync`); `AufgabeAbschliessenAsync` war im Issue nur ein Beispiel |
| Schrittfolge | `StopCliAsync` → `CancelScheduledPrompt` → To-do-Check → Klon-Löschung → Status → Protokoll | Exakte Sequenz aus dem Issue-Vorschlag; der CLI-Stopp vor der To-do-Prüfung entspricht dem bisherigen Monitoring-Verhalten, die UI-Vorprüfung verhindert den Pass-Aufruf im Regelfall ohnehin |
| Optionale Dienste | `StopCliAsync` nur bei konfiguriertem `KiAusfuehrungsService`, `CancelScheduledPrompt` nur bei konfiguriertem `PromptZeitVersandService` | Options sind bewusst optional; Tests ohne CLI-/Prompt-Infrastruktur laufen unverändert weiter |
| Monitoring-Aufrufer | Äußere `RunAsync`-Sperre in `CheckTaskAsync` bleibt (Revalidierung muss atomar mit dem Abschluss unter der Sperre bleiben); innerer Aufruf wird zum einzelnen `AbschliessenAsync` | Reentrancy über `AsyncLocal`-Besitzer: der Pass läuft in bereits gehaltener Sperre direkt durch; die Revalidierungs-Checks (ErkanntUtc, Wartezeit, offene Ereignisse) gehören nicht in den allgemeinen Pass |
| `ScmE2ERuntimeControl` | **Nicht** auf den Pass-Einstieg umstellen | Die Klasse vollzieht einen CLI-Sitzungs-Ersatz (Sperre → `StopCliAsync` → `StartTerminalSessionAsync`), keinen Aufgaben-Abschluss — es gibt keine gemeinsame Sequenz; die Aufgabensperre nutzt sie bereits. Bewusste Abweichung vom Issue-Wortlaut, im PR-Bericht transparent gemacht |
| `ProzessStartenAsync` | Sichtbarkeit `public` → `internal`; `InternalsVisibleTo("Softwareschmiede.IntegrationTests")` ergänzen | Issue nennt `internal` ausdrücklich als Option; Umstellung der Integrationstests auf den produktiven Einstieg wäre ein unverhältnismäßiger Fixture-Umbau (benötigt `KiAusfuehrungsService`/`ITerminalSessionFactory`) |

## Programmabläufe

### Einheitlicher Abschluss-Pass

1. `AbschliessenAsync(aufgabeId, ct)` delegiert an
   `ScmTaskCoordinator.Shared.RunAsync(aufgabeId, () => AbschliessenCoordinatedAsync(aufgabeId, ct), ct)`.
2. `AbschliessenCoordinatedAsync`:
   a. `_options.KiAusfuehrungsService?.StopCliAsync(aufgabeId, ct)` — laufenden
      CLI-Prozess beenden (falls Dienst konfiguriert).
   b. `_options.PromptZeitVersandService?.CancelScheduledPrompt(aufgabeId)` —
      geplanten zeitgesteuerten Prompt zurückziehen (falls Dienst konfiguriert).
   c. `_aufgabeService.GetDetailAsync` — `InvalidOperationException`, wenn Aufgabe fehlt.
   d. `CanCompleteTaskAsync` — bei offenen To-dos `InvalidOperationException`
      (`OffeneTodosFehlermeldungFormat`); Status quo.
   e. `DeleteDirectoryForce(LokalerKlonPfad)`, falls gesetzt/existent.
   f. `_aufgabeService.AbschliessenAsync` (Status `Beendet`, Lauf-Felder räumen)
      + `_protokollService.AddStatusUebergangAsync`.

Beteiligte Klassen: `EntwicklungsprozessService`, `ScmTaskCoordinator`,
`KiAusfuehrungsService`, `PromptZeitVersandService`, `AufgabeService`,
`ProtokollService`.

### UI-Abschluss

`TaskDetailViewModel.AufgabeAbschliessenAsync`: `CanCompleteTaskAsync`-Vorprüfung
mit To-do-Fehlermeldung bleibt (Anzeigelogik); danach direkt
`_entwicklungsprozessService.AbschliessenAsync` → `LadenAsync`. Der eigene
`CancelScheduledPrompt`-Aufruf entfällt (jetzt im Pass enthalten).

### Automatischer Abschluss (Monitoring)

`ScmTaskCompletionMonitoringService.CheckTaskAsync`: die
`RunAsync`-Revalidierung bleibt; der `try`-Block ruft nur noch
`EntwicklungsprozessService.AbschliessenAsync` + Protokolleintrag auf — kein
eigener `KiAusfuehrungsService`-Aufruf mehr.

## Neue Klassen

Keine.

## Änderungen an bestehenden Klassen

### `EntwicklungsprozessServiceOptions` (record)

- **Neue Eigenschaften:** `PromptZeitVersandService` (`PromptZeitVersandService?`, Default `null`) — optionaler Singleton zum Zurückziehen geplanter Prompts. Als letzter positionaler Parameter, damit bestehende benannte Argumente unverändert kompilieren.

### `EntwicklungsprozessService`

- **Geänderte Methoden:** `AbschliessenAsync` — wird zum koordinierten
  Pass-Einstieg; bisheriger Rumpf wandert in `AbschliessenCoordinatedAsync`
  und wird um CLI-Stopp und Prompt-Abbruch ergänzt.
- **Geänderte Methoden:** `ProzessStartenAsync` — Sichtbarkeit `public` → `internal`.
- **Neue Methoden:** `AbschliessenCoordinatedAsync` (privat) — vollständige
  Abschluss-Sequenz unter der Aufgabensperre.

### `ScmTaskCompletionMonitoringService`

- **Geänderte Methoden:** `CheckTaskAsync` — ersetzt
  `GetRequiredService<KiAusfuehrungsService>().StopCliAsync` +
  `AbschliessenAsync` durch den einzelnen Pass-Aufruf; Revalidierung und
  Fehlerfänger unverändert.

### `TaskDetailViewModel`

- **Geänderte Methoden:** `AufgabeAbschliessenAsync` — entfernt
  `_promptZeitVersandService.CancelScheduledPrompt`; restliche Anzeigelogik
  (To-do-Meldung, `LadenAsync`, `SetFehler`) unverändert.

### `App.xaml.cs`

- **Geänderte Registrierung:** `EntwicklungsprozessService`-Factory — Options
  um `PromptZeitVersandService: sp.GetRequiredService<PromptZeitVersandService>()`
  ergänzt.

### `AssemblyInfo.cs` (`src/Softwareschmiede/Properties`)

- **Ergänzt:** `[assembly: InternalsVisibleTo("Softwareschmiede.IntegrationTests")]`.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine neuen — die bestehende To-do-Validierung (`CanCompleteTaskAsync` +
`OffeneTodosFehlermeldungFormat`) bleibt Bestandteil des Passes.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Monitoring-Pfad:** Bei blockiertem Abschluss (offene To-dos) werden CLI-Stopp
  und Prompt-Abbruch bereits ausgeführt, bevor die To-do-Prüfung scheitert —
  entspricht dem Issue-Vorschlag und dem bisherigen Monitoring-Verhalten
  (CLI-Stopp lief dort bereits vor dem Abschluss); neu ist nur der Prompt-Abbruch
  in diesem Fall.
- **UI-Pfad:** `CancelScheduledPrompt` läuft jetzt innerhalb der Sperre statt vor
  dem Service-Aufruf — Gleichwertigkeit gegeben, da der Pass bei blockiertem
  Abschluss durch die VM-Vorprüfung gar nicht erreicht wird.
- **E2E-Laufzeitkontrolle:** bleibt bewusst unverändert (siehe Designentscheidung).
- **Öffentliche API:** `ProzessStartenAsync` wird `internal` — kein produktiver
  Aufrufer betroffen; Integrationstests erhalten `InternalsVisibleTo`.

## Umsetzungsreihenfolge

1. **`EntwicklungsprozessServiceOptions` erweitern**
   - Voraussetzungen: `PromptZeitVersandService` existiert (Singleton, `App.xaml.cs`).
   - Beschreibung: Optionaler Parameter `PromptZeitVersandService` am Ende der
     Parameterliste ergänzen; XML-Dokumentation am Record anpassen.

2. **`AbschliessenAsync` zum Pass-Einstieg ausbauen**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: `AbschliessenAsync` delegiert auf `RunAsync` +
     `AbschliessenCoordinatedAsync`; Sequenz gemäß Programmablauf; XML-Doku
     um CLI-Stopp/Prompt-Abbruch/Sperre ergänzen.

3. **`ProzessStartenAsync` auf `internal` einschränken + `InternalsVisibleTo`**
   - Voraussetzungen: Keine.
   - Beschreibung: Sichtbarkeit ändern;
     `InternalsVisibleTo("Softwareschmiede.IntegrationTests")` in
     `AssemblyInfo.cs` ergänzen.

4. **Aufrufer umstellen**
   - Voraussetzungen: Schritt 2.
   - Beschreibung: `TaskDetailViewModel.AufgabeAbschliessenAsync` (eigenes
     `CancelScheduledPrompt` entfernen); `ScmTaskCompletionMonitoringService.CheckTaskAsync`
     (nur noch Pass-Aufruf); `App.xaml.cs`-Factory um
     `PromptZeitVersandService`-Auflösung ergänzen.

5. **Test-Infrastruktur ergänzen**
   - Voraussetzungen: Schritte 1–4.
   - Beschreibung: `TaskDetailViewModelTestFactory` und `ScmDispatchFixture`
     übergeben `PromptZeitVersandService` in die Options (Fixture registriert
     den Dienst im `ServiceCollection`).

6. **Neue Tests schreiben**
   - Voraussetzungen: Schritte 1–5.
   - Beschreibung: Prompt-Abbruch- und CLI-Stopp-Nachweis im Pass
     (siehe Tests-Abschnitt).

7. **Dokumentation aktualisieren**
   - Voraussetzungen: Schritte 1–6.
   - Beschreibung: `docs/help/aufgaben/ablauf-technisch.md` (Abschluss-Abschnitte,
     `CancelScheduledPrompt`-Aufruferliste), `docs/help/aufgaben/business-rules.md`
     (To-do-Regel: Pass statt direkter Service-Aufruf),
     `docs/help/plugins/github-ereignisse/ablauf-technisch.md` +
     `business-rules.md` (Monitoring: einheitlicher Pass statt eigener Komposition).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `AbschliessenAsync_CancelsScheduledPrompt_WhenAufgabeExists` | `EntwicklungsprozessServiceTests` | Über `PromptZeitVersandService` geplanter Prompt ist nach dem Pass nicht mehr geplant (`GetScheduledPromptStatus` → `null`); `TimeProvider`-gesteuerte Planung |
| `AbschliessenAsync_StopsRunningCli_WhenSessionActive` | `EntwicklungsprozessServiceTests` | Nach `StartTerminalSessionAsync` (Test-Launcher) meldet `IsRunning`/`GetLastExitCode`-Pfad nach dem Pass den Stopp — Nachweis, dass der Pass `StopCliAsync` fährt |
| `Monitor_MergedPullRequest_CancelsScheduledPromptOnComplete` | `ScmTaskCompletionMonitoringServiceTests` | Über den Fixture-`PromptZeitVersandService` geplanter Prompt ist nach dem Monitoring-Abschluss nicht mehr geplant (Prompt-Abbruch jetzt auch im Monitoring-Pfad) |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `TaskDetailViewModelTestFactory`, `ScmDispatchFixture` | Options um `PromptZeitVersandService` ergänzt (Fixture: zusätzliche DI-Registrierung) |
| Bestehende `AbschliessenAsync`-/`AufgabeAbschliessenCommand`-Tests | Unverändert bestehen — Verhalten ist additiv; Service ohne Options-Dienste verhält sich wie bisher |

### E2E-Tests (Pflicht)

Keine neue Benutzerinteraktion — der Abschluss-Button und die automatische
Abschlussüberwachung existieren unverändert; die Änderung betrifft die
interne Komposition. Die bestehende E2E-Abdeckung (`E2E_ScmEvents`,
Monitoring-Integrationstests mit echten Prozessen) bleibt unberührt.

## Offene Punkte

Keine.

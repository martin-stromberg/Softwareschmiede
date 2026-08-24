# Anforderung: Autonome Aufgabe – Echte CLI-Ausführung und UI-Integration

## Fachliche Zusammenfassung

Autonome Aufgaben sollen echte CLI-Prozesse starten und verwalten, statt nur DB-Status zu aktualisieren. Die UI wird aufgeräumt (Entfernung doppelter Bedienung), die Ribbon-Steuerung wird auf Autonome Aufgaben begrenzt, und der Lifecycle wird über App-Neustarts persistiert: Aufgaben, die zuvor gestartet wurden, sollen automatisch mit Session-Resume fortgesetzt werden; explizit gestoppte Aufgaben nicht. Dies erfordert echte CLI-Integration analog zu regulären Aufgaben, ein neues Persistenz-Flag für explizites Stoppen, und automatischen Wiederstart beim App-Start.

## Betroffene Klassen und Komponenten

### Datenmodellklassen (Erweiterungen)

- **`AutonomAufgabeKonfiguration`** (erweitern):
  - Neues Feld: `ExplizitGestoppt` (bool, default: false) — kennzeichnet, ob die CLI explizit vom Nutzer gestoppt wurde oder nur bei Budget/Timeout pausiert
  - Oder: Neues Feld: `LetzterStartStatusUtc` (DateTimeOffset?, null wenn nicht gestartet oder explizit gestoppt) — als Alternative zum boolesch Flag, falls differenziertere Historie nötig ist

### Logikklassen / Services

- **`ProjektleiterAgentService`** (Erweiterung/Umgestaltung):
  - `StarteAgentAsync(...)` — Aktuell reine DB-Buchhaltung; muss erweitert werden um:
    - Aufruf von `KiAusfuehrungsService.StartWithPseudoConsoleAsync(...)` mit dem Initialprompt (`AutonomAufgabeKonfiguration.InitialPrompt`)
    - Übergabe von `optionalParameters` (null beim Erststart, Resume-Prompt beim Neustart nach App-Neustart)
    - Rückgabe der echten CLI-Process-ID (nicht nur `agentId`-String)
  - Neue Methode: `StarteAgenNachAppNeustartAsync(aufgabeId)` — Automatischer Start beim App-Start, falls noch nicht explizit gestoppt
    - Prüfung: `AutonomAufgabeKonfiguration.ExplizitGestoppt == false` UND `AusfuehrungsStatus == Aktiv` (oder Wartend bei Session-Pause)
    - Aufruf von `StarteAgentAsync(...)` mit Resume-Prompt statt Initialprompt

- **`CliStoppService`** oder Erweiterung in `ProjektleiterAgentService`:
  - Neue Methode: `StoppeAgenExplizitAsync(aufgabeId)` — Setzt `AutonomAufgabeKonfiguration.ExplizitGestoppt = true` + ruft `KiAusfuehrungsService.StopCliAsync(...)` auf

- **`KiAusfuehrungsService`** (optional, prüfung auf Kompatibilität):
  - Prüfung, ob `StartWithPseudoConsoleAsync(...)` die `optionalParameters` (Resume-Prompt) korrekt an die CLI durchreicht
  - Prüfung: Welche Plugin unterstützen `SupportsSessionContinuation()` bereits? (für Resume-Semantik)

- **Neuer Service (optional)**: `AppStartupAutonomAufgabenRecoveryService`
  - Beim App-Start: `RecoveriereAutonomAufgabenNachNeustart()`
    - Abfrage aller `AutonomAufgabeKonfiguration` mit `ExplizitGestoppt == false` und `AusfuehrungsStatus == Aktiv` oder `Wartend`
    - Für jede: Aufruf von `ProjektleiterAgentService.StarteAgenNachAppNeustartAsync(aufgabeId)`

### UI / ViewModel / Controller

- **`AutonomAufgabeDetailViewModel`** (Erweiterung):
  - Bestehendes Command `StartCommand` — Aktuell bindet es `ProjektleiterAgentService.StarteAgentAsync()` auf reine DB-Operationen; keine UI-Reaktion auf echte CLI
  - Neue Property: `CliIsRunning` (bool, bindbar) — Wird True, wenn `KiAusfuehrungsService.IsRunning(aufgabeId)` = true
  - Neues Command: `StopCommand` (gebunden zum Ribbon-Button „Beenden" in Gruppe „Autonome Aufgabe") — Ruft `ProjektleiterAgentService.StoppeAgenExplizitAsync(aufgabeId)` auf
  - Neues Command: `ResumeCommand` (ggf. gebunden zum Button „Fortsetzen") — Ruft `SessionManagementService.SetzeFortAsync(aufgabeId)` auf (bereits im Plan vorhanden)
  - Event-Listener: Abonniert `KiAusfuehrungsService.CliProcessStatusChanged` um `CliIsRunning` zu aktualisieren

- **`AutonomAufgabeDetailView.xaml`** (Vereinfachung):
  - **Entfernen**: Die Buttons `StartButton`, `StopButton`, `ResumeButton` im Inhaltsbereich (aktuell Zeilen 23–38, falls vorhanden)
  - Grund: Doppelte Bedienung; alle Steuerung soll über das Ribbon erfolgen

- **`TaskDetailView.xaml`** und `TaskDetailViewModel` (Erweiterung):
  - **Neue Visibility-Binding**: `Aufgabe.AutonomKonfiguration != null` → Ribbon-Buttons „Start" (Gruppe „Aufgabe") und „Beenden" (`Visibility = Collapsed`)
  - Alternativ: Property `CanStartOrStopViaRegularButtons` (bool) im ViewModel, berechnet aus `Aufgabe.AutonomKonfiguration == null`
  - Ribbon-Buttons „Start" und „Beenden" (Gruppe „Aufgabe") sollen ausgeblendet werden, wenn `AutonomKonfiguration != null`
  - **Zu prüfen**: Betrifft auch „CLI starten" / „Stoppen" in der Gruppe „Ausführung" (Zeilen ~80–89 der current TaskDetailView.xaml)? Anforderung nennt explizit nur „Start" und „Beenden", aber Konsistenz zu prüfen.

### Tests

- Neue Unittest-Klasse: `ProjektleiterAgentServiceTests`
  - Test: `StarteAgentAsync_CallsKiAusfuehrungsService_WithInitialPrompt()`
  - Test: `StarteAgenNachAppNeustartAsync_WennNichtExplizitGestoppt_StartetNeuMitResumePrompt()`
  - Test: `StarteAgenNachAppNeustartAsync_WennExplizitGestoppt_StartetNicht()`
  - Test: `StoppeAgenExplizitAsync_SetzExplizitGestoppt()`

- E2E-Test (optional): `AutonomAufgabenUITests`
  - Test: Ribbon-Button „Start" startet echte CLI (überprüfen: `KiAusfuehrungsService.IsRunning()` wird true)
  - Test: Nach App-Neustart wird pausierte Aufgabe automatisch fortgesetzt (prüfen auf `KiAusfuehrungsService.IsRunning()` nach App-Restart)
  - Test: Nach explizitem „Beenden" wird Aufgabe nicht mehr automatisch gestartet

## Implementierungsansatz

### (1) Neue Persistenz-Felter für „explizit gestoppt"

- Datenbank-Migration: Füge `AutonomAufgabeKonfiguration.ExplizitGestoppt` (bool, default: false) hinzu
- Oder: Alternative: `LastExplicitStopUtc` (DateTimeOffset?, null) — gibt Zeitpunkt des letzten expliziten Stops an

### (2) Integration mit `KiAusfuehrungsService`

- `ProjektleiterAgentService.StarteAgentAsync()` muss den echten CLI-Start durchführen:
  ```csharp
  await _kiAusfuehrungsService.StartWithPseudoConsoleAsync(
      kiPlugin: /* Konfiguration aus Aufgabe.KiPluginPrefix */,
      localRepoPath: configuration.ArbeitsverzeichnisPfad,
      optionalParameters: /* InitialPrompt bei Erststart, Resume-Prompt bei Neustart */
  );
  ```
- `ProjektleiterAgentService.StoppeAgenExplizitAsync()`:
  ```csharp
  configuration.ExplizitGestoppt = true;
  await _kiAusfuehrungsService.StopCliAsync(aufgabeId);
  await _db.SaveChangesAsync();
  ```

### (3) Resume-Semantik beim App-Start

- Neuer Service `AppStartupAutonomAufgabenRecoveryService`:
  ```csharp
  public async Task RecoveriereAutonomAufgabenNachNeustart(CancellationToken ct)
  {
      var aufgabenZuStarten = await _db.AutonomAufgabeKonfigurationen
          .Where(k => !k.ExplizitGestoppt && k.Aufgabe.AusfuehrungsStatus == AufgabeAusfuehrungsStatus.Aktiv)
          .ToListAsync(ct);
      
      foreach (var konfiguration in aufgabenZuStarten)
      {
          var resumePrompt = GenerieResumePrompt(konfiguration);
          await _projektleiterAgentService.StarteAgentAsync(konfiguration, resumePrompt);
      }
  }
  ```
- Aufruf dieses Services in `App.xaml.cs` (OnStartup) oder im Initialisierungscode der Hauptanwendung, nach dem DbContext-Startup

### (4) UI-Aufräumen

- Entfernung der Button-Gruppe aus `AutonomAufgabeDetailView.xaml` (Zeilen 23–38)
- Ribbon-Integration sicherstellen: Buttons „Start", „Stop", „Resume" sind bereits im Ribbon vorhanden (Gruppe „Autonome Aufgabe")

### (5) Sichtbarkeits-Binding für reguläre Aufgaben-Buttons

- `TaskDetailView.xaml`: Ribbon-Buttons „Start" und „Beenden" nur sichtbar, wenn `Aufgabe.AutonomKonfiguration == null`
  ```xaml
  Visibility="{Binding IsAutonomAufgabe, Converter={StaticResource BooleanToVisibilityConverter}, ConverterParameter=Inverted}"
  ```
- `TaskDetailViewModel`: Property `IsAutonomAufgabe` (bool)
  ```csharp
  public bool IsAutonomAufgabe => AufgabenDetails?.AutonomKonfiguration != null;
  ```

### (6) Plugin-Auflösung und Session-Continuation

- Prüfung: `IKiPlugin.SupportsSessionContinuation()` — Welche Plugins unterstützen es bereits?
- Wenn Plugin Resume **nicht** unterstützt: Resume-Prompt wird trotzdem gesendet (als normaler Prompt), ohne Session-Context fortzusetzen
- Wenn Plugin Resume **unterstützt**: Resume-Parameter (z. B. `--resume` Flag) wird an `KiAusfuehrungsService.StartWithPseudoConsoleAsync()` übergeben

### (7) Fehlerbehandlung

- `StarteAgentAsync()` bei `KiAusfuehrungsService.StartWithPseudoConsoleAsync()` wirft Exception: fangen, loggen, `AusfuehrungsStatus` auf `Beendet` setzen
- `StoppeAgenExplizitAsync()` bei `KiAusfuehrungsService.StopCliAsync()` wirft Exception: loggen, aber nicht werfen (Best-Effort-Stop)
- App-Startup-Recovery bei Exception: loggen, aber nicht werfen (einzelne fehlgeschlagene Aufgaben behindern nicht den App-Start)

## Konfiguration

### Datenbank-Migrations

- **Migration 1**: `AddExplizitGestopptToAutonomAufgabeKonfiguration`
  - Spalte: `ExplizitGestoppt` (bool, default: false, NOT NULL)

### Feature-Flag (optional)

- `AutonomAufgaben.EnableAutoResume` (appsettings.json, default: true) — ermöglicht Deaktivierung der App-Startup-Recovery für Debugging

### Ribbon-Konfiguration (UI)

- Keine neue Konfiguration nötig; bestehende Ribbon-Buttons werden einfach sichtbar/unsichtbar geschaltet per Binding

## Offene Fragen

1. **Plugin-Support für Session-Continuation**: Welche Plugins (z. B. `ClaudeCliPlugin`, `DevinPlugin`) unterstützen bereits `SupportsSessionContinuation()`? Müssen diese erweitert werden?

2. **Resume-Prompt-Formulierung**: Wer formuliert den „Mach bitte weiter"-Prompt? Soll er dynamisch aus `plan.md` und `progress.md` generiert werden oder ist ein statischer Standard-Prompt ausreichend?

3. **Heartbeat-Timeout bei App-Neustart**: Wenn die App für längere Zeit beendet ist und die Aufgabe danach neugestartet wird, sollte `LastHeartbeatUtc` zurückgesetzt werden oder kann es in der Vergangenheit bleiben?

4. **Korrektur einer Falschannahme aus dem ersten Übersetzungs-Durchlauf**: Es existiert in diesem Projekt **kein** `IAgentRuntime`-Interface oder vergleichbares Agent-Runtime-Abstraktions-Pattern (verifiziert per Codesuche — keine Treffer). `KiAusfuehrungsService` ist der einzige vorhandene Mechanismus zum Starten/Verwalten von CLI-Prozessen und sollte direkt für den Projektleiter-Agent-Start verwendet werden, analog zu `EntwicklungsprozessService`.

5. **„CLI starten" / „Stoppen" in Gruppe „Ausführung"**: Anforderung nennt explizit nur „Start" und „Beenden" in Gruppe „Aufgabe". Sollte auch die Gruppe „Ausführung" (Zeilen ~80–89 in TaskDetailView.xaml) ausgeblendet werden, wenn eine Aufgabe autonom ist?

6. **Explizites Stoppen vs. Session-Pause**: Wenn Budget-Limit erreicht wird, setzt `SessionManagementService.PauseAufgabeBeiBudgetLimitAsync()` `AusfuehrungsStatus = Wartend`. Sollte `ExplizitGestoppt` beim Budget-Limit auch auf true gesetzt werden, oder nur beim manuellen „Beenden"-Button?

7. **Schnittstelle zwischen `ProjektleiterAgentService` und `KiAusfuehrungsService`**: 
   - Unterschied zwischen `optionalParameters` (Resume-Prompt) vs. `InitialPrompt`?
   - Wird der Prompt als `optionalParameters` oder als neuer Aufruf-Parameter an `StartWithPseudoConsoleAsync()` übergeben?

8. **Datenbank-Feld-Alternativenermittlung**: Sollte `ExplizitGestoppt` ein bool sein oder eine DateTime (`LastExplicitStopUtc`)? Ersterer ist einfacher, letzterer gibt mehr Audit-Trail.

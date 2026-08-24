# Continue: Autonome Aufgabe – echte CLI-Ausführung statt reiner Buchhaltung

## Kontext / Root Cause (durch direkte Code-Analyse verifiziert, 2026-08-24)

Der Ribbon-Button **„Start"** in der Gruppe **„Autonome Aufgabe"** (`AutonomAufgabeDetailViewModel.StartCommand`) ruft
`ProjektleiterAgentService.StarteAgentAsync(...)` auf
(`src/Softwareschmiede/Application/Services/ProjektleiterAgentService.cs:33-62`).

Diese Methode tut **ausschließlich**:
1. Schreibt `skills/skill_projektleiter_v1.md`, falls nicht vorhanden.
2. Lädt `Aufgabe`/`AutonomAufgabeKonfiguration` aus der DB.
3. Erzeugt eine `agentId`-Zeichenkette (`projektleiter-{guid}`).
4. Setzt in der DB `AusfuehrungsStatus = Aktiv`, `ProjektleiterAgentId`, `AktiveRunId`, `LastHeartbeatUtc`.
5. Loggt und gibt die `agentId` zurück.

Es wird **kein Prozess gestartet** — keine ConPTY-Session, keine CLI, kein Initialprompt wird abgesetzt. Deshalb: Klick auf „Start" ändert nur DB-Status, aber es entsteht kein `plan.md`, keine sichtbare CLI-Aktivität. Das ist keine Regression aus dem letzten UI-Integrations-Zyklus, sondern eine bislang fehlende Kernfunktion — `ProjektleiterAgentService` ist bisher reine Buchhaltung, kein Agent-Runner.

### Relevante bestehende Infrastruktur für reguläre (nicht-autonome) Aufgaben, als Vorbild/Wiederverwendungskandidat

- **`KiAusfuehrungsService`** (`src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`) ist der zentrale Singleton-Service, der CLI-Prozesse pro Aufgabe verwaltet (`StartWithPseudoConsoleAsync`, `StopCliAsync`, `IsRunning`, `CliProcessStatusChanged`-Event). Nimmt `IKiPlugin kiPlugin`, `localRepoPath`, optionale `optionalParameters` (Session-/Resume-Parameter) entgegen, startet den Prozess per ConPTY und sendet den vom Plugin gebauten CLI-Befehl verzögert (~300ms) in die Pseudo-Konsole.
- **`EntwicklungsprozessService`** zeigt das Muster für reguläre Aufgaben:
  - `ProzessStartenUndCliStartenAsync(...)` (Zeile ~124): Erststart — Klon + Branch + CLI-Start ohne `optionalParameters` (Initialprompt kommt vermutlich über `aufgabe.AnforderungsBeschreibung`/Plugin-eigene Mechanik, nicht über den Parameter).
  - `CliNeustartenAsync(aufgabeId, kiPluginPrefix, optionalParameters, ct)` (Zeile ~183): Erneuter CLI-Start im bereits vorhandenen Klon — genutzt für „CLI starten" (Ribbon, `TaskDetailViewModel.CliNeustartenAsync`) und ist der Ort, an dem `optionalParameters` (z. B. ein Resume-/„Mach bitte weiter"-Prompt) durchgereicht werden kann.
- **`IKiPlugin`/`CliKiPluginBase`** (`src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/CliKiPluginBase.cs`) hat bereits das Konzept `SupportsSessionContinuation()` (abstract) — Session-Fortsetzung ist also plugin-seitig vorgesehen, muss für Autonome Aufgaben aber noch geprüft/genutzt werden (wie genau ein Plugin „--resume" umsetzt, ist je Plugin zu klären, z. B. `ClaudeCliPlugin`).
- **`AufgabeRecoveryService`** (`src/Softwareschmiede/Application/Services/AufgabeRecoveryService.cs`) ist die bestehende Crash-Recovery für reguläre Aufgaben — **wichtig**: Sie ist rein manuell (Nutzer muss Wiederherstellung explizit auslösen) und schließt Autonome Aufgaben laut eigenem Kommentar ausdrücklich aus (Zeile ~35-37: „Autonome Aufgaben werden durch den Projektleiter-Agenten selbst gesteuert, nicht durch die generische Crash-Recovery"). Es gibt also **aktuell keinen automatischen CLI-Neustart beim App-Neustart**, auch nicht für reguläre Aufgaben — das für Punkt 4 unten benötigte Verhalten muss neu entworfen werden, nicht nur von einem bestehenden Mechanismus kopiert werden.

## Anforderungen (User, 2026-08-24)

1. **UI-Aufräumen**: Die Buttons „Start", „Stop", „Resume" im Inhaltsbereich von `AutonomAufgabeDetailView.xaml` (aktuell Zeilen 23-38, `StartCommand`/`StopCommand`/`ResumeCommand`) können entfernt werden, da sie bereits im Ribbon (Gruppe „Autonome Aufgabe") verfügbar sind. Keine doppelte Bedienung nötig.

2. **Echter CLI-Start beim Klick auf „Start"**: Mit Klick auf „Start" für eine Autonome Aufgabe soll die CLI tatsächlich ausgeführt werden — analog zum Verhalten bei einer normalen Aufgabe (`KiAusfuehrungsService`/`EntwicklungsprozessService`-Muster). Der Initialprompt (`AutonomAufgabeKonfiguration.InitialPrompt`) soll abgesetzt werden, sodass der „Projektleiter"-Agent seine Arbeit beginnt (nicht nur DB-Status setzen wie aktuell).

3. **Ribbon-Actions ausblenden nach Einrichtung**: Die regulären Ribbon-Actions „Start" (`AutomationName="Starten"`, `StartenCommand`) und „Beenden" (`AutomationName="Beenden"`, `AufgabeAbschliessenCommand`) in der Gruppe „Aufgabe" (`TaskDetailView.xaml` Zeilen ~49-56) sollen ausgeblendet werden, sobald eine Aufgabe einmal als Autonome Aufgabe eingerichtet wurde — die Steuerung läuft dann ausschließlich über die Gruppe „Autonome Aufgabe". (Zu prüfen im Inventory/Plan: ob auch „CLI starten"/„Stoppen" in der Gruppe „Ausführung", Zeilen ~80-89, betroffen sein sollen — der Auftrag nennt explizit nur „Start" und „Beenden".)

4. **Persistenz über App-Neustart hinweg / Resume-Semantik**:
   - Beenden des Programms und Wiederöffnen der Autonomen Aufgabe: Falls die Autonome Aufgabe zuvor gestartet wurde (und nicht explizit gestoppt), soll die CLI beim Öffnen der Aufgabe **automatisch wieder geöffnet** werden.
   - Wurde die Ausführung dagegen explizit gestoppt, soll die CLI beim Wiederöffnen **nicht** automatisch erneut ausgeführt werden.
   - Beim automatischen Wiederausführen der CLI muss die **alte Session fortgesetzt** werden (Resume-Parameter des jeweiligen KI-Plugins, siehe `SupportsSessionContinuation()`), und es soll ein **„Mach bitte weiter"-Prompt** abgesetzt werden (nicht der ursprüngliche Initialprompt erneut).

## Hinweis für die Umsetzung

Diese vier Punkte hängen zusammen (ein echter Agent-Runner ist Voraussetzung für 2 und 4) und sollten im selben Lifecycle-Zyklus behandelt werden. Requirement 4 braucht vermutlich einen neuen Zustand/ein neues Feld, um zu unterscheiden „lief zuletzt, war aber nicht explizit gestoppt" (→ Auto-Resume) vs. „wurde explizit gestoppt" (→ kein Auto-Resume) — dafür genügt der bestehende `AusfuehrungsStatus`/`AbsichtlichGestoppt`-Ansatz aus `KiAusfuehrungsService`/`CliProcessHandle` evtl. nicht 1:1, da er pro laufendem Prozess-Handle lebt und nicht persistiert wird. Das ist im Inventory/Plan-Schritt genauer zu klären.

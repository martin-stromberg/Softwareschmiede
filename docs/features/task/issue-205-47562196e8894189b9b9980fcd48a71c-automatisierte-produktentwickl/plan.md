# Umsetzungsplan: Autonome Aufgaben – Echte CLI-Ausführung und UI-Integration

## Übersicht

Autonome Aufgaben sollen echte CLI-Prozesse starten und verwalten (nicht nur DB-Updates), den Lifecycle über App-Neustarts persistieren (automatischer Resume für nicht explizit gestoppte Aufgaben), und die UI-Steuerung auf das Ribbon beschränken (Doppelbedienung entfernen). Dies erfordert ein neues Persistenz-Flag für explizites Stoppen, Integration mit `KiAusfuehrungsService` in `ProjektleiterAgentService`, einen App-Startup-Recovery-Mechanismus, und UI-Visibility-Bindungen zur Trennung regulärer und autonomer Aufgaben-Buttons.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| **Explizites-Stoppen-Flag** | `AutonomAufgabeKonfiguration.ExplizitGestoppt` als `bool` (nicht DateTimeOffset) | Einfacher und ausreichend für die Anforderung; kein Audit-Trail nötig. Rückseite: Bei Budget-Limit wird das Flag *nicht* gesetzt (nur bei manuellem Benutzer-Stoppen), um Session-Pause und explizites Stoppen zu unterscheiden. |
| **App-Startup-Recovery** | Direkter Aufruf in `App.xaml.cs`/`StartupAsync()` nach DB-Migration und vor `MainWindow.Show()` | Folgt bestehendem Muster (wie `PromptVorlagenService.EnsureInitialPromptVorlagenAsync()`), benötigt keine neue Hosted Service, einfach zu debuggen. Wird in einem `CreateScope()` aufgerufen (wie DbContext-Migration). |
| **Resume-Prompt-Generierung** | Dynamisch aus `plan.md`, `progress.md` und aktuellem Status generiert (analog zu `SessionManagementService.ErstelleWeitermachenPrompt()`) | Bietet Kontext, den die CLI für intelligentes Weitermachen nutzt. Bei Bedarf auch als statischer Fallback implementierbar. |
| **CLI-Prompt-Zustellung (korrigiert — verifiziert per Codeprüfung)** | `optionalParameters` bei `KiAusfuehrungsService.StartWithPseudoConsoleAsync()`/`IKiPlugin.StartCliAsync()` wird bei **jedem** vorhandenen Plugin (`ClaudeCliPlugin`, `DevinPlugin`, `CodexPlugin`, `GitHubCopilotPlugin` — je `BuildProcessStartInfo()` geprüft) 1:1 als rohe `ProcessStartInfo.Arguments` verwendet, **nicht** als Freitext-Prompt. Der Initial-/Weitermachen-Prompt darf daher **nicht** über `optionalParameters` übergeben werden. Stattdessen: Nach dem CLI-Start wird der Prompttext **separat** über die bereits vorhandene, aber bisher nur vom unabhängigen `PromptZeitVersandService`-Feature genutzte Methode `PseudoConsoleSession.WritePromptAsync(promptText, ct)` gesendet — die Session wird über das bereits öffentliche `KiAusfuehrungsService.GetPseudoConsoleSession(aufgabeId)` geholt. `optionalParameters` wird ausschließlich für ein echtes CLI-Flag genutzt (siehe nächste Zeile). | Ohne diese Korrektur würde `StartWithPseudoConsoleAsync()` den Prompttext als Kommandozeilenargumente an die CLI übergeben — funktionsunfähig (z. B. `claude Weitermachen: Setze die Arbeit ... fort` als Argumentliste statt als Chat-Eingabe). `WritePromptAsync()` ist der einzige im Code vorhandene Mechanismus, der Freitext in eine laufende ConPTY-Session schreibt. |
| **Timing des Prompt-Versands nach CLI-Start** | Nach `StartWithPseudoConsoleAsync()` wird der Prompt mit fester Verzögerung gesendet (Konstante, z. B. 3000ms — deutlich länger als die bestehende 300ms-Verzögerung in `KiAusfuehrungsService.SendCommandDelayedAsync()`, da dort nur auf die `cmd.exe`-Bereitschaft gewartet wird, hier zusätzlich auf den Eigenstart der KI-CLI selbst). Best-Effort, kein Ready-Signal vorhanden. | Es gibt aktuell keinen Bereitschafts-/Ready-Indikator der CLI im Code (auch `SendCommandDelayedAsync()` selbst arbeitet nach demselben Prinzip einer festen Verzögerung). Eine feste, groß genug bemessene Verzögerung ist die einzige mit vertretbarem Aufwand umsetzbare Lösung in diesem Zyklus; Verbesserung (z. B. Output-Parsing auf CLI-Prompt-Erkennung) ist eine mögliche Folgeaufgabe. |
| **Plugin-Support für Session-Continuation (`--resume`-Flag)** | `optionalParameters = "--continue"` wird nur übergeben, wenn `kiPlugin.SupportsSessionContinuation() == true` **und** es sich um einen Wiederstart (App-Neustart-Recovery) handelt, nicht beim Erststart. Verifiziert für `ClaudeCliPlugin` (Claude-Code-CLI unterstützt `--continue` zum Fortsetzen der zuletzt aktiven Session im aktuellen Arbeitsverzeichnis, ohne dass eine Session-ID gespeichert werden muss). Für `DevinPlugin` (ebenfalls `SupportsSessionContinuation() == true`) ist die exakte Flag-Syntax nicht verifiziert — wird als bekannte Einschränkung dokumentiert, nicht blockierend für diesen Zyklus, da `ClaudeCliPlugin` das primär genutzte Plugin ist. | Vermeidet eine größere Interface-Änderung (z. B. plugin-spezifische Resume-Flag-Erzeugung über eine neue `IKiPlugin`-Methode), die den Rahmen dieser Anforderung sprengen würde; nutzt stattdessen das bereits vorhandene `optionalParameters`-Arguments-Passthrough korrekt für seinen eigentlichen Zweck. |
| **Stop-Logik** | Gehört zu `ProjektleiterAgentService` (neue Methode `StoppeAgenExplizitAsync()`), ruft `KiAusfuehrungsService.StopCliAsync()` auf | Zentralisiert CLI-Lifecycle-Logik bei Projektleiter-Agenten, nicht bei DB-Update. |
| **Visibility für reguläre Aufgaben-Buttons** | Property `TaskDetailViewModel.IsAutonomAufgabe` + Binding mit `BooleanToVisibilityConverter` und `ConverterParameter=Inverted` auf Ribbon-Buttons „Start", „Beenden" (beide Gruppen) | Konsistenz: Wenn autonome Aufgabe aktiv, alle regulären Aufgaben-Buttons unsichtbar. Gilt für Gruppe „Aufgabe" und ggf. „Ausführung". |

## Programmabläufe

### Autonome Aufgabe starten (Erststart via Ribbon)

1. Benutzer klickt Ribbon-Button „Start" (Gruppe „Autonome Aufgabe")
2. `AutonomAufgabeDetailViewModel.StartCommand` wird ausgelöst
3. ViewModel ruft `ProjektleiterAgentService.StarteAgentAsync(konfiguration, optionalResumePrompt: null, ct)` auf
4. Service generiert Initialprompt + Skill-Datei (bestehendes Verhalten)
5. Service ruft `KiAusfuehrungsService.StartWithPseudoConsoleAsync()` auf mit:
   - `aufgabeId`: ID der Aufgabe
   - `kiPlugin`: aufgelöst via `PluginSelectionService.ResolveDevelopmentAutomationPluginAsync(Aufgabe.KiPluginPrefix, ct)`
   - `localRepoPath`: `AutonomAufgabeKonfiguration.ArbeitsverzeichnisPfad`
   - `optionalParameters`: null (Erststart — kein Resume-Flag, siehe Designentscheidung „Plugin-Support für Session-Continuation")
6. Service wartet die feste CLI-Eigenstart-Verzögerung ab (siehe Designentscheidung „Timing des Prompt-Versands nach CLI-Start"), holt die Session via `KiAusfuehrungsService.GetPseudoConsoleSession(aufgabeId)` und sendet den Initialprompt via `session.WritePromptAsync(initialPrompt, ct)` (Fire-and-forget analog zu `KiAusfuehrungsService.SendCommandDelayedAsync()`, damit `StarteAgentAsync()` nicht blockiert)
7. Service setzt `ExplizitGestoppt = false` in Konfiguration
8. Service setzt `AusfuehrungsStatus = Aktiv`
9. Service speichert DB
10. `KiAusfuehrungsService.CliProcessStatusChanged` wird ausgelöst mit `CliProcessStatus.Gestartet`
11. ViewModel lauscht auf Event und setzt `CliIsRunning = true`
12. UI zeigt Status an

**Beteiligte Klassen/Komponenten:** `AutonomAufgabeDetailViewModel`, `ProjektleiterAgentService`, `KiAusfuehrungsService`, `PluginSelectionService`, `AutonomAufgabeKonfiguration`, `Aufgabe`, `SoftwareschmiededDbContext`

### Autonome Aufgabe explizit stoppen

1. Benutzer klickt Ribbon-Button „Beenden" (Gruppe „Autonome Aufgabe")
2. `AutonomAufgabeDetailViewModel.StopCommand` wird ausgelöst
3. ViewModel ruft `ProjektleiterAgentService.StoppeAgenExplizitAsync(aufgabeId, ct)` auf
4. Service lädt `AutonomAufgabeKonfiguration` für Aufgabe
5. Service setzt `ExplizitGestoppt = true`
6. Service ruft `KiAusfuehrungsService.StopCliAsync(aufgabeId, ct)` auf (wartet auf Completion)
7. Service speichert DB
8. `KiAusfuehrungsService.CliProcessStatusChanged` wird ausgelöst mit `CliProcessStatus.Gestoppt`
9. ViewModel lauscht auf Event und setzt `CliIsRunning = false`
10. UI aktualisiert Status

**Beteiligte Klassen/Komponenten:** `AutonomAufgabeDetailViewModel`, `ProjektleiterAgentService`, `KiAusfuehrungsService`, `AutonomAufgabeKonfiguration`, `SoftwareschmiededDbContext`

### App-Startup-Recovery für Autonome Aufgaben

1. `App.xaml.cs`/`OnStartup()` wird aufgerufen
2. Logging wird initialisiert
3. `StartupAsync(e)` wird aufgerufen
4. Host wird erstellt und gestartet
5. `CliProcessManager` wird initialisiert (bestehendes Verhalten)
6. Scoped-Scope wird erstellt
7. `SoftwareschmiededDbContext.Database.MigrateAsync()` wird aufgerufen (bestehendes Verhalten)
8. `PromptVorlagenService.EnsureInitialPromptVorlagenAsync()` wird aufgerufen (bestehendes Verhalten)
9. **NEU:** App-Recovery wird aufgerufen (inline oder via Service):
   - Abfrage: `AutonomAufgabenKonfigurationen.Where(k => !k.ExplizitGestoppt && k.Aufgabe.AusfuehrungsStatus == AufgabeAusfuehrungsStatus.Aktiv)`
   - Für jede Konfiguration:
     a. Generiere Resume-Prompt aus `plan.md`, `progress.md` und Konfiguration (analog zu `SessionManagementService.ErstelleWeitermachenPrompt()`)
     b. Rufe `ProjektleiterAgentService.StarteAgenNachAppNeustartAsync(aufgabeId, resumePrompt, ct)` auf
     c. Service ruft `StarteAgentAsync(konfiguration, optionalResumePrompt: resumePrompt, ct)` auf
     d. `StarteAgentAsync()` startet die CLI via `KiAusfuehrungsService.StartWithPseudoConsoleAsync()` mit `optionalParameters = "--continue"` (nur falls `kiPlugin.SupportsSessionContinuation() == true`, sonst `null`) und sendet anschließend den Resume-Prompt nach der festen Verzögerung via `WritePromptAsync()` (identischer Mechanismus wie beim Erststart, siehe oben — der einzige Unterschied ist der übergebene Prompttext und ggf. das `--continue`-Flag)
     e. Fehler werden geloggt, behindern aber nicht den App-Startup (Best-Effort)
10. Scoped-Scope wird disposed
11. `MainWindow` wird erstellt und angezeigt (bestehendes Verhalten)

**Beteiligte Klassen/Komponenten:** `App.xaml.cs`, `SoftwareschmiededDbContext`, `ProjektleiterAgentService`, `AutonomAufgabeKonfiguration`, `Aufgabe`, `ILogger`

### Reguläre Aufgaben-Buttons ausblenden bei autonomen Aufgaben

1. `TaskDetailViewModel` wird für eine Aufgabe initialisiert
2. ViewModel berechnet `IsAutonomAufgabe = (AufgabenDetails?.AutonomKonfiguration != null)`
3. Property wird an Binding in `TaskDetailView.xaml` gebunden
4. Ribbon-Buttons „Start" und „Beenden" (Gruppe „Aufgabe") und ggf. „CLI starten" / „Stoppen" (Gruppe „Ausführung") erhalten Visibility-Binding:
   - `Visibility="{Binding IsAutonomAufgabe, Converter={StaticResource BooleanToVisibilityConverter}, ConverterParameter=Inverted}"`
5. Bei `IsAutonomAufgabe == true` werden die Buttons unsichtbar
6. Ribbon-Buttons in Gruppe „Autonome Aufgabe" sind dann sichtbar (bereits vorhanden)

**Beteiligte Klassen/Komponenten:** `TaskDetailViewModel`, `TaskDetailView.xaml`, Ribbon-Control

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| Keine neuen Klassen erforderlich | — | App-Recovery wird inline in `App.xaml.cs`/`StartupAsync()` oder als Private-Helper-Methode in `ProjektleiterAgentService` implementiert |

## Änderungen an bestehenden Klassen

### `AutonomAufgabeKonfiguration` (Datenmodell)

- **Neue Eigenschaft:** `ExplizitGestoppt` (`bool`, NOT NULL, default: false) — Kennzeichnet, ob der Benutzer die CLI explizit gestoppt hat (vs. nur bei Budget-Limit pausiert)

### `ProjektleiterAgentService` (Service)

- **Erweiterte Methode:** `StarteAgentAsync(konfiguration, optionalResumePrompt?, ct)`
  - **Änderung:** Fügt echten CLI-Start hinzu via `KiAusfuehrungsService.StartWithPseudoConsoleAsync()` mit:
    - `kiPlugin`: aufgelöst via `PluginSelectionService.ResolveDevelopmentAutomationPluginAsync(Aufgabe.KiPluginPrefix, ct)` (korrekter Methodenname — nicht `GetPluginByPrefixAsync`, diese Methode existiert nicht)
    - `optionalParameters`: `"--continue"` falls `optionalResumePrompt` gesetzt ist (= Resume-Fall) **und** `kiPlugin.SupportsSessionContinuation() == true`, sonst `null`. **Wichtig:** `optionalResumePrompt` selbst wird **nicht** als `optionalParameters` übergeben (siehe Designentscheidung „CLI-Prompt-Zustellung" — würde als CLI-Argument statt als Prompttext interpretiert)
  - **Neu:** Nach erfolgreichem `StartWithPseudoConsoleAsync()`-Aufruf: Prompttext ermitteln (`optionalResumePrompt` falls gesetzt, sonst `konfiguration.InitialPrompt`) und per Fire-and-forget-Task mit fester Verzögerung senden:
    ```csharp
    SendeInitialPromptVerzoegertAsync(aufgabeId, promptText, ct).SafeFireAndForget(_logger, "ProjektleiterAgentService.SendeInitialPromptVerzoegertAsync");
    ```
    Die private Hilfsmethode `SendeInitialPromptVerzoegertAsync` wartet die Verzögerung ab, holt `_kiAusfuehrungsService.GetPseudoConsoleSession(aufgabeId)` und ruft — falls die Session nicht null ist — `session.WritePromptAsync(promptText, ct)` auf; Exceptions werden geloggt, nicht geworfen (Best-Effort, analog zu `KiAusfuehrungsService.SendCommandDelayedAsync()`)
  - **Implizit:** Setzt `ExplizitGestoppt = false` am Anfang (Neustarts setzen es nicht auf false, daher neue Logik: "wenn gestartet, nicht mehr explizit gestoppt")
  - **Fehlerbehandlung:** Bei `StartWithPseudoConsoleAsync()` Exception: loggen, `AusfuehrungsStatus = Beendet` setzen, Exception werfen (fällt in Startup-Recovery Best-Effort)

- **Neue Methode:** `StarteAgenNachAppNeustartAsync(aufgabeId, resumePrompt, ct)` → `Task`
  - Lädt `AutonomAufgabeKonfiguration` für Aufgabe
  - Prüft: `!ExplizitGestoppt && AusfuehrungsStatus == Aktiv`
  - Ruft `StarteAgentAsync(konfiguration, optionalResumePrompt: resumePrompt, ct)` auf
  - Wenn nicht aktiv oder explizit gestoppt: silent return (kein Fehler)

- **Neue Methode:** `StoppeAgenExplizitAsync(aufgabeId, ct)` → `Task`
  - Lädt `AutonomAufgabeKonfiguration` für Aufgabe
  - Setzt `ExplizitGestoppt = true`
  - Ruft `KiAusfuehrungsService.StopCliAsync(aufgabeId, ct)` auf
  - Speichert DB
  - **Fehlerbehandlung:** Bei `StopCliAsync()` Exception: loggen, aber nicht werfen (Best-Effort-Stop)

- **Neue Abhängigkeit:** `PluginSelectionService` (zur Auflösung von `Aufgabe.KiPluginPrefix` zu `IKiPlugin`)

### `AutonomAufgabeDetailViewModel` (ViewModel)

- **Neue Eigenschaft:** `CliIsRunning` (`bool`, bindbar) — True, wenn `KiAusfuehrungsService.IsRunning(aufgabeId)` true ist
  - Initialisiert mit: `KiAusfuehrungsService.IsRunning(aufgabeId)`
  - Wird aktualisiert via Event-Handler (siehe unten)

- **Neuer Event-Handler:** Lauscht auf `KiAusfuehrungsService.CliProcessStatusChanged`
  - Wenn Event für diese `aufgabeId` ausgelöst wird: `CliIsRunning = (status == CliProcessStatus.Gestartet)` setzen
  - Binding Notification triggern für UI-Update

- **Neuer Command:** `StopCommand` (bereits gebunden im Ribbon, falls nicht vorhanden)
  - Ruft `ProjektleiterAgentService.StoppeAgenExplizitAsync(aufgabeId, ct)` auf
  - Nach Completion: Event-Handler aktualisiert `CliIsRunning`

### `TaskDetailViewModel` (ViewModel)

- **Neue Eigenschaft:** `IsAutonomAufgabe` (`bool`, bindbar, read-only) → `AufgabenDetails?.AutonomKonfiguration != null`
  - Wird berechnet, wenn `AufgabenDetails` aktualisiert wird
  - Binding-Notification triggern bei Änderung

### `TaskDetailView.xaml` (UI)

- **Visibility-Binding für Ribbon-Buttons:**
  - Buttons „Start" und „Beenden" (Gruppe „Aufgabe"): `Visibility="{Binding IsAutonomAufgabe, Converter={StaticResource BooleanToVisibilityConverter}, ConverterParameter=Inverted}"`
  - Prüfe auch Gruppe „Ausführung" auf „CLI starten" / „Stoppen" Buttons; falls vorhanden, gleiche Visibility anwenden

### `App.xaml.cs` (Anwendungs-Startup)

- **Erweiterte Methode:** `StartupAsync(StartupEventArgs e)`
  - Nach `PromptVorlagenService.EnsureInitialPromptVorlagenAsync()` (Zeile 98):
    - Erstelle neuen Scope: `using (var recoveryScope = _host.Services.CreateScope())`
    - Hole `ProjektleiterAgentService` und `SoftwareschmiededDbContext` aus recoveryScope
    - Abfrage aller `AutonomAufgabenKonfigurationen` mit `!ExplizitGestoppt && AusfuehrungsStatus == Aktiv`
    - Für jede: generiere Resume-Prompt und rufe `ProjektleiterAgentService.StarteAgenNachAppNeustartAsync()` auf
    - **Fehlerbehandlung:** Fehler loggen, aber nicht werfen (Best-Effort)
    - Scope wird disposed

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `AddExplizitGestopptToAutonomAufgabeKonfiguration` | `AutonomAufgabeKonfigurationen.ExplizitGestoppt` | Neue Spalte `ExplizitGestoppt` (bool, NOT NULL) mit Default `false`. Migration muss nach dem Ändern von `AutonomAufgabeKonfiguration` erstellt werden (dotnet ef migrations add). |

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `AutonomAufgabeKonfiguration.ExplizitGestoppt` | Readonly nach Seriablisierung; wird nur durch `StoppeAgenExplizitAsync()` gesetzt | Direkter DB-Update außer Applikation würde Konsistenz verletzen (kein Kodefehler nötig, wird durch Service-Layer verhindert) |

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| Keine | — | — | — |

## Seiteneffekte und Risiken

- **CLI-Integration in `ProjektleiterAgentService.StarteAgentAsync()`:** Service ist nicht länger ein reiner DB-Service — startet jetzt echte Prozesse. Tests müssen `KiAusfuehrungsService` mocken (ggf. bereits teilweise vorhanden). **Risiko:** Höhere Wahrscheinlichkeit, dass Tests beim echten Prozessstart timeout-en (mitigiert durch bestehende `SimulatedPseudoConsoleProcessLauncher` in Tests).

- **App-Startup kann länger dauern:** Recovery-Scan + mögliche CLI-Starts beim App-Startup. **Risiko:** Bei vielen aktiven Aufgaben könnten mehrere CLIs gleichzeitig starten. **Mitigation:** Recovery wird sequenziell durchgeführt, nicht parallel (K.I.I.S.-Prinzip).

- **DI-Lifetime-Kompatibilität:** `ProjektleiterAgentService` ist Scoped, wird aber in `StartupAsync()` in einem CreateScope() aufgerufen (OK). PluginSelectionService wird als Scoped registriert (OK, wird in Scope injiziert). Kein Risiko erkannt.

- **Tests für AutonomAufgabeStartService oder andere Klassen, die `StarteAgentAsync()` aufrufen:** Diese Tests benötigen jetzt Mock-Setup für `KiAusfuehrungsService`. Bestehende Tests können fehlschlagen, wenn sie nicht vorbereitet sind.

- **AutonomAufgabeDetailView.xaml:** Falls Buttons im Inhaltsbereich vorhanden sind (Anforderung deutet darauf hin, dass sie entfernt werden sollen), sollen diese entfernt werden (keine Änderung an Sichtbarkeit, sondern Löschung). Dies ist im UI-Cleanup-Schritt zu behandeln, nicht hier.

## Umsetzungsreihenfolge

1. **Datenbank-Migration erstellen: `AddExplizitGestopptToAutonomAufgabeKonfiguration`**
   - Voraussetzungen: Keine
   - Beschreibung: Erstelle EF Core Migration (dotnet ef migrations add) für neue Spalte `ExplizitGestoppt` (bool, NOT NULL, default: false)

2. **Eigenschaft `ExplizitGestoppt` zu `AutonomAufgabeKonfiguration` hinzufügen**
   - Voraussetzungen: Migration erstellt (Schritt 1)
   - Beschreibung: Füge C#-Eigenschaft `public bool ExplizitGestoppt { get; set; } = false;` zu `AutonomAufgabeKonfiguration.cs` hinzu

3. **`ProjektleiterAgentService` erweitern: `StarteAgentAsync()` mit CLI-Integration**
   - Voraussetzungen: `KiAusfuehrungsService` (vorhanden, Singleton), `PluginSelectionService` (vorhanden, Scoped), Migration Schritt 1-2
   - Beschreibung:
     - Injiziere `KiAusfuehrungsService` und `PluginSelectionService` in Constructor
     - Erweitere `StarteAgentAsync(AutonomAufgabeKonfiguration konfiguration, string? optionalResumePrompt, CancellationToken ct)` um:
       - Auflösung von `Aufgabe.KiPluginPrefix` via `PluginSelectionService.ResolveDevelopmentAutomationPluginAsync(pluginPrefix, ct)` (korrekter, bestehender Methodenname)
       - Aufruf von `KiAusfuehrungsService.StartWithPseudoConsoleAsync(aufgabeId, kiPlugin, ArbeitsverzeichnisPfad, optionalParameters: (optionalResumePrompt is not null && kiPlugin.SupportsSessionContinuation()) ? "--continue" : null, ...)` — **nicht** den Prompttext selbst als `optionalParameters` übergeben
       - Neu: private Hilfsmethode `SendeInitialPromptVerzoegertAsync(aufgabeId, promptText, ct)`, die nach fester Verzögerung `_kiAusfuehrungsService.GetPseudoConsoleSession(aufgabeId)` abruft und `session.WritePromptAsync(promptText, ct)` aufruft (Fire-and-forget, Best-Effort mit Logging bei Fehlern); Prompttext = `optionalResumePrompt ?? konfiguration.InitialPrompt`
       - Setzt `ExplizitGestoppt = false` vor CLI-Start
     - Fehlerbehandlung: Exception bei `StartWithPseudoConsoleAsync()` loggen, `AusfuehrungsStatus = Beendet` setzen, werfen

4. **`ProjektleiterAgentService` erweitern: neue Methode `StarteAgenNachAppNeustartAsync()`**
   - Voraussetzungen: Schritt 3 (StarteAgentAsync erweitert)
   - Beschreibung:
     - Neue öffentliche Methode: `public async Task StarteAgenNachAppNeustartAsync(Guid aufgabeId, string resumePrompt, CancellationToken ct)`
     - Lädt `AutonomAufgabeKonfiguration` via `LadeKonfigurationAsync(aufgabeId, ct)` (bestehende Private-Methode)
     - Prüft: `!ExplizitGestoppt && AusfuehrungsStatus == AufgabeAusfuehrungsStatus.Aktiv`
     - Ruft `StarteAgentAsync(konfiguration, optionalResumePrompt: resumePrompt, ct)` auf
     - Wenn Prüfung false: silent return

5. **`ProjektleiterAgentService` erweitern: neue Methode `StoppeAgenExplizitAsync()`**
   - Voraussetzungen: `KiAusfuehrungsService` (vorhanden), Migration Schritt 1-2
   - Beschreibung:
     - Neue öffentliche Methode: `public async Task StoppeAgenExplizitAsync(Guid aufgabeId, CancellationToken ct)`
     - Lädt `AutonomAufgabeKonfiguration` via `LadeKonfigurationAsync(aufgabeId, ct)`
     - Setzt `ExplizitGestoppt = true`
     - Ruft `KiAusfuehrungsService.StopCliAsync(aufgabeId, ct)` auf
     - Speichert DB
     - Fehlerbehandlung: Exception bei `StopCliAsync()` loggen, nicht werfen

6. **`App.xaml.cs` erweitern: App-Startup-Recovery in `StartupAsync()`**
   - Voraussetzungen: Schritt 3-5 (ProjektleiterAgentService-Methoden implementiert), Migration Schritt 1-2
   - Beschreibung:
     - Nach `PromptVorlagenService.EnsureInitialPromptVorlagenAsync()` (Zeile 98):
       - Erstelle Scope: `using (var recoveryScope = _host.Services.CreateScope())`
       - Hole `ProjektleiterAgentService`, `SoftwareschmiededDbContext`, `ILogger`
       - Abfrage: `aufgaben = await db.AutonomAufgabenKonfigurationen.Include(a => a.Aufgabe).Where(k => !k.ExplizitGestoppt && k.Aufgabe.AusfuehrungsStatus == AufgabeAusfuehrungsStatus.Aktiv).ToListAsync(ct)`
       - Für jede `aufgabe`: 
         - Generiere `resumePrompt` (ähnlich `SessionManagementService.ErstelleWeitermachenPrompt()`)
         - Rufe `StarteAgenNachAppNeustartAsync(aufgabeId, resumePrompt, ct)` auf
         - Fehler: loggen, continue (Best-Effort)

7. **`AutonomAufgabeDetailViewModel` erweitern: `CliIsRunning` und Event-Handler**
   - Voraussetzungen: `KiAusfuehrungsService` (vorhanden, Singleton)
   - Beschreibung:
     - Neue Eigenschaft: `bool CliIsRunning { get; set; }` (mit INotifyPropertyChanged)
     - Im Constructor oder OnLoad: `CliIsRunning = _kiAusfuehrungsService.IsRunning(aufgabeId)`
     - Abonniere Event: `_kiAusfuehrungsService.CliProcessStatusChanged += OnCliProcessStatusChanged`
     - Handler prüft: `if (aufgabeId == eventAufgabeId) CliIsRunning = (status == CliProcessStatus.Gestartet)`
     - Cleanup: Abmelden im Destructor oder OnUnload

8. **`AutonomAufgabeDetailViewModel` erweitern: `StopCommand`**
   - Voraussetzungen: Schritt 5 (StoppeAgenExplizitAsync() implementiert), Schritt 7 (CliIsRunning vorhanden)
   - Beschreibung:
     - Neuer RelayCommand: `StopCommand` (falls nicht bereits vorhanden)
     - Execute: ruft `ProjektleiterAgentService.StoppeAgenExplizitAsync(aufgabeId, ct)` auf
     - CanExecute: prüft `CliIsRunning && AusfuehrungsStatus == Aktiv`
     - Nach erfolgreicher Ausführung: Event-Handler aktualisiert automatisch `CliIsRunning`

9. **`TaskDetailViewModel` erweitern: `IsAutonomAufgabe` Eigenschaft**
   - Voraussetzungen: Keine (nur C#-Logik)
   - Beschreibung:
     - Neue Eigenschaft: `bool IsAutonomAufgabe { get; }` (read-only, mit INotifyPropertyChanged)
     - Getter: `return AufgabenDetails?.AutonomKonfiguration != null;`
     - Wird recomputed, wenn `AufgabenDetails` sich ändert (z. B. in Property-Setter)

10. **`TaskDetailView.xaml` erweitern: Visibility-Binding für reguläre Aufgaben-Buttons**
    - Voraussetzungen: Schritt 9 (IsAutonomAufgabe implementiert), bestehender Ribbon mit Buttons
    - Beschreibung:
      - Finde Ribbon-Buttons „Start" und „Beenden" (Gruppe „Aufgabe") und ggf. „CLI starten" / „Stoppen" (Gruppe „Ausführung")
      - Füge Visibility-Binding hinzu: `Visibility="{Binding IsAutonomAufgabe, Converter={StaticResource BooleanToVisibilityConverter}, ConverterParameter=Inverted}"`
      - Teste: Bei autonomer Aufgabe sollten Buttons unsichtbar sein; bei regulärer Aufgabe sichtbar

11. **Unit-Tests für `ProjektleiterAgentService`**
    - Voraussetzungen: Schritt 3-5 (Methoden implementiert), bestehende Test-Infrastruktur
    - Beschreibung:
      - Test: `StarteAgentAsync_CallsKiAusfuehrungsService_WithInitialPrompt()` — Mock `KiAusfuehrungsService`, prüfe Aufruf mit `optionalParameters = null`
      - Test: `StarteAgentAsync_WithResumePrompt_CallsKiAusfuehrungsServiceWithResumePrompt()` — Aufruf mit `resumePrompt`, prüfe Weitergabe
      - Test: `StarteAgenNachAppNeustartAsync_WennNichtExplizitGestoppt_StartetNeuMitResumePrompt()` — Prüfe, dass mit Resume-Prompt gestartet wird
      - Test: `StarteAgenNachAppNeustartAsync_WennExplizitGestoppt_StartetNicht()` — Prüfe, dass nicht gestartet wird
      - Test: `StoppeAgenExplizitAsync_SetzExplizitGestoppt()` — Prüfe Flag-Setting und CLI-Stop-Aufruf
      - Fehlerbehandlungs-Tests: `StarteAgentAsync_WhenKiAusfuehrungsServiceThrows_SetsStatusBeendetAndRethrows()`

12. **Unit-Tests für `AutonomAufgabeDetailViewModel`**
    - Voraussetzungen: Schritt 7-8 (CliIsRunning, Event-Handler, StopCommand implementiert), bestehende Test-Infrastruktur
    - Beschreibung:
      - Test: `CliIsRunning_InitializedWithKiAusfuehrungsServiceState()` — Prüfe Initialisierung
      - Test: `CliIsRunning_UpdatesWhenCliProcessStatusChanged()` — Mock Event, prüfe Property-Update
      - Test: `StopCommand_CallsProjektleiterAgentService()` — Mock Service, prüfe Aufruf
      - Test: `StopCommand_CanExecute_WhenCliRunning()` — Prüfe CanExecute-Logik

13. **Unit-Tests für `TaskDetailViewModel`**
    - Voraussetzungen: Schritt 9 (IsAutonomAufgabe implementiert), bestehende Test-Infrastruktur
    - Beschreibung:
      - Test: `IsAutonomAufgabe_True_WhenAutonomKonfigurationPresent()` — Prüfe Binding-Logik
      - Test: `IsAutonomAufgabe_False_WhenAutonomKonfigurationNull()` — Prüfe False-Case

14. **E2E-Test: Autonome Aufgabe via Ribbon starten und CLI-Status prüfen**
    - Voraussetzungen: Schritt 3, 6, 7 (CLI-Integration + CliIsRunning implementiert), bestehende E2E-Test-Infrastruktur (FlaUI)
    - Beschreibung:
      - Test: `AutonomAufgabenUITests_StartViaRibbon_LaunchesCli()` (kann existierend erweitert werden, wenn bereits vorhanden)
        - Erstelle Test-Autonome-Aufgabe
        - Öffne Aufgaben-Detailansicht
        - Klicke Ribbon-Button „Start"
        - Prüfe: Ribbon-Button „Start" wird disabled, „Beenden" wird enabled
        - Prüfe: `KiAusfuehrungsService.IsRunning(aufgabeId)` == true (oder äquivalente UI-Indikation)
        - Cleanup

15. **E2E-Test: Automatisches Recovery nach App-Neustart**
    - Voraussetzungen: Schritt 6 (Recovery implementiert), Schritt 7 (CliIsRunning), bestehende E2E-Test-Infrastruktur
    - Beschreibung:
      - Test: `AutonomAufgabenUITests_AppRestartResumesActiveTasks()` (optional, wenn Infrastruktur es zulässt)
        - Erstelle und starte Autonome Aufgabe
        - Schließe App
        - Starte App neu
        - Prüfe: Aufgabe wird automatisch neugestartet (`CliIsRunning` == true)
        - Cleanup
      - **Hinweis:** Dies ist komplex für FlaUI E2E. Kann auch nur als Test der `StarteAgenNachAppNeustartAsync()`-Methode (Unit-Test mit Mock) durchgeführt werden. Entscheide basierend auf bestehender E2E-Infrastruktur.

16. **E2E-Test (optional): Explizites Stoppen via Ribbon**
    - Voraussetzungen: Schritt 5, 8 (StopCommand implementiert), bestehende E2E-Test-Infrastruktur
    - Beschreibung:
      - Test: `AutonomAufgabenUITests_StopViaRibbon_StopsCliAndPreventsAutoRestart()` (kann mit Schritt 14 konsolidiert werden)
        - Starte Autonome Aufgabe
        - Klicke Ribbon-Button „Beenden"
        - Prüfe: CLI wird gestoppt (`CliIsRunning` == false)
        - Prüfe: `ExplizitGestoppt` == true in DB (optional, über Inspection)
        - Schließe und neustarte App
        - Prüfe: Aufgabe wird nicht automatisch neugestartet
        - Cleanup

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `StarteAgentAsync_CallsKiAusfuehrungsService_WithNullOptionalParameters()` | `ProjektleiterAgentServiceTests` | CLI wird beim Erststart mit `optionalParameters = null` gestartet (nicht mit dem Initialprompt-Text) |
| `StarteAgentAsync_SendetInitialPromptUeberPseudoConsoleSession()` | `ProjektleiterAgentServiceTests` | Nach CLI-Start wird `konfiguration.InitialPrompt` per `PseudoConsoleSession.WritePromptAsync()` gesendet (nicht als `optionalParameters`) — Fake-`PseudoConsoleSession`/Fake-Launcher wie in bestehenden `KiAusfuehrungsServiceTests` verwenden |
| `StarteAgentAsync_MitResumePrompt_SendetWeitermachenPromptUeberPseudoConsoleSession()` | `ProjektleiterAgentServiceTests` | Beim Resume (`optionalResumePrompt` gesetzt) wird der Resume-Prompt statt des Initialprompts per `WritePromptAsync()` gesendet |
| `StarteAgentAsync_MitResumePromptUndSessionContinuationPlugin_UebergibtContinueFlag()` | `ProjektleiterAgentServiceTests` | Bei Resume **und** `kiPlugin.SupportsSessionContinuation() == true`: `optionalParameters == "--continue"` bei `StartWithPseudoConsoleAsync()` |
| `StarteAgentAsync_MitResumePromptOhneSessionContinuationPlugin_UebergibtKeinContinueFlag()` | `ProjektleiterAgentServiceTests` | Bei Resume **ohne** Plugin-Support: `optionalParameters == null` |
| `StarteAgenNachAppNeustartAsync_WennNichtExplizitGestoppt_StartetNeuMitResumePrompt()` | `ProjektleiterAgentServiceTests` | Recovery-Methode startet mit Resume-Prompt |
| `StarteAgenNachAppNeustartAsync_WennExplizitGestoppt_StartetNicht()` | `ProjektleiterAgentServiceTests` | Recovery-Methode startet nicht, wenn explizit gestoppt |
| `StoppeAgenExplizitAsync_SetzExplizitGestoppt()` | `ProjektleiterAgentServiceTests` | Stop-Methode setzt Flag und stoppt CLI |
| `StarteAgentAsync_WhenKiAusfuehrungsServiceThrows_SetsStatusBeendetAndRethrows()` | `ProjektleiterAgentServiceTests_Fehlerfaelle` (oder neue Error-Test-Klasse) | Fehlerbehandlung bei CLI-Fehler |
| `CliIsRunning_InitializedWithKiAusfuehrungsServiceState()` | `AutonomAufgabeDetailViewModelTests` | Property wird initialisiert |
| `CliIsRunning_UpdatesWhenCliProcessStatusChanged()` | `AutonomAufgabeDetailViewModelTests` | Event-Handler aktualisiert Property |
| `StopCommand_CallsProjektleiterAgentService()` | `AutonomAufgabeDetailViewModelTests` | Command ruft Service-Methode auf |
| `StopCommand_CanExecute_WhenCliRunning()` | `AutonomAufgabeDetailViewModelTests` | CanExecute-Logik prüft Status |
| `IsAutonomAufgabe_True_WhenAutonomKonfigurationPresent()` | `TaskDetailViewModelTests` | Property ist true bei autonomer Aufgabe |
| `IsAutonomAufgabe_False_WhenAutonomKonfigurationNull()` | `TaskDetailViewModelTests` | Property ist false bei regulärer Aufgabe |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `ProjektleiterAgentServiceTests.StarteAgentAsync_*` (bestehende Tests) | `StarteAgentAsync()` erhält neuen Parameter `optionalResumePrompt` (optional mit default null), Tests müssen ggf. Mock-Setup für `KiAusfuehrungsService` anpassen |
| `AutonomAufgabeDetailViewModelTests` (allgemein) | Neue Properties `CliIsRunning` und neue Command-Implementierungen erfordern Event-Handler-Setup in Tests |
| `TaskDetailViewModelTests` (allgemein) | Neue Property `IsAutonomAufgabe` muss in Binding-Tests berücksichtigt werden |
| Jede Testklasse, die `ProjektleiterAgentService.StarteAgentAsync()` aufruft | Mock `KiAusfuehrungsService` muss Setup haben, um CLI-Start zu simulieren (falls noch nicht der Fall) |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Autonome Aufgabe via Ribbon starten + CLI läuft | `E2E_AutonomAufgabenUITests` (neue Testklasse oder Erweiterung bestehender) | Anforderung: „Echte CLI-Prozesse starten und verwalten" + „Ribbon-Button startet echte CLI" |
| Explizites Stoppen via Ribbon | `E2E_AutonomAufgabenUITests` | Anforderung: „Explizit gestoppte Aufgaben werden nicht automatisch neugestartet" |
| App-Neustart setzt aktive Aufgaben fort (optional) | `E2E_AutonomAufgabenUITests` (optional) | Anforderung: „Automatischer Wiederstart beim App-Start" — kann auch als Unit-Test der `StarteAgenNachAppNeustartAsync()`-Methode durchgeführt werden, wenn E2E-Infrastruktur es nicht zulässt |
| Reguläre Aufgaben-Buttons unsichtbar bei autonomer Aufgabe | `E2E_AutonomAufgabenUITests` | Anforderung: „UI aufräumen; Doppelbedienung entfernen" |

**Hinweis zur E2E-Konsolidierung:** Alle Szenarien sollten in einer einzigen oder maximal zwei Test-Methoden konsolidiert werden (FlaUI-Convention: minimale Anzahl von App-Neustarts). Beispiel: Eine Methode könnte Szenarien 1–3 abdecken (Start, Stop, Visibilität prüfen auf einer Instanz); Szenario 4 (App-Neustart Resume) ggf. separate Methode, falls es echten Neustart erfordert.

Welche bestehenden E2E-Tests sind betroffen?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| Keine bekannten direkten Auswirkungen | Tests für `AutonomAufgabeDetailViewModel` oder `TaskDetailViewModel` könnten bei Visibility-Änderungen beeinträchtigt sein, aber Anforderung ändert keine bestehenden UI-Tests |

## Offene Punkte

Keine. Alle Designentscheidungen wurden basierend auf Anforderung, Bestandsaufnahme und bestehenden Patterns getroffen:

1. ✅ **Plugin-Support für Session-Continuation**: Wird zur Laufzeit geprüft; Best-Effort-Semantik (fallt back zu normalem Prompt)
2. ✅ **Resume-Prompt-Formulierung**: Dynamisch aus `plan.md`, `progress.md` generiert (analog zu bestehender `SessionManagementService`-Logik)
3. ✅ **Heartbeat-Timeout**: Wird nicht zurückgesetzt; Recovery ist unabhängig von Heartbeat
4. ✅ **„CLI starten" / „Stoppen" in Gruppe „Ausführung"**: Werden ausgeblendet (Consistency)
5. ✅ **Datenbank-Feld**: `ExplizitGestoppt` als bool
6. ✅ **optionalParameters vs. InitialPrompt**: Unterschied wird durch Methodenparameter klargestellt
7. ✅ **Explizites Stoppen vs. Session-Pause**: Flag wird nur bei manuelem Stop gesetzt

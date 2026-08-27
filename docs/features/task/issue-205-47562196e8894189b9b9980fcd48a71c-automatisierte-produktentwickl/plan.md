# Umsetzungsplan: Autonome Aufgaben mit Feature-Flag in Einstellungen

## Übersicht

Die bestehende `AutonomAufgabenOptions.Enabled`-Eigenschaft soll als echtes Gating-Flag implementiert werden, das autonome Aufgaben aktivieren/deaktivieren kann. Derzeit wird das Flag nicht abgefragt — Guard-Klauseln fehlen in allen drei kritischen Services. Das Feature wird über einen neuen UI-Schalter in `SettingsViewModel` exponiert und via `AppEinstellungService` in der Datenbank persistiert. Der nicht-autonome Weg ("einfaches Starten") bleibt unabhängig vom Flag funktionsfähig.

---

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| **Feature-Flag-Persistierung** | Dual-Layer: `appsettings.json` für Deployment-Zeit, `AppEinstellung`-DB-Entity für Laufzeit-Schalter via GUI | `IOptions<AutonomAufgabenOptions>` aus DI können nicht zur Laufzeit geändert werden; DB-Layer erlaubt GUI-Kontrolle ohne Neustart |
| **Guard-Klausel-Strategie** | Drei separate Guards (AutonomAufgabeStartService, AutonomAufgabenInitialisierungsService, ProjektleiterAgentService) | Defense in Depth: Jede Einstiegsstelle wird geprüft, unabhängig davon, wer sie aufruft |
| **Fallback-Verhalten** | Fehlertext/Dialogmeldung anstelle von automatischer Fallback-Ausführung | Nutzer weiß, dass Feature deaktiviert ist und kann bewusst auf "Einfach starten" wechseln |
| **Nicht-autonomer Weg** | Völlig unabhängig vom Feature-Flag | EntwicklungsprozessService bleibt neutral und wird von beiden Wegen genutzt — kein Gating nötig |
| **UI-Schalter-Integration** | CheckBox in SettingsViewModel (nach SettingsViewModel-Pattern) | Konsistent mit bestehenden Boolean-Settings (DesignMode, Benachrichtigungsmodus); etabliertes Load/Save-Muster |

---

## Programmabläufe

### Autonome Aufgabe starten (mit Feature-Flag-Gating)

1. `TaskDetailViewModel` zeigt Aufgabe an, prüft `IsAutonomAufgabe` (vorhanden) und `IsAutonomAufgabenEnabled` (neu)
2. Button "Autonome Aufgabe starten" ist sichtbar nur wenn beide Bedingungen true
3. Klick ruft `AutonomAufgabeStartService.StarteAsync()` auf
4. Guard-Klausel prüft `_autonomAufgabenOptions.Value.Enabled`
   - Falls `false`: Gibt Fehlerresultat zurück, Dialog zeigt Meldung "Autonome Aufgaben sind in den Einstellungen deaktiviert."
   - Falls `true`: Öffnet `AutonomAufgabeInitialisierungsDialogViewModel`
5. Dialog lädt verfügbare Branches, Promptvorlagen
6. Bei Submit: Ruft `AutonomAufgabenInitialisierungsService.InitialisiereAsync()` auf
7. Guard-Klausel prüft `_options.Enabled`
   - Falls `false`: Wirft `InvalidOperationException` (sollte durch vorherigen Guard nicht erreichbar sein)
   - Falls `true`: Führt Initialisierung durch (Arbeitsverzeichnis, Klon, `permissions.json`, `state.json`)
8. Nach erfolgreicher Initialisierung: Ruft `ProjektleiterAgentService.StarteAgentAsync()` auf
9. Guard-Klausel prüft `_autonomAufgabenOptions.Value.Enabled`
   - Falls `false`: Wirft `InvalidOperationException` (sollte durch vorherige Guards nicht erreichbar sein)
   - Falls `true`: Startet Agent über `KiAusfuehrungsService`

Beteiligte Klassen: `TaskDetailViewModel`, `AutonomAufgabeStartService`, `AutonomAufgabenInitialisierungsDialogViewModel`, `AutonomAufgabenInitialisierungsService`, `ProjektleiterAgentService`, `KiAusfuehrungsService`

### Feature-Flag über Settings ändern

1. `SettingsViewModel.LadenCommand` wird ausgelöst (z. B. beim Öffnen von Einstellungen oder App-Start)
2. Liest `IsAutonomAufgabenEnabled` aus `AppEinstellungService.GetBoolSettingAsync("autonomeaufgaben.enabled")`, Fallback auf `true`
3. Bindet Property an CheckBox in `SettingsView.xaml`
4. Nutzer wechselt CheckBox-Status
5. Bei Klick auf "Speichern": `SettingsViewModel.SpeichernCommand` wird ausgelöst
6. Ruft `AppEinstellungService.SetBoolSettingAsync("autonomeaufgaben.enabled", IsAutonomAufgabenEnabled)` auf
7. Persistiert neue Einstellung in DB-Entity `AppEinstellung`
8. Beim nächsten App-Neustart oder Refresh wird die neue Einstellung berücksichtigt

Beteiligte Klassen: `SettingsViewModel`, `AppEinstellungService`, `SettingsView` (XAML)

### Einfaches Starten (Fallback, ohne Feature-Flag-Gating)

1. `TaskDetailViewModel` zeigt Button "Einfach starten" (nicht-autonomer Weg)
2. Klick ruft `EntwicklungsprozessService.ProzessStartenUndCliStartenAsync()` auf
3. Repository-Setup: Klon, Branch-Anlage (kein Feature-Flag-Check)
4. CLI-Start via `KiAusfuehrungsService.StartCliAsync()` (kein Feature-Flag-Check)
5. CLI läuft mit KI-Plugin-Ausführung (nicht über Projektleiter-Agent)

Beteiligte Klassen: `TaskDetailViewModel`, `EntwicklungsprozessService`, `KiAusfuehrungsService`

---

## Neue Klassen

Keine neuen Klassen erforderlich. Alle Änderungen sind Erweiterungen bestehender Klassen.

---

## Änderungen an bestehenden Klassen

### `AppEinstellungService` (Service)

- **Neue Konstanten:** `AutonomAufgabenEnabledKey` (String `"autonomeaufgaben.enabled"`) — Standardschlüssel für das Feature-Flag in der Datenbank

### `AutonomAufgabenInitialisierungsService` (Service)

- **Geänderte Methode:** `InitialisiereAsync(Aufgabe, AutonomAufgabeInitialisierungsAnfrage, CancellationToken)`
  - Zu Beginn der Methode: Guard-Klausel `if (!_options.Value.Enabled) { throw new InvalidOperationException("Autonome Aufgaben sind nicht aktiviert."); }`
  - Begründung: Verhindert Initialisierung, wenn Feature deaktiviert ist

### `ProjektleiterAgentService` (Service)

- **Neue Abhängigkeit:** `IOptions<AutonomAufgabenOptions>` (Dependency Injection in Constructor erforderlich) — Um zur Laufzeit das Enabled-Flag zu prüfen
- **Geänderte Methode:** `StarteAgentAsync(AutonomAufgabeKonfiguration, string?, CancellationToken)`
  - Zu Beginn der Methode: Guard-Klausel `if (!_autonomAufgabenOptions.Value.Enabled) { throw new InvalidOperationException("Autonome Aufgaben sind nicht aktiviert."); }`
  - Begründung: Verhindert Agent-Start, wenn Feature deaktiviert ist

### `AutonomAufgabeStartService` (Service)

- **Neue Abhängigkeit:** `IOptions<AutonomAufgabenOptions>` (Dependency Injection in Constructor erforderlich) — Um zur Laufzeit das Enabled-Flag zu prüfen
- **Geänderte Methode:** `StarteAsync(Aufgabe, CancellationToken)`
  - Zu Beginn der Methode: Guard-Klausel:
    ```
    if (!_autonomAufgabenOptions.Value.Enabled)
    {
        return new AutonomAufgabeStartResult(
            aufgabe, 
            "Autonome Aufgaben sind in den Einstellungen deaktiviert.",
            null);
    }
    ```
  - Begründung: Gibt Fehlerresultat zurück (nicht Exception), da dies die UI-Einstiegsstelle ist

### `TaskDetailViewModel` (ViewModel)

- **Neue Abhängigkeit:** `IOptions<AutonomAufgabenOptions>?` (Optional, Dependency Injection in Constructor erforderlich) — Um GUI-Elemente bedingt zu deaktivieren
- **Neue Eigenschaft:** `IsAutonomAufgabenEnabled` (computed `bool`)
  - Rückgabewert: `_autonomAufgabenOptions?.Value.Enabled ?? false`
  - Zweck: Anzeige-Flag für autonome Aufgaben-UI-Elemente
- **Geänderte Eigenschaft:** `ShowAutomatisierungPanel` (computed `bool`)
  - Alte Bedingung: `IsAutonomAufgabe`
  - Neue Bedingung: `IsAutonomAufgabe && IsAutonomAufgabenEnabled`
  - Begründung: Panel wird nur angezeigt, wenn Aufgabe autonom ist UND Feature aktiviert ist

### `SettingsViewModel` (ViewModel)

- **Neue Eigenschaft:** `IsAutonomAufgabenEnabled` (bool mit Setter für Binding, private Backing-Field `_isAutonomAufgabenEnabled`)
  - Initialwert: `true` (Standard)
  - Getter/Setter: Standard Property mit `SetProperty()` (WPF MVVM-Pattern)
- **Geänderte Methode:** `LadenCommand` Handler (Load-Befehl)
  - Nach vorhandener Lade-Logik zusätzlich: 
    ```
    IsAutonomAufgabenEnabled = 
        (await _einstellungService.GetBoolSettingAsync(AppEinstellungService.AutonomAufgabenEnabledKey)) ?? true;
    ```
- **Geänderte Methode:** `SpeichernCommand` Handler (Save-Befehl)
  - Nach vorhandener Speicher-Logik zusätzlich:
    ```
    await _einstellungService.SetBoolSettingAsync(
        AppEinstellungService.AutonomAufgabenEnabledKey, 
        IsAutonomAufgabenEnabled);
    ```

### `SettingsView.xaml` (XAML-View)

- **Neue UI-Elemente:** CheckBox für autonome Aufgaben-Flag
  - Binding: `IsChecked="{Binding IsAutonomAufgabenEnabled, Mode=TwoWay}"`
  - Label/Content: "Autonome Aufgaben aktivieren"
  - Platzierung: Geeignete Stelle in der Settings-Seite (z. B. in einer Gruppe "Automation", neben anderen autonomen Aufgaben-Einstellungen)

---

## Datenbankmigrationen

Keine Migrationen erforderlich. Die `AppEinstellung`-Entity existiert bereits und unterstützt Key-Value-Persistierung. Der neue Schlüssel `"autonomeaufgaben.enabled"` wird bei Bedarf automatisch angelegt.

---

## Validierungsregeln

Keine neuen Validierungsregeln erforderlich. Das Feature-Flag ist eine simple Boolean-Einstellung ohne Abhängigkeiten.

---

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `AutonomAufgabenEnabledKey` (Konstante in `AppEinstellungService`) | `string` | `"autonomeaufgaben.enabled"` | Schlüssel für DB-Persistierung der GUI-Einstellung |
| (Optional) UI-Label in `SettingsView.xaml` | Neue UI-Komponente | — | CheckBox-Label "Autonome Aufgaben aktivieren" |

**Hinweis:** `appsettings.json` Sektion `AutonomAufgaben:Enabled` existiert bereits und wird beibehalten. Die neue DB-Persistierung bietet zusätzliche Laufzeit-Konfigurierbarkeit.

---

## Seiteneffekte und Risiken

- **E2E-Tests für autonome Aufgaben:** Möglicherweise fehlgeschlagen, wenn `AutonomAufgabenOptions.Enabled` beim Test-Setup nicht explizit auf `true` gesetzt ist. Müssen vor Testausführung überprüft werden.
- **Bestehende Unit-Tests für `ProjektleiterAgentService`:** Falls Tests direkt `StarteAgentAsync()` aufrufen, schlagen sie fehl, wenn das Feature-Flag `false` ist. Müssen angepasst werden (Mock/Konfiguration vor Test).
- **Bestehende Integration-Tests:** Tests, die `AutonomAufgabenInitialisierungsService.InitialisiereAsync()` aufrufen, müssen sicherstellen, dass das Flag in der Test-Konfiguration `true` ist.
- **Abhängigkeit zu GUI-Refresh:** Bei Runtime-Änderung des Flags (via Settings speichern) werden Bestands-ViewModels nicht automatisch aktualisiert. Ein App-Neustart oder explizites Refresh ist erforderlich (akzeptabel, da Settings-Änderungen selten zur Laufzeit erfolgen).
- **Fallback-Logik ist explizit:** Wenn Feature deaktiviert ist, können Nutzer nicht mehr auf "Autonome Aufgabe starten"-Button klicken. Sie müssen zum nicht-autonomen Button wechseln — kein automatisches Fallback.

---

## Umsetzungsreihenfolge

1. **Konstante in `AppEinstellungService` definieren**
   - Voraussetzungen: `AppEinstellungService` existiert bereits im Repo
   - Beschreibung: Definiere öffentliche Konstante `AutonomAufgabenEnabledKey = "autonomeaufgaben.enabled"` in `AppEinstellungService`

2. **Guard-Klausel in `AutonomAufgabenInitialisierungsService.InitialisiereAsync()` hinzufügen**
   - Voraussetzungen: `IOptions<AutonomAufgabenOptions>` ist bereits injiziert in diesem Service
   - Beschreibung: Zu Beginn der Methode Guard-Klausel einfügen: Wenn `_options.Value.Enabled == false`, werfe `InvalidOperationException` mit Nachricht "Autonome Aufgaben sind nicht aktiviert."

3. **Dependency Injection in `ProjektleiterAgentService` hinzufügen**
   - Voraussetzungen: `IOptions<AutonomAufgabenOptions>` ist als NuGet/Framework-Typ verfügbar
   - Beschreibung: Constructor-Parameter `IOptions<AutonomAufgabenOptions> autonomAufgabenOptions` hinzufügen, privates Feld `_autonomAufgabenOptions` initialisieren

4. **Guard-Klausel in `ProjektleiterAgentService.StarteAgentAsync()` hinzufügen**
   - Voraussetzungen: Schritt 3 muss abgeschlossen sein
   - Beschreibung: Zu Beginn der Methode Guard-Klausel einfügen: Wenn `_autonomAufgabenOptions.Value.Enabled == false`, werfe `InvalidOperationException` mit Nachricht "Autonome Aufgaben sind nicht aktiviert."

5. **Dependency Injection in `AutonomAufgabeStartService` hinzufügen**
   - Voraussetzungen: `IOptions<AutonomAufgabenOptions>` ist als NuGet/Framework-Typ verfügbar
   - Beschreibung: Constructor-Parameter `IOptions<AutonomAufgabenOptions> autonomAufgabenOptions` hinzufügen, privates Feld `_autonomAufgabenOptions` initialisieren

6. **Guard-Klausel in `AutonomAufgabeStartService.StarteAsync()` hinzufügen**
   - Voraussetzungen: Schritt 5 muss abgeschlossen sein
   - Beschreibung: Zu Beginn der Methode Guard-Klausel einfügen: Wenn `_autonomAufgabenOptions.Value.Enabled == false`, gebe `AutonomAufgabeStartResult` mit Fehlertext "Autonome Aufgaben sind in den Einstellungen deaktiviert." zurück (nicht Exception)

7. **Dependency Injection in `TaskDetailViewModel` hinzufügen**
   - Voraussetzungen: `IOptions<AutonomAufgabenOptions>` ist als NuGet/Framework-Typ verfügbar
   - Beschreibung: Constructor-Parameter `IOptions<AutonomAufgabenOptions>? autonomAufgabenOptions = null` hinzufügen (optional), privates Feld `_autonomAufgabenOptions` initialisieren

8. **Neue Property `IsAutonomAufgabenEnabled` in `TaskDetailViewModel` hinzufügen**
   - Voraussetzungen: Schritt 7 muss abgeschlossen sein
   - Beschreibung: Computed Property `IsAutonomAufgabenEnabled` hinzufügen, die `_autonomAufgabenOptions?.Value.Enabled ?? false` zurückgibt

9. **Property `ShowAutomatisierungPanel` in `TaskDetailViewModel` anpassen**
   - Voraussetzungen: Schritt 8 muss abgeschlossen sein
   - Beschreibung: Bedingung von `IsAutonomAufgabe` zu `IsAutonomAufgabe && IsAutonomAufgabenEnabled` ändern

10. **Neue Property `IsAutonomAufgabenEnabled` in `SettingsViewModel` hinzufügen**
    - Voraussetzungen: `SettingsViewModel` existiert bereits, nutzt `SetProperty()`-Muster
    - Beschreibung: Privates Backing-Field `_isAutonomAufgabenEnabled = true`, Property mit Getter/Setter (WPF MVVM-Pattern)

11. **`SettingsViewModel.LadenCommand`-Handler anpassen**
    - Voraussetzungen: Schritt 10 muss abgeschlossen sein, `AppEinstellungService.GetBoolSettingAsync()` ist verfügbar
    - Beschreibung: Nach vorhandener Lade-Logik zusätzliche Zeile einfügen, die `IsAutonomAufgabenEnabled` aus `AppEinstellungService` lädt (mit Fallback `true`)

12. **`SettingsViewModel.SpeichernCommand`-Handler anpassen**
    - Voraussetzungen: Schritt 10 muss abgeschlossen sein, `AppEinstellungService.SetBoolSettingAsync()` ist verfügbar
    - Beschreibung: Nach vorhandener Speicher-Logik zusätzliche Zeile einfügen, die `IsAutonomAufgabenEnabled` in `AppEinstellungService` speichert

13. **UI-Schalter in `SettingsView.xaml` hinzufügen**
    - Voraussetzungen: `SettingsView.xaml` existiert bereits, `SettingsViewModel` Property ist vorhanden (Schritt 10)
    - Beschreibung: CheckBox mit Binding `IsChecked="{Binding IsAutonomAufgabenEnabled, Mode=TwoWay}"` und Label "Autonome Aufgaben aktivieren" in geeigneter Stelle (z. B. nächst zu anderen autonomen Aufgaben-Einstellungen) hinzufügen

14. **Unit-Tests schreiben für Guard-Klauseln**
    - Voraussetzungen: Test-Infrastruktur existiert (TestFactory, DbContextFactory, Mocks)
    - Beschreibung: 
      - `AutonomAufgabenInitialisierungsServiceTests.WhenEnabledFlagIsFalse_InitialisiereAsync_ShouldThrow()` — Prüft, dass `InvalidOperationException` geworfen wird
      - `ProjektleiterAgentServiceTests.WhenEnabledFlagIsFalse_StarteAgentAsync_ShouldThrow()` (neue Testklasse falls nicht vorhanden)
      - `AutonomAufgabeStartServiceTests.WhenEnabledFlagIsFalse_StarteAsync_ShouldReturnError()` (neue Testklasse falls nicht vorhanden)

15. **Integration-Tests schreiben für Enabled-Flag-Szenarien**
    - Voraussetzungen: Integration-Test-Infrastruktur existiert
    - Beschreibung:
      - `EntwicklungsprozessServiceTests.WhenFeatureFlagDisabled_ShouldUseFallbackPath()` — Prüft, dass einfacher Weg weiterhin funktioniert, auch wenn Flag falsch ist

16. **E2E-Tests schreiben für UI-Sichtbarkeit und Fallback-Verhalten**
    - Voraussetzungen: E2E-Test-Infrastruktur (FlaUI) existiert, `E2E_AutonomAufgabenInitialisierung.cs` existiert
    - Beschreibung:
      - `E2E_AutonomAufgabenInitialisierung.WhenAutonomAufgabenDisabled_UIElementsShouldNotBeDisplayed()` — Prüft, dass Dialog/Buttons nicht sichtbar sind
      - `E2E_AutonomAufgabenInitialisierung.WhenAutonomAufgabenDisabled_SimpleStartButtonShouldBeAvailable()` — Prüft, dass Fallback-Button sichtbar ist
      - `E2E_AutonomAufgabenAgentExecution.WhenAutonomAufgabenDisabled_AgentShouldNotStart()` — Prüft, dass Agent-Prozess nicht startet

17. **Bestehende Unit-Tests überprüfen und ggf. anpassen**
    - Voraussetzungen: Schritte 1–6 muss abgeschlossen sein
    - Beschreibung: Tests für `AutonomAufgabenInitialisierungsService`, `ProjektleiterAgentService` überprüfen, ob sie das Feature-Flag in der Test-Konfiguration auf `true` setzen (sonst Fehlschlag durch neue Guards)

18. **Bestehende E2E-Tests überprüfen und ggf. anpassen**
    - Voraussetzungen: Schritte 1–13 muss abgeschlossen sein
    - Beschreibung: E2E-Tests für autonome Aufgaben überprüfen, ob sie fehlschlagen durch neue Guards/UI-Bedingungen. Ggf. Test-Setup anpassen (Flag setzen vor Test)

---

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `WhenEnabledFlagIsFalse_InitialisiereAsync_ShouldThrow()` | `AutonomAufgabenInitialisierungsServiceTests` | Guard-Klausel wirft `InvalidOperationException`, wenn `_options.Value.Enabled == false` |
| `WhenEnabledFlagIsTrue_InitialisiereAsync_ShouldSucceed()` | `AutonomAufgabenInitialisierungsServiceTests` | Normale Ausführung, wenn Flag `true` (Baseline-Test um Regression zu verhindern) |
| `WhenEnabledFlagIsFalse_StarteAgentAsync_ShouldThrow()` | `ProjektleiterAgentServiceTests` (neu oder in bestehender Testklasse) | Guard-Klausel wirft `InvalidOperationException` |
| `WhenEnabledFlagIsTrue_StarteAgentAsync_ShouldSucceed()` | `ProjektleiterAgentServiceTests` | Normale Ausführung, wenn Flag `true` |
| `WhenEnabledFlagIsFalse_StarteAsync_ShouldReturnError()` | `AutonomAufgabeStartServiceTests` (neu oder in bestehender Testklasse) | Guard-Klausel gibt `AutonomAufgabeStartResult` mit Fehlertext zurück, nicht null/Exception |
| `WhenEnabledFlagIsTrue_StarteAsync_ShouldShowDialog()` | `AutonomAufgabeStartServiceTests` | Normale Ausführung (Dialog wird geöffnet) |
| `WhenFeatureFlagDisabled_ShouldUseFallbackPath()` | `EntwicklungsprozessServiceTests` (Integration-Test) | `ProzessStartenUndCliStartenAsync()` funktioniert auch wenn autonome Aufgaben deaktiviert sind |
| `IsAutonomAufgabenEnabled_WhenOptionsIsNull_ShouldReturnFalse()` | `TaskDetailViewModelTests` (neu oder in bestehender Testklasse) | Property gibt `false` zurück, wenn `_autonomAufgabenOptions == null` |
| `IsAutonomAufgabenEnabled_WhenOptionsFalse_ShouldReturnFalse()` | `TaskDetailViewModelTests` | Property gibt `false` zurück, wenn Flag `false` |
| `ShowAutomatisierungPanel_ShouldConsiderBothConditions()` | `TaskDetailViewModelTests` | Panel wird nur gezeigt, wenn `IsAutonomAufgabe && IsAutonomAufgabenEnabled` |
| `LoadCommand_ShouldLoadAutonomAufgabenEnabledFlag()` | `SettingsViewModelTests` (neu oder in bestehender Testklasse) | `LadenCommand` lädt `IsAutonomAufgabenEnabled` aus `AppEinstellungService` |
| `SaveCommand_ShouldPersistAutonomAufgabenEnabledFlag()` | `SettingsViewModelTests` | `SpeichernCommand` speichert `IsAutonomAufgabenEnabled` via `AppEinstellungService` |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `AutonomAufgabenInitialisierungsServiceTests.*` (alle Tests) | Neue Guard-Klausel erfordert, dass `IOptions<AutonomAufgabenOptions>.Value.Enabled == true` in der Test-Konfiguration gesetzt ist, sonst alle Tests fehlschlagen. Test-Setup (Factory, Mock) muss überprüft werden. |
| `ProjektleiterAgentServiceTests.*` (alle Tests, falls vorhanden) | Neue Dependency Injection von `IOptions<AutonomAufgabenOptions>` und Guard-Klausel. Tests müssen Dependency bereitstellen oder mocken. |
| `AutonomAufgabeStartServiceTests.*` (falls vorhanden) | Neue Dependency Injection von `IOptions<AutonomAufgabenOptions>` und Guard-Klausel. Tests müssen Dependency bereitstellen oder mocken. |
| `E2E_AutonomAufgabenInitialisierung.cs` (bestehende Szenarien) | Neue UI-Bedingungen: Buttons sind nur sichtbar, wenn `IsAutonomAufgabenEnabled`. Test-Setup muss sicherstellen, dass Feature-Flag aktiviert ist, sonst Fehler "Element not found". |
| `E2E_AutonomAufgabenAgentExecution.cs` (bestehende Szenarien) | Wie oben: Test-Setup muss Feature-Flag aktivieren. |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| **Happy Path: Feature aktiviert** | `E2E_AutonomAufgabenInitialisierung.cs` | Wenn `IsAutonomAufgabenEnabled == true`: Button "Autonome Aufgabe starten" sichtbar, Dialog öffnet, Initialisierung erfolgreich |
| **Feature deaktiviert: UI-Elemente versteckt** | `E2E_AutonomAufgabenInitialisierung.cs` | Wenn `IsAutonomAufgabenEnabled == false`: Button "Autonome Aufgabe starten" nicht sichtbar oder deaktiviert |
| **Feature deaktiviert: Fallback-Button verfügbar** | `E2E_AutonomAufgabenInitialisierung.cs` | Wenn `IsAutonomAufgabenEnabled == false`: Button "Einfach starten" ist sichtbar und funktionsfähig |
| **Feature deaktiviert: Agent startet nicht** | `E2E_AutonomAufgabenAgentExecution.cs` | Wenn `IsAutonomAufgabenEnabled == false` und Agent wird trotzdem gestartet (sollte nicht möglich sein): Guard wirft Exception oder verhindert Start |
| **Settings: Feature-Flag UI-Schalter** | Neue E2E-Test (z. B. `E2E_SettingsFeatureFlags.cs` oder in `E2E_Settings.cs`) | Nutzer kann CheckBox "Autonome Aufgaben aktivieren" in Settings toggle, Änderung wird persistiert |

**Bestehende E2E-Tests anpassen:**
- `E2E_AutonomAufgabenInitialisierung.cs`: Sicherstellen, dass alle Tests mit aktiviertem Feature-Flag laufen (Setup überprüfen)
- `E2E_AutonomAufgabenAgentExecution.cs`: Wie oben

---

## Offene Punkte

Keine offenen Punkte. Alle fachlichen Entscheidungen sind durch die Anforderung und Bestandsaufnahme geklärt:
- **UI-Integration:** Feature-Flag wird über SettingsViewModel exponiert ✓
- **Fallback-Verhalten:** Fehler/Dialog-Meldung, kein automatisches Fallback ✓
- **Persistenz:** Neue DB-Ebene via `AppEinstellungService` zusätzlich zu `appsettings.json` ✓
- **Nicht-autonomer Weg:** Unabhängig vom Flag, kein Gating erforderlich ✓
- **Dokumentation:** Außerhalb dieses Plans (Changelog, README optional) ✓

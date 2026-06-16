# Umsetzungsplan: Aufgabenworkflow Optimierung

## Übersicht

Der Aufgabenworkflow wird vereinfacht durch direkte Übergänge von Status `Neu` zu `Gestartet` mit kombiniertem Repository-Klone und CLI-Start. Die Zwischenstatus `ArbeitsverzeichnisEingerichtet` und `InArbeit` werden entfernt. Ein neuer Dialog mit optionaler Projekt-Level-Speicherung ermöglicht die flexible KI-Plugin-Auswahl, und die UI wird mit neuen Befehlen ausgestattet. Betroffene Bereiche: Datenmodell (Enum-Anpassung), Service-Layer (kombinierte Prozesslogik und Dialog-Integration), ViewModels (neue Commands), UI-Komponenten (Dialog, neue Buttons) und Tests (Unit, E2E).

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| **Projekt-Level Plugin-Speicherung** | Scoped-Key in `AppEinstellung` (PoEAA: Service Locator / Global Registry mit lokalem Scope) | Konsistent mit bestehender `PluginDefaultSettingsService`-Architektur; Projekt-ID als Scope-Präfix (z. B. `plugins.default.project.{ProjektId}.KiAutomation`) ermöglicht einfache Abfrage ohne neue Tabelle |
| **Kombinierter Start-Ablauf** | Neue Methode `ProzessStartenUndCliStartenAsync` in `EntwicklungsprozessService` (Orchestration Service Pattern) | Entkopplung von Repo-Setup und CLI-Start bleibt erhalten, neue Methode koordiniert beide; einfachere Fehlerbehandlung und transaktionale Konsistenz |
| **Plugin-Dialog-Integration** | Eigene neue Klasse `PluginSelectionDialogService` (PoEAA: Facade Pattern) | Entkoppelt Dialog-Technologie (WPF) von Service-Logik; ViewModel bleibt unabhängig von UI-Implementierungsdetails; Dialog-Service nutzt `IDialogService` als Abstraktionsfläche |
| **Automatischer CLI-Neustart** | Logik in `TaskDetailViewModel.LadenAsync` mit explicititem `EnsureCliRunning`-Flag | Keine separate State-Machine erforderlich; einfache, nachvollziehbare Kontrollfluss-Verästelung; Status `Gestartet` ist "sollte laufen" State |
| **Plugin-Wechsel mit Prozess-Neustarts** | `KiAusfuehrungsService.StopCliAsync` gefolgt von `StartCliAsync` (Restart-as-Sequence Pattern) | Keine Nebenläufigkeitsrisiken; klare Fehlerbehandlung (Fehler beim Stop sperrt Restart nicht); bestehende Prozess-Verwaltung wird wiederverwendet |

## Programmabläufe

### Aufgabe starten (von Neu zu Gestartet mit kombiniertem Klone und CLI-Start)

1. Benutzer klickt `StartenCommand` im Ribbon der Aufgabendetailansicht
2. `TaskDetailViewModel.StartenAsync` wird aufgerufen
3. Prüfung: Status muss `Neu` sein, sonst Fehler
4. `PluginSelectionService.ResolveDevelopmentAutomationPluginAsync` wird mit Projekt-Kontext aufgerufen
5. Falls Plugin noch nicht für Projekt gespeichert: `PluginSelectionDialogService.ShowPluginSelectionDialogAsync` öffnet Modal-Dialog
6. Benutzer wählt Plugin; Checkbox "Für dieses Projekt verwenden" wird ausgewertet
7. Falls Checkbox aktiviert: `PluginDefaultSettingsService.SaveProjectDefaultPluginPrefixAsync(projektId, pluginPrefix)` speichert Projekt-Default
8. `EntwicklungsprozessService.ProzessStartenUndCliStartenAsync` wird aufgerufen mit Aufgabe-ID, Repository-URL, Plugin-Prefix
   - Repository wird geklont, Branch erstellt
   - Status wird auf `Gestartet` gesetzt (nicht zwischendurch auf `ArbeitsverzeichnisEingerichtet`)
   - CLI wird sofort mit gewähltem Plugin gestartet
   - Im Fehlerfall: Rollback Status, Cleanup Klon-Verzeichnis
9. Neue Running-CLI wird eingebettet; `IsCliRunning` wird auf true gesetzt
10. Benutzer sieht running CLI-Session

Beteiligte Klassen/Komponenten: `TaskDetailViewModel`, `PluginSelectionService`, `PluginSelectionDialogService`, `EntwicklungsprozessService`, `KiAusfuehrungsService`, `AufgabeService`, `PluginDefaultSettingsService`

### Plugin-Dialog-Anzeige und Speicherung

1. Wenn `ResolveDevelopmentAutomationPluginAsync` aufgerufen wird (z. B. beim Starten), prüft Service:
   - Hat Aufgabe bereits `KiPluginPrefix` gesetzt? → Nutze diesen
   - Nicht gesetzt: Prüfe Projekt-Level-Default mit `PluginDefaultSettingsService.GetProjectDefaultPluginPrefixAsync(projektId)`
   - Nicht gefunden: Prüfe globalen Default mit `PluginDefaultSettingsService.GetDefaultPluginPrefixAsync(PluginType.KiAutomation)`
   - Nicht gefunden: `PluginSelectionDialogService.ShowPluginSelectionDialogAsync` zeigt Dialog
2. Dialog stellt verfügbare Plugins dar; Benutzer selektiert eines
3. Checkbox "Für dieses Projekt verwenden" wird geprüft:
   - Falls aktiviert: `PluginDefaultSettingsService.SaveProjectDefaultPluginPrefixAsync(projektId, ausgewaehltesPlugin)`
   - Falls deaktiviert: Nur für diese Aufgabe verwenden (über `AufgabeService.UpdateAsync` mit `KiPluginPrefix`)
4. Dialog schließt, Plugin wird zurückgegeben

Beteiligte Klassen/Komponenten: `PluginSelectionService`, `PluginSelectionDialogService`, `PluginDefaultSettingsService`, `AufgabeService`

### Plugin-Wechsel bei laufender CLI (PluginAendernCommand)

1. Benutzer klickt `PluginAendernCommand` im Ribbon
2. `TaskDetailViewModel.PluginWechselAsync` wird aufgerufen
3. `PluginSelectionDialogService.ShowPluginSelectionDialogAsync` zeigt Dialog mit aktuellem Plugin vorselektiert
4. Benutzer wählt neues Plugin
5. `KiAusfuehrungsService.StopCliAsync` wird aufgerufen (wartet bis Prozess beendet oder 5s timeout)
6. Falls `StopCliAsync` fehlschlägt: Fehler anzeigen, Dialog offenlassen, Neustart nicht durchführen
7. Falls erfolgreich: `KiAusfuehrungsService.StartCliAsync` wird mit neuem Plugin aufgerufen
8. Projekt-Level-Default wird mit `PluginDefaultSettingsService.SaveProjectDefaultPluginPrefixAsync` aktualisiert (falls Checkbox aktiviert)
9. Neue CLI wird eingebettet

Beteiligte Klassen/Komponenten: `TaskDetailViewModel`, `PluginSelectionDialogService`, `KiAusfuehrungsService`, `PluginDefaultSettingsService`

### Automatischer CLI-Neustart bei Aufgabe laden

1. `TaskDetailViewModel.LadenAsync` wird aufgerufen (z. B. Navigation zu bestehender Aufgabe)
2. Aufgabe wird mit Status geladen
3. Prüfung: Status == `Gestartet` && `KiAusfuehrungsService.IsRunning(aufgabeId) == false` ?
4. Falls wahr: `KiAusfuehrungsService.StartCliAsync` wird mit gespeichertem Plugin aufgerufen (oder über `PluginSelectionService` aufgelöst)
5. CLI wird eingebettet

Beteiligte Klassen/Komponenten: `TaskDetailViewModel`, `KiAusfuehrungsService`, `PluginSelectionService`, `AufgabeService`

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `PluginSelectionDialogService` | Service / Facade | Kapselt Dialog-Anzeige für Plugin-Wahl mit WPF-Technologie-Details; gibt `PluginSelectionResult` zurück |
| `PluginSelectionResult` | Value Object / DTO | Ergebnis des Plugin-Dialogs: `SelectedPluginPrefix`, `SaveAsProjectDefault` (bool) |
| `PluginSelectionDialog` | WPF Dialog / UserControl | UI für Plugin-Auswahl mit Dropdown, Checkbox "Für Projekt verwenden", OK/Abbrechen-Buttons |
| `PluginSelectionDialogViewModel` | ViewModel | Logik für Dialog: verfügbare Plugins, Benutzerauswahl, Checkbox-State |

## Änderungen an bestehenden Klassen

### `AufgabeStatus` (Enum)

- **Entfernte Werte:** `ArbeitsverzeichnisEingerichtet`, `InArbeit`
- **Beibehaltene Werte:** `Neu`, `Gestartet`, `Wartend`, `Beendet`, `Archiviert`

### `AufgabeService`

- **Angepasste Methoden:**
  - `StartenAsync(id, branchName, lokalerKlonPfad, ct)` — Setzt Status auf `Gestartet` statt `ArbeitsverzeichnisEingerichtet`
  - `ValidateStatusTransition(current, next)` — Neue erlaubte Übergänge: `Neu` → `Gestartet`, `Gestartet` → `Wartend`, `Gestartet` → `Beendet`, usw. (entfernt `ArbeitsverzeichnisEingerichtet`, `InArbeit` Übergänge)
- **Neue Methoden:** (keine neuen Methoden erforderlich, bestehende genügen)

### `EntwicklungsprozessService`

- **Neue Methoden:**
  - `ProzessStartenUndCliStartenAsync(aufgabeId, repositoryUrl, basisBranchName, kiPluginPrefix, ct)` — Kombiniert `ProzessStartenAsync` + CLI-Start; setzt Status direkt auf `Gestartet`, startet CLI, Fehlerrollback
- **Angepasste Methoden:**
  - `ProzessStartenAsync` — Weiterhin vorhanden, aber künftig optional (wird durch neue Kombinationsmethode ersetzt wo möglich)

### `PluginSelectionService`

- **Neue Methoden:**
  - `ResolveDevelopmentAutomationPluginWithProjectScopeAsync(aufgabeId, projektId, showDialog, ct)` — Erweiterte Auflösung mit Projekt-Kontext und optionalem Dialog
- **Angepasste Methoden:**
  - `ResolveDevelopmentAutomationPluginAsync` — Bleibt unverändert für bestehende Aufrufe

### `PluginDefaultSettingsService`

- **Neue Methoden:**
  - `GetProjectDefaultPluginPrefixAsync(projektId, pluginType, ct)` — Lädt Projekt-spezifischen Default aus AppEinstellung mit Key `plugins.default.project.{ProjektId}.{PluginType}`
  - `SaveProjectDefaultPluginPrefixAsync(projektId, pluginType, pluginPrefix, ct)` — Speichert Projekt-spezifischen Default in AppEinstellung

### `KiAusfuehrungsService`

- **Neue Events / Änderungen:** Keine, bestehende `StopCliAsync` + `StartCliAsync` werden direkt kombiniert

### `TaskDetailViewModel`

- **Neue Properties:**
  - (keine neuen Properties, existierende genügen)
- **Neue Commands:**
  - `StartenCommand` — Ruft `StartenAsync` auf; CanExecute: `Status == Neu && !IsCliRunning`
  - `PluginAendernCommand` — Ruft `PluginWechselAsync` auf; CanExecute: `Status ∈ {Gestartet, Wartend, InArbeit} && IsCliRunning`
- **Neue Methoden:**
  - `StartenAsync()` — Kombinierter Start-Ablauf (Klone + Dialog + CLI-Start)
  - `PluginWechselAsync()` — Plugin-Dialog + Prozess-Neustarts
  - `LadenAsync` — Ergänzung: Automatischer CLI-Neustart bei Status `Gestartet` ohne laufenden Prozess
- **Geänderte Methoden:**
  - `LadenAsync` — Zusätzliche Prüfung und automatischer CLI-Neustart falls erforderlich
  - `StatusGestartetSetzenCommand` — Wird durch `StartenCommand` ersetzt (oder als nicht sichtbarer Fallback für Legacy-Fälle beibehalten)

### `IDialogService`

- **Neue Methode:**
  - `ShowPluginSelectionDialogAsync(availablePlugins, currentSelection, projektId, ct)` — Zeigt Plugin-Wahledialog

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `20260610000001_UpdateAufgabeStatusEnum` (bereits vorhanden) | Aufgaben.Status | Migriert existierende Aufgaben: `ArbeitsverzeichnisEingerichtet` → `Gestartet`, `InArbeit` → `Gestartet` (Annahme: Aufgaben waren in Bearbeitung) |

**Zusätzliche Migrations-Überlegungen:**
- Falls alte Status-Werte in `AufgabeStatus` Enum-Spalte verbleiben: EF Core wird beim nächsten Build wegen Enum-Mismatch warnen. Migration muss alle existierenden Zeilen aktualisieren.
- Test: Aufgaben in Status `ArbeitsverzeichnisEingerichtet` müssen prüfbar zu `Gestartet` migriert werden, ohne Daten zu verlieren.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `AufgabeStatus` (Enum-Übergang) | `ValidateStatusTransition` prüft erlaubte Übergänge; neue Übergänge: `Neu` → `Gestartet` | `InvalidStatusTransitionException` bei ungültigem Übergang |
| `EntwicklungsprozessService.ProzessStartenUndCliStartenAsync` (Parameter) | `aufgabeId` muss existieren, Status muss `Neu` sein, `repositoryUrl` muss nicht leer sein, `kiPluginPrefix` muss gültig sein | Fehler mit Rollback: Status zurücksetzen, Klon-Verzeichnis löschen |
| `PluginSelectionDialogService.ShowPluginSelectionDialogAsync` (Dialog-Resultat) | Nutzer muss Plugin selektieren; Dialog-Abbruch ist erlaubt | Rückgabe `null` oder `SelectedPluginPrefix == null` signalisiert Abbruch |

## Konfigurationsänderungen

Keine neuen Konfigurationseinträge in `appsettings.json` erforderlich. Projekt-Level Plugin-Speicherung erfolgt über bestehende `AppEinstellung`-Tabelle mit Scoped-Keys.

## Seiteneffekte und Risiken

- **Enum-Wert-Entfernung**: Jeder Code, der noch auf `AufgabeStatus.ArbeitsverzeichnisEingerichtet` oder `.InArbeit` verweist, wird zur Compile-Zeit fehlen. Suche erforderlich.
- **Status-Transitions-Validierung**: Beendete oder archivierte Aufgaben könnten in alten Status-Werten "hängen". Migration muss alle existierenden Zeilen aktualisieren; siehe Datenbankmigrationen.
- **Task-Detail-Panel UI**: Button-Beschriftungen und Sichtbarkeit ändern sich (neue `StartenCommand`, alte `CliStartenCommand` + `StatusGestartetSetzenCommand` werden durch neue kombiniert). Ribbon-Definition muss angepasst werden.
- **E2E-Tests für Navigation**: Bestehende Tests, die auf alte Befehle verweisen (`CliStartenCommand`, `StatusGestartetSetzenCommand` separat), müssen angepasst werden.
- **Recovery bei Prozess-Fehler**: Status `Gestartet` ohne Arbeitsverzeichnis (z. B. nach Festplattenausfall) ist ein Recovery-Fall. Fehlerbehandlung sollte Benutzer auffordern, Aufgabe zu archivieren oder erneut zu starten.

## Umsetzungsreihenfolge

1. **Enum-Anpassung: `AufgabeStatus` vereinfachen**
   - Voraussetzungen: Keine
   - Beschreibung: `ArbeitsverzeichnisEingerichtet` und `InArbeit` aus Enum entfernen; bleibt: `Neu`, `Gestartet`, `Wartend`, `Beendet`, `Archiviert`

2. **Datenbankmigrationen ausführen / prüfen**
   - Voraussetzungen: Enum angepasst (Schritt 1)
   - Beschreibung: Existierende Migration `20260610000001_UpdateAufgabeStatusEnum` validieren, dass alle alten Status zu `Gestartet` migriert werden. Falls fehlend: neue Migration anlegen.

3. **Status-Transition-Validierung anpassen: `AufgabeService.ValidateStatusTransition`**
   - Voraussetzungen: Enum angepasst (Schritt 1), Migrationen validiert (Schritt 2)
   - Beschreibung: Neue erlaubte Übergänge: `Neu` → `Gestartet` (direkt), `Gestartet` → `Wartend`, `Gestartet` → `Beendet`, usw. Tests muss überprüft werden.

4. **Service-Klassen für Dialog vorbereiten: `PluginDefaultSettingsService` erweitern**
   - Voraussetzungen: Keine
   - Beschreibung: Neue Methoden `GetProjectDefaultPluginPrefixAsync`, `SaveProjectDefaultPluginPrefixAsync` implementieren; nutzen bestehende `AppEinstellung`-Abfragen mit scoped Key.

5. **Dialog Service und Dialog-Komponenten anlegen**
   - Voraussetzungen: Keine
   - Beschreibung: Neue Klassen `PluginSelectionDialogService`, `PluginSelectionResult` anlegen; neue WPF-Komponenten `PluginSelectionDialog.xaml`, `PluginSelectionDialog.xaml.cs`, `PluginSelectionDialogViewModel` erstellen.

6. **PluginSelectionService erweitern: Projekt-Kontext unterstützen**
   - Voraussetzungen: `PluginDefaultSettingsService` erweitert (Schritt 4), Dialog Service angelegt (Schritt 5)
   - Beschreibung: Neue Methode `ResolveDevelopmentAutomationPluginWithProjectScopeAsync` mit Fallback-Kette: Aufgaben-Plugin → Projekt-Default → Global-Default → Dialog.

7. **EntwicklungsprozessService: Kombinierte Methode anlegen**
   - Voraussetzungen: Enum angepasst (Schritt 1), Status-Transitions angepasst (Schritt 3), PluginSelectionService erweitert (Schritt 6)
   - Beschreibung: Neue Methode `ProzessStartenUndCliStartenAsync` implementieren; koordiniert Klone + Status-Update auf `Gestartet` + CLI-Start in einer Transaktion; Fehlerrollback.

8. **AufgabeService.StartenAsync anpassen**
   - Voraussetzungen: Enum angepasst (Schritt 1), Status-Transitions angepasst (Schritt 3)
   - Beschreibung: `StartenAsync` setzt Status direkt auf `Gestartet` statt `ArbeitsverzeichnisEingerichtet`.

9. **TaskDetailViewModel erweitern: Neue Commands anlegen**
   - Voraussetzungen: Dialog Service angelegt (Schritt 5), PluginSelectionService erweitert (Schritt 6), EntwicklungsprozessService erweitert (Schritt 7)
   - Beschreibung: Neue `RelayCommand` Properties `StartenCommand`, `PluginAendernCommand`; neue `async` Methoden `StartenAsync`, `PluginWechselAsync` mit Dialog-Integration und Fehlerbehandlung.

10. **TaskDetailViewModel.LadenAsync: Automatischer CLI-Neustart**
    - Voraussetzungen: ViewModel erweitert (Schritt 9)
    - Beschreibung: Nach Laden der Aufgabe: Prüfung Status == `Gestartet` && !`IsCliRunning` → automatischen CLI-Start aufrufen.

11. **UI-Komponenten aktualisieren: Ribbon-Menü und neue Dialog**
    - Voraussetzungen: ViewModel erweitert (Schritt 9), Dialog Komponenten angelegt (Schritt 5)
    - Beschreibung: `TaskDetailView.xaml` Ribbon-Definition: neue Buttons für `StartenCommand`, `PluginAendernCommand`; alte Buttons für `StatusGestartetSetzenCommand` + `CliStartenCommand` entfernen oder deaktivieren. Dialog-Komponente in App-Ressourcen registrieren.

12. **Fehlerbehandlung und UI-Feedback**
    - Voraussetzungen: Alle Service- und ViewModel-Anpassungen (Schritte 3-10)
    - Beschreibung: Fehlerbehandlung in `StartenAsync`, `PluginWechselAsync`, `ProzessStartenUndCliStartenAsync` mit Benutzer-Feedback (Error MessageBox, FehlerMeldung Property, Command-Deaktivierung).

13. **Unit Tests anpassen und erweitern**
    - Voraussetzungen: Enum angepasst (Schritt 1), Services angepasst (Schritte 3-7), ViewModel erweitert (Schritte 9-10)
    - Beschreibung:
        - `AufgabeStatusTransitionTests` — neue Test-Cases für direkte `Neu` → `Gestartet` Übergänge
        - `AufgabeServiceTests` — Tests für angepasste `StartenAsync` Methode
        - `EntwicklungsprozessServiceTests` — neue Tests für `ProzessStartenUndCliStartenAsync` (erfolgreicher Ablauf, Fehler mit Rollback)
        - `PluginDefaultSettingsServiceTests` — neue Tests für Projekt-Level-Speicherung und -Abfrage
        - `TaskDetailViewModelTests` — neue Tests für `StartenCommand`, `PluginAendernCommand`, automatischen CLI-Neustart

14. **E2E-Tests implementieren**
    - Voraussetzungen: Alle Service-, ViewModel-, UI-Änderungen abgeschlossen (Schritte 1-12)
    - Beschreibung: Siehe Abschnitt "E2E-Tests (Pflicht)" — 6 neue E2E-Szenarien mit vollständiger UI-Interaktion, Repository-Setup, Dialog-Handling, Prozess-Verifikation.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `TestStatusTransitions_NeuToGestartet_Direct_Succeeds` | `AufgabeStatusTransitionTests` | Direkter Übergang `Neu` → `Gestartet` ist erlaubt |
| `TestStatusTransitions_OldArbeitsverzeichnisStatus_Removed` | `AufgabeStatusTransitionTests` | `ArbeitsverzeichnisEingerichtet` kann nicht mehr verwendet werden (Enum-Fehler) |
| `TestStatusTransitions_OldInArbeitStatus_Removed` | `AufgabeStatusTransitionTests` | `InArbeit` kann nicht mehr verwendet werden (Enum-Fehler) |
| `TestStatusValidation_GestartetToWartend_IsAllowed` | `AufgabeStatusTransitionTests` | `Gestartet` → `Wartend` ist erlaubt |
| `TestStatusValidation_GestartetToBeendet_IsAllowed` | `AufgabeStatusTransitionTests` | `Gestartet` → `Beendet` ist erlaubt |
| `TestStartenAsync_UpdatesStatusToGestartet` | `AufgabeServiceTests` | `AufgabeService.StartenAsync` setzt Status auf `Gestartet` (nicht auf alten Status) |
| `TestProzessStartenUndCliStartenAsync_Success` | `EntwicklungsprozessServiceTests` | Kombinierte Klone + CLI-Start setzt Status auf `Gestartet`, CLI läuft |
| `TestProzessStartenUndCliStartenAsync_RepositoryCloneFails_RollbackStatus` | `EntwicklungsprozessServiceTests` | Falls Klone fehlschlägt: Status bleibt `Neu`, Klone-Verzeichnis wird gelöscht |
| `TestProzessStartenUndCliStartenAsync_CliStartFails_RollbackStatus` | `EntwicklungsprozessServiceTests` | Falls CLI-Start fehlschlägt: Status wird zurückgesetzt, Klone-Verzeichnis gelöscht |
| `TestGetProjectDefaultPluginPrefix_ReturnsStoredValue` | `PluginDefaultSettingsServiceTests` | Gespeicherter Projekt-Default wird korrekt abgerufen |
| `TestGetProjectDefaultPluginPrefix_NoProjectDefault_ReturnsNull` | `PluginDefaultSettingsServiceTests` | Falls kein Projekt-Default: `null` wird zurückgegeben |
| `TestSaveProjectDefaultPluginPrefix_StoresInAppEinstellung` | `PluginDefaultSettingsServiceTests` | Projekt-Default wird mit scoped Key in `AppEinstellung` gespeichert |
| `TestResolvePluginWithProjectScope_UsesProjectDefault` | `PluginSelectionServiceTests` | `ResolveDevelopmentAutomationPluginWithProjectScopeAsync` nutzt Projekt-Default wenn vorhanden |
| `TestResolvePluginWithProjectScope_ShowsDialogIfNoDefault` | `PluginSelectionServiceTests` | Dialog wird gezeigt, falls kein Projekt- oder Aufgaben-Plugin vorhanden |
| `TestStartenCommand_CanExecute_StatusNeuNotCliRunning` | `TaskDetailViewModelTests` | `StartenCommand.CanExecute` == true wenn Status `Neu` und CLI nicht läuft |
| `TestStartenCommand_CanExecute_StatusNotNeu_ReturnsFalse` | `TaskDetailViewModelTests` | `StartenCommand.CanExecute` == false wenn Status != `Neu` |
| `TestStartenAsync_ShowsDialogIfNoPluginSelected` | `TaskDetailViewModelTests` | Plugin-Dialog wird angezeigt, falls Aufgabe kein Plugin hat |
| `TestStartenAsync_SavesProjectDefaultIfCheckboxActivated` | `TaskDetailViewModelTests` | Falls "Für Projekt verwenden" aktiviert: Projekt-Default wird gespeichert |
| `TestStartenAsync_DoesNotSaveProjectDefaultIfCheckboxDeactivated` | `TaskDetailViewModelTests` | Falls "Für Projekt verwenden" nicht aktiviert: kein Projekt-Default gespeichert |
| `TestStartenAsync_InvokesCombinedProcess_StartsCliUponSuccess` | `TaskDetailViewModelTests` | `StartenAsync` ruft `ProzessStartenUndCliStartenAsync` auf, CLI läuft danach |
| `TestPluginWechselCommand_CanExecute_CliRunning` | `TaskDetailViewModelTests` | `PluginAendernCommand.CanExecute` == true wenn CLI läuft |
| `TestPluginWechselCommand_CanExecute_CliNotRunning_ReturnsFalse` | `TaskDetailViewModelTests` | `PluginAendernCommand.CanExecute` == false wenn CLI nicht läuft |
| `TestPluginWechselAsync_StopsCliAndStartsNew` | `TaskDetailViewModelTests` | Plugin-Wechsel stoppt alte CLI, zeigt Dialog, startet neue CLI |
| `TestPluginWechselAsync_StopCliFailure_ShowsError` | `TaskDetailViewModelTests` | Falls `StopCliAsync` fehlschlägt: Fehler wird angezeigt, Dialog bleibt offen |
| `TestLoadAsync_AutoRestartsCli_StatusGestartetNoRunningProcess` | `TaskDetailViewModelTests` | `LadenAsync` startet CLI automatisch falls Status `Gestartet` und kein Prozess läuft |
| `TestLoadAsync_NoAutoRestart_CliAlreadyRunning` | `TaskDetailViewModelTests` | `LadenAsync` startet CLI nicht, falls dieser bereits läuft |
| `TestLoadAsync_NoAutoRestart_StatusNotGestartet` | `TaskDetailViewModelTests` | `LadenAsync` startet CLI nicht, falls Status != `Gestartet` |
| `TestPluginSelectionDialog_ShowsAvailablePlugins` | `PluginSelectionDialogTests` | Dialog zeigt verfügbare Plugins in Dropdown-Liste |
| `TestPluginSelectionDialog_CheckboxSaveAsProjectDefault` | `PluginSelectionDialogTests` | Checkbox-Zustand wird in Dialog-Resultat erfasst |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `AufgabeStatusTransitionTests.*` | Alle Tests müssen neue Enum-Werte nutzen; alte Status können nicht mehr verwendet werden |
| `TaskDetailViewModelTests.ShowCliPanel_IsTrue_WhenStatusInArbeit` | Status `InArbeit` existiert nicht mehr (entfernt); Test kann entfallen oder Status wird geändert |
| `TaskDetailViewModelTests.KannSpeichern_IsTrue_WhenStatusGestartetUndTitelGesetzt` | Bedingung prüfen: Status `Gestartet` ist noch gültig, aber `InArbeit` nicht mehr |
| `AufgabeServiceTests.StartenAsync_*` | `StartenAsync` setzt jetzt Status `Gestartet` statt `ArbeitsverzeichnisEingerichtet`; Assertions müssen angepasst werden |
| `EntwicklungsprozessServiceTests.ProzessStartenAsync_*` | Status-Assertion: erwartet `ArbeitsverzeichnisEingerichtet`, jetzt `Gestartet` (falls neue kombinierte Methode genutzt wird) |
| `E2E_TaskDetailNavigation.*` | Navigation-Pfade prüfen: alte UI-Elemente (Buttons) wurden entfernt; Test-Code muss neue Commands verweisen |
| `E2E_CreateNewTaskNavigation.*` | Workflow hat sich geändert: direkt `StartenCommand`, nicht `StatusGestartetSetzenCommand` + `CliStartenCommand` |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Aufgabe im Status "Neu" mit "Starten" auf "Gestartet" wechseln; Repository geklont, CLI gestartet | `E2E_AufgabeStarten` | Akzeptanzkriterium 1: Direkter Übergang mit kombiniertem Klone + CLI-Start |
| Plugin-Dialog anzeigen, falls kein CLI-Plugin gespeichert; Plugin auswählen | `E2E_PluginSelectionDialog` | Akzeptanzkriterium 2: Dialog wird angezeigt bei fehlendem Plugin |
| Plugin-Dialog mit Checkbox aktivieren; Projekt-Standard wird gespeichert | `E2E_PluginProjectDefault` | Akzeptanzkriterium 3: Nächste Aufgabe nutzt gespeichertes Plugin, Dialog nicht gezeigt |
| Nächste Aufgabe desselben Projekts; Dialog nicht anzeigen, gespeichertes Plugin verwenden | `E2E_PluginProjectDefault_NextTask` | Akzeptanzkriterium 3 (Fortsetzung): Dialog wird übersprungen für nächste Aufgabe |
| Plugin-Wechsel durch "Plugin ändern"-Button; Dialog anzeigen, neues Plugin; bestehendes Cli-Prozess beendet, neue CLI gestartet | `E2E_PluginWechsel` | Akzeptanzkriterium 4: Plugin-Wechsel mit Dialog + Prozess-Neustart |
| Aufgabendetailansicht für Status "Gestartet" ohne aktiven Prozess öffnen; CLI automatisch starten und einbetten | `E2E_AutoStartCli` | Akzeptanzkriterium 5: Automatischer CLI-Neustart bei Ansicht-Laden |
| Menüband-Elemente: neue "Starten"-Aktion sichtbar; alte "Repository klonen" + "CLI starten" Buttons nicht vorhanden | `E2E_RibbonMenuItems` | Akzeptanzkriterium 6: UI-Elemente aktualisiert |

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `E2E_TaskDetailNavigation.*` | Button-Selektoren ändern: alte `CliStartenCommand` / `StatusGestartetSetzenCommand` Buttons nicht mehr vorhanden; neue `StartenCommand` Button verwenden |
| `E2E_CreateNewTaskNavigation.*` | Workflow vereinfacht: alte Schritt-Sequenz nicht mehr nötig; direkt neuer Schritt-Ablauf mit `StartenCommand` |

## Offene Punkte

Keine. Alle offenen Punkte aus der Anforderung wurden durch folgende Annahmen geklärt:

1. **Datenbankkompatibilität (Requirement Punkt 1):**
   - **Annahme:** Alte Status `ArbeitsverzeichnisEingerichtet` und `InArbeit` werden zu `Gestartet` migriert. Begründung: Diese Status bedeuten, dass die Aufgabe in Bearbeitung war; `Gestartet` ist der neue äquivalente Status.
   - **Umsetzung:** Migration `20260610000001_UpdateAufgabeStatusEnum` (bereits vorhanden) wird validiert.

2. **Plugin-Zuordnungspeicherort (Requirement Punkt 2):**
   - **Annahme:** Projekt-Level Plugin-Speicherung erfolgt über bestehende `PluginDefaultSettingsService` mit Scoped-Key `plugins.default.project.{ProjektId}.{PluginType}` in `AppEinstellung` Tabelle. Keine neue Tabelle.
   - **Umsetzung:** Neue Methoden `GetProjectDefaultPluginPrefixAsync`, `SaveProjectDefaultPluginPrefixAsync` in `PluginDefaultSettingsService`.

3. **Fehlerbehandlung bei CLI-Wechsel (Requirement Punkt 3):**
   - **Annahme:** Wenn Prozess-Beendigung fehlschlägt, wird Fehler angezeigt, Dialog bleibt offen, Neustarts nicht durchgeführt.
   - **Umsetzung:** `TaskDetailViewModel.PluginWechselAsync` mit try-catch um `StopCliAsync`.

4. **Menüband-Elemente (Requirement Punkt 4):**
   - **Annahme:** Alle alten Buttons für Repository-Klone und CLI-Start werden entfernt und durch neuen `StartenCommand` Button ersetzt.
   - **Umsetzung:** Ribbon-Definition in `TaskDetailView.xaml` wird aktualisiert.

5. **Recovery-Verhalten (Requirement Punkt 5):**
   - **Annahme:** Falls Status `Gestartet` aber kein Arbeitsverzeichnis existiert: Fehler wird angezeigt, kein automatischer Recovery. Benutzer kann Aufgabe erneut starten oder archivieren.
   - **Umsetzung:** Error-Handling in `KiAusfuehrungsService.StartCliAsync` (existiert bereits für ähnliche Fälle).

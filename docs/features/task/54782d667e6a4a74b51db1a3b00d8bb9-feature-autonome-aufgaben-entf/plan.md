# Umsetzungsplan: Entfernen des Features „Autonome Aufgaben"

## Übersicht

Das Feature „Autonome Aufgaben" (Projektleiter-Agent, Unteragenten, Initialisierungsdialog, Token-/Laufzeitbudget, Persistenz-Modus, Feature-Flag, zugehörige Detailansicht) wird vollständig aus Code, Konfiguration, Datenmodell, Tests und Dokumentation entfernt. Betroffen sind die Schichten Domain (Entitäten, Value Objects, Enums), Application (Services, `AufgabeService`, `AufgabeRecoveryService`, `KiPluginLimitService`, `AppEinstellungService`), App (`TaskDetailViewModel`/`TaskDetailView`, `SettingsViewModel`/`SettingsView`, `App.xaml.cs`, Dialoge), Infrastructure (`SoftwareschmiededDbContext`, neue additive Migration), die Konfiguration (`appsettings.json`, Abschnitt `AutonomAufgaben`) sowie die Tests und die Hilfe-/README-Dokumentation.

Der einfache Aufgabenstart, die ereignisgetriebene Bearbeitung (`ScmEvent*`), der Pausen-Mechanismus (`Aufgabe.PausiertBisUtc`) und die übrige Aufgabenlogik bleiben unverändert erhalten. Bestehende Migrationen werden nicht verändert.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Datenmodell-Entfernung (`AutonomAufgabeKonfiguration`, `UnteragentSpezifikation`, `SkillDefinition`) | Eine neue, additive EF-Core-Migration (Name `RemoveAutonomAufgabeModels`), die Tabellen, Indizes und FKs entfernt und die Bestandsdaten bereinigt. Die autonomen Tabellen werden gelöscht (nicht als ungenutzte Tabellen belassen). Bestehende Migrationen (inkl. `Designer.cs`) bleiben unverändert. | Upgrade-Sicherheit für bestehende Installationen (Skill `entityframework-database`). Historie bleibt nachvollziehbar. Anwenderentscheidung: Löschen. |
| Umgang mit Bestandsdaten autonomer Aufgaben (Zeilen in `Aufgabe`) | Aufgabe-Zeilen bleiben erhalten und werden zu regulären Aufgaben. Ihr Laufzustand wird in der Migration zurückgesetzt: `AusfuehrungsStatus` = `NichtGestartet`, `AktiveRunId` = NULL, `PausiertBisUtc` = NULL, `LastHeartbeatUtc` = NULL. Die Config-Zeilen werden gelöscht. Anwenderentscheidung: Die Aufgaben bleiben als reguläre Aufgaben mit zurückgesetztem Laufzustand bestehen. | Verhindert, dass ehemals autonome Aufgaben von `AufgabeRecoveryService` oder `KiPluginLimitService` als laufend behandelt werden. Der Anwender kann sie mit dem regulären Starten-Button wieder aufnehmen. Geprüft: `AufgabeAusfuehrungsStatusExtensions.DarfAusfuehrungStarten` erlaubt `NichtGestartet` (für Status ungleich `Beendet`/`Archiviert`), der Starten-Button bleibt also nutzbar. |
| Bereinigung des Einstellungsschlüssels `autonomeaufgaben.enabled` | Die Zeile wird in der Migration über `migrationBuilder.Sql` aus der Tabelle `AppEinstellungen` gelöscht. | Die Anforderung verlangt, dass das Feature nie existiert hat. Ein verwaister Schlüssel wäre ein Rest. |
| Pausen-Mechanismus (`Aufgabe.PausiertBisUtc`, `AufgabeService.SetPauseAsync`) | Bleibt erhalten. Nur die Sperre `if (aufgabe.IstAutonom()) throw` und das `Include(a => a.AutonomKonfiguration)` entfallen. | Der Pausen-Mechanismus wird von `KiPluginLimitService`, `PromptZeitVersandService`, `EntwicklungsprozessService`, `ScmEventOrchestrator` und `AufgabeRecoveryService` genutzt und ist kein autonomes Feld. |
| Filter `a.AutonomKonfiguration == null` in `AufgabeRecoveryService` und `KiPluginLimitService` | Ersatzlos entfernen. | Nach Entfernen der Navigation ist jede Aufgabe regulär. Das Verhalten für reguläre Aufgaben bleibt identisch. |
| Signatur `AufgabeRecoveryService.IstRecoveryStatus(status, ausfuehrungsStatus, istAutonom)` | Parameter `istAutonom` entfernen; die Logik wird auf die beiden übrigen Parameter reduziert. | Der Parameter hat nach der Entfernung keine fachliche Bedeutung mehr. Betroffen ist auch `AufgabeRecoveryServiceTests`. |
| `TaskDetailViewModel`-Prädikate `!_aufgabe.IstAutonom()` (Z. ~251, 488, 578, 756) | Ausdruck vereinfachen (immer wahr), nicht nur löschen. | Die Prädikate sind Teil gemeinsamer Pfade (Pause, Starten). |
| Einstellungsabschnitt „Automatisierung" in `SettingsView.xaml` | Vollständig entfernen (Überschrift und Container). Anwenderentscheidung. | Nach Entfernen der einzigen Checkbox ist der Abschnitt leer. |
| `GitArbeitsbereich` (Value Object) und `Aufgabe.GitArbeitsbereich` | Bleiben erhalten. Nur `UnteragentSpezifikation.GitArbeitsbereich` entfällt zusammen mit dem Entity. | Gemeinsam genutzt von `AufgabeService` und `Aufgabe`. |
| `AddRepositoryInitialisierungKonfiguration` (Migration) | Bleibt unverändert; gehört nicht zum autonomen Feature. | Die Migration gehört zur Repository-Initialisierung (`RepositoryInitialisierungKonfiguration` an `GitRepository`, Issue 228). |
| Dokumentation | Bereinigt werden ausschließlich `docs/help/` und `README.md`, ohne Hinweis auf eine frühere Existenz des Features. `docs/features/` (inkl. `docs/features/task/...`) und `changes.log` bleiben unverändert. Anwenderentscheidung. | Anforderung: „als hätte es das Feature nie gegeben" gilt für die aktive Hilfe und das README. `docs/features/` enthält historische Protokolle. Die in der Anforderung genannte Datei `docs/features.md` existiert nicht und entfällt als Prüfpunkt. |
| Hinweis zum Datenverlust | Knapper Hinweis in den Release Notes (Datenverlust durch die Migration: Projektleiter-, Unteragent- und Skill-Daten). Die Hilfe-Dokumentation bleibt ohne Hinweis. Anwenderentscheidung. | Der Datenverlust ist sonst für Anwender nicht erkennbar. |
| Ausführung der E2E-Tests (FlaUI) in der Sandbox | E2E-Tests (FlaUI) werden in der Sandbox ausgeführt. Die ConPTY-Tests bleiben über `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` übersprungen. Schlägt ein früher E2E-Test fehl, wird er erneut ausgeführt, bevor er als Fehler gilt, da der Anwender parallel am Desktop arbeiten kann. Zusätzlich gilt die CLAUDE.md-Regel: Bei „element not found" oder Timeout zuerst das App-Log prüfen. Anwenderentscheidung. | Die E2E-Abdeckung ist der primäre Funktionsnachweis. Die Störanfälligkeit durch Desktopaktivität wird durch Wiederholung abgefangen. |
| Ereignisgetriebene Bearbeitung | Keine Änderung. | Per Grep geprüft: keine Abhängigkeit auf autonome Typen (siehe `inventory/logic.md`). |

Die Annahme aus der Anforderung (Punkt 6 der offenen Fragen), dass „ereignisgetriebene Bearbeitung" die `ScmEvent`-Kette meint und „einfacher Aufgabenstart" den `EntwicklungsprozessService`, ist durch die Bestandsaufnahme bestätigt.

## Programmabläufe

### Anwendungsstart (Startup-Recovery und DI)

1. `App.xaml.cs` führt die Startup-Recovery nur noch für reguläre Aufgaben über `AufgabeRecoveryService` aus. Der Block für `AutonomAufgabeKonfigurationen` (Z. ~114–136) und `ErstelleResumePromptNachAppNeustart` (Z. ~158) entfällt.
2. `KiPluginLimitService` (Z. ~285) und `AufgabeRecoveryService` (Z. ~290) bleiben registriert. Alle autonomen Registrierungen entfallen (Z. ~241–245, 299, 334–335, 397, `Configure<AutonomAufgabenOptions>` Z. ~234). Dazu gehört der gehostete Dienst `UnteragentGovernanceMonitoringService`.
3. Die App startet ohne Abschnitt `AutonomAufgaben` in `appsettings.json`. Die Migration läuft beim Start vor der Recovery.

### Datenbankmigration beim Start (bestehende Installation)

1. EF Core wendet die neue Migration `RemoveAutonomAufgabeModels` an.
2. Die Migration setzt per SQL die Laufzustände der Aufgaben zurück, die eine Zeile in `AutonomAufgabeKonfigurationen` haben (siehe Designentscheidungen).
3. Die Migration löscht die Zeile `autonomeaufgaben.enabled` in `AppEinstellungen` und anschließend `AutonomAufgabeKonfigurationen`, `UnteragentSpezifikationen`, `SkillDefinitionen` (Kindtabellen zuerst).
4. Die `Down()`-Methode erzeugt die Tabellen mit ihrem Schema wieder, ohne Daten zurückzuholen.

### Einfacher Aufgabenstart und Pause (regulär, unverändert im Ablauf)

1. Der Anwender startet eine Aufgabe über den Starten-Button. `TaskDetailViewModel` ruft `EntwicklungsprozessService` auf. Die Ribbon-Gruppe „Autonome Aufgabe" ist nicht mehr vorhanden, die Sichtbarkeit der übrigen Schaltflächen hängt nicht mehr vom Autonom-Status ab.
2. Der Anwender pausiert über `AufgabePausierenDialog` → `AufgabeService.SetPauseAsync`. Die Methode setzt `PausiertBisUtc`. Der Autonom-Check entfällt.
3. Bei einem Plugin-Rate-Limit ruft `CliOutputProtokollWriter` `KiPluginLimitService.VerarbeiteRateLimitAsync` auf. Die Kandidatenabfrage umfasst alle aktiven Aufgaben ohne Autonom-Filter.

### Recovery nach Absturz

1. `AufgabeRecoveryService.RecoverManuellAsync` lädt die Aufgabe ohne `Include(AutonomKonfiguration)` und ruft `IstRecoveryStatus(status, ausfuehrungsStatus)` auf (ohne Autonom-Flag).
2. Die Kandidatensuche umfasst alle Aufgaben ohne `AutonomKonfiguration`-Filter.

### Einstellungen

1. `SettingsViewModel` lädt und speichert nur noch die verbleibenden Einstellungen. Die Eigenschaft `IsAutonomAufgabenEnabled`, der Konstruktorparameter `IOptions<AutonomAufgabenOptions>` und die zugehörigen Lade-/Speicherzweige entfallen.
2. `SettingsView.xaml` enthält keinen Abschnitt „Automatisierung" mehr.

### Dialog- und Startpfad (entfällt)

Der Aufruf `TaskDetailViewModel.AutonomAufgabeInitialisierenAsync` → `AutonomAufgabeStartService.StarteAsync` → `IDialogService.ShowAutonomAufgabeInitialisierungsDialogAsync` entfällt vollständig. Ebenso entfällt die Wiederherstellung der `AutonomAufgabeDetailViewModel` über `StelleAutonomAufgabeDetailViewModelWiederHerAsync`.

Beteiligte Klassen/Komponenten: `App.xaml.cs`, `TaskDetailViewModel`, `TaskDetailView.xaml`, `SettingsViewModel`, `SettingsView.xaml`, `AufgabeService`, `AufgabeRecoveryService`, `KiPluginLimitService`, `AppEinstellungService`, `SoftwareschmiededDbContext`, neue Migration.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `RemoveAutonomAufgabeModels` (Migration, Dateien `Migrations/<Zeitstempel>_RemoveAutonomAufgabeModels.cs` und `.Designer.cs`) | EF-Core-Migration | Entfernt die autonomen Tabellen und bereinigt Bestandsdaten. Wird per `dotnet ef migrations add` erzeugt, Datenbereinigungs-SQL wird ergänzt. |
| `RemoveAutonomAufgabeModelsMigrationTests` (IntegrationTests) | Testklasse | Prüft die Migration an einer SQLite-Datei, siehe Tests. |

Die Migration ersetzt außerdem den Snapshot `SoftwareschmiededDbContextModelSnapshot.cs` (wird von EF aktualisiert, nicht von Hand geändert).

## Änderungen an bestehenden Klassen

### `Aufgabe` (Domain-Entität)

- **Entfernte Eigenschaft:** `AutonomKonfiguration` (`AutonomAufgabeKonfiguration?`) inkl. XML-Dokumentation (Z. ~117–118)
- Alle übrigen Eigenschaften bleiben unverändert (`PausiertBisUtc`, `GitArbeitsbereich`, `BasisBranchName`, `ScmAbschlussErkanntUtc` usw.).

### `AufgabeExtensions` (Domain, statische Klasse)

- **Entfernte Methode:** `IstAutonom` (einzige Methode der Datei)
- **Konsequenz:** Die Datei `AufgabeExtensions.cs` wird vollständig gelöscht.

### `SoftwareschmiededDbContext` (Infrastructure)

- **Entfernte DbSets:** `AutonomAufgabeKonfigurationen`, `UnteragentSpezifikationen`, `SkillDefinitionen` (Z. ~90–97)
- **Entfernte Mappings:** `Aufgabe.AutonomKonfiguration` 1:1 (Z. ~239–243), `AutonomAufgabeKonfiguration` (Z. ~245–264), `UnteragentSpezifikation` (Z. ~266–281), `SkillDefinition` (Z. ~283–294)
- **Unverändert:** `RepositoryInitialisierungKonfiguration`-Mapping, alle `ScmEvent*`-Mappings, `Aufgabe`-Mapping ohne Navigation.

### `AufgabeService` (Application)

- **Geänderte Methoden:** `SetPauseAsync` (Z. ~836–850) — die Prüfung `if (aufgabe.IstAutonom()) throw ...` und das `.Include(a => a.AutonomKonfiguration)` entfallen. Pausen-Logik bleibt.
- **Geänderte Methoden:** `GetDetailAsync` (Z. ~102–103) — `.Include(a => a.AutonomKonfiguration)` entfällt.

### `AufgabeRecoveryService` (Application)

- **Geänderte Methoden:** `RecoverManuellAsync` (Z. ~72–84) — `Include` und Aufruf `IstAutonom()` entfallen.
- **Geänderte Methoden:** `IstRecoveryStatus(AufgabeStatus, AufgabeAusfuehrungsStatus, bool istAutonom)` (Z. ~235) — Parameter `istAutonom` entfällt, Signatur wird `IstRecoveryStatus(AufgabeStatus, AufgabeAusfuehrungsStatus)`.
- **Geänderte Methoden:** Kandidatensuche (Z. ~54) — Klausel `&& a.AutonomKonfiguration == null` entfällt.

### `KiPluginLimitService` (Application)

- **Geänderte Methoden:** Kandidatenabfrage (Z. ~85) — Klausel `&& a.AutonomKonfiguration == null` entfällt. Sonst keine Änderung.

### `AppEinstellungService` (Application)

- **Entfernte Eigenschaft:** Konstante `AutonomAufgabenEnabledKey` (`autonomeaufgaben.enabled`, Z. ~43)
- **Entfernte Methode:** `GetAutonomAufgabenEnabledAsync` (Z. ~179–182)
- Alle übrigen Einstellungsmethoden (`GetScmMonitoringEnabledAsync`, `GetBoolSettingAsync`, `SetSettingAsync`, Plugin-Einstellungen) bleiben unverändert.

### `TaskDetailViewModel` (App)

- **Entfernte Felder:** `_autonomAufgabeStartService`, `_autonomAufgabenOptions`, `_autonomAufgabeDetailViewModel`, `_isAutonomAufgabenEnabled`
- **Entfernte Properties:** `IsAutonomAufgabe`, `CanAutonomAufgabeInitialisieren`, `ShowAutonomAufgabeRibbonGruppe`, `ShowAutomatisierungPanel`, `IsAutonomAufgabenEnabled`, `AutonomAufgabeDetailViewModel`
- **Entfernte Commands:** `AutonomAufgabeInitialisierenCommand`
- **Entfernte Methoden:** `AutonomAufgabeInitialisierenAsync`, `AktualisiereIsAutonomAufgabenEnabledAsync`, `StelleAutonomAufgabeDetailViewModelWiederHerAsync`, `SetzeAutonomAufgabeDetailViewAsync`
- **Geänderte Prädikate:** `!_aufgabe.IstAutonom()` (Z. ~251, 488, 578, 756) — Ausdruck wird vereinfacht (immer wahr bzw. entfernt).
- **Konstruktor:** Parameter `IStartService`/Options/DetailVM entfallen, Konstruktoraufrufe in Tests und `App.xaml.cs` werden angepasst.

### `TaskDetailView.xaml` (App)

- **Entfernt:** Ribbon-Gruppe „Autonome Aufgabe" (Z. ~214–237) mit den Schaltflächen `AutonomAufgabeInitialisieren`, `AutonomAufgabeStartAgent`, `AutonomAufgabeStopAgent`, `AutonomAufgabeResumeAgent`.
- **Entfernt:** `<views:AutonomAufgabeDetailView>` (Z. ~648).
- **Geändert:** Bindings `Visibility="{Binding IsAutonomAufgabe, Converter=...}"` (Z. ~53, 58) — die `Visibility`-Attribute entfallen, da die Elemente immer sichtbar sind.

### `SettingsViewModel` (App)

- **Entfernte Property:** `IsAutonomAufgabenEnabled` (Z. ~224–228)
- **Entfernte Zweige:** Laden (Z. ~438) und Speichern (Z. ~513–514)
- **Entfernter Konstruktorparameter:** `IOptions<AutonomAufgabenOptions>` (Z. ~29, 343, 355)

### `SettingsView.xaml` (App)

- **Entfernt:** Container `<!-- Automatisierung -->` (Z. ~278–288) mit Überschrift und Checkbox `IsAutonomAufgabenEnabled`.

### `IDialogService` / `WpfDialogService` (App)

- **Entfernte Methode:** `ShowAutonomAufgabeInitialisierungsDialogAsync` (Interface Z. ~50–52, Implementierung Z. ~141)

### `App.xaml.cs` (App, Composition Root)

- **Entfernt:** Startup-Recovery für autonome Konfigurationen (Z. ~114–136), `ErstelleResumePromptNachAppNeustart` (Z. ~158), `Configure<AutonomAufgabenOptions>` (Z. ~234), DI-Registrierungen (Z. ~241–245, 299, 334–335, 397).
- **Beibehalten:** `KiPluginLimitService`, `AufgabeRecoveryService`, alle `ScmEvent*`-Registrierungen.

### `appsettings.json` (Konfiguration)

- **Entfernt:** Abschnitt `AutonomAufgaben` (Z. ~17–25) mit `Enabled`, `DefaultTokenBudget`, `DefaultRuntimeLimitMinutes`, `HeartbeatTimeoutSeconds`, `MaxConcurrentUnteragenten`, `SkillAutogenerationEnabled`, `MaxClones`, `MaxFeatureBranches`.

### `docs/help/terminal/api.md` (Dokumentation)

- Z. 128: Verweis „und `ProjektleiterAgentService` (Initial-Prompt)" entfällt; der Satz bleibt mit `PromptZeitVersandService`.

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `<Zeitstempel>_RemoveAutonomAufgabeModels` | `Aufgabe` (Daten), `AutonomAufgabeKonfigurationen`, `UnteragentSpezifikationen`, `SkillDefinitionen`, `AppEinstellungen` | (1) SQL: Laufzustand der Aufgaben mit Config-Zeile zurücksetzen (`AusfuehrungsStatus`=`NichtGestartet`, `AktiveRunId`, `PausiertBisUtc`, `LastHeartbeatUtc` = NULL). (2) SQL: Zeile `autonomeaufgaben.enabled` aus `AppEinstellungen` löschen. (3) `DropTable` der drei autonomen Tabellen (Kindtabellen zuerst; `UnteragentSpezifikationen` und `SkillDefinitionen` verweisen auf `AutonomAufgabeKonfigurationen`). (4) `Down()` erzeugt die Tabellen wieder (Schema, ohne Daten). |

Keine bestehende Migration wird geändert. Die bestehenden Migrationen zu autonomen Entitäten (z. B. `20260820175118_AddAutonomAufgabeModels`) bleiben als Historie erhalten. Die Migration `20260823091609_AddRepositoryInitialisierungKonfiguration` bleibt unberührt.

Die Migration ist mit SQLite zu prüfen (Provider laut `SoftwareschmiededDbContextDesignTimeFactory`).

## Validierungsregeln

Keine. Das Feature entfernt Validierungen (z. B. `ValidiereAnfrage` mit Budget-Grenzen), es entstehen keine neuen Eingaben.

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `AutonomAufgaben` (Abschnitt in `appsettings.json`) | Konfigurationsabschnitt | — | Entfällt vollständig. |
| `autonomeaufgaben.enabled` (Zeile in `AppEinstellungen`) | Anwendungseinstellung | — | Wird durch die Migration gelöscht. |

## Seiteneffekte und Risiken

- **Bestandsdaten autonomer Aufgaben:** Die Migration löscht Projektleiter-, Unteragent- und Skill-Daten unwiderruflich. Die Aufgabe-Zeilen bleiben erhalten und werden zu regulären Aufgaben (siehe Designentscheidungen). Dies ist ein Datenverlust für Anwender, die das Feature genutzt haben; er wird in den Release Notes genannt.
- **Recovery und Plugin-Limit:** Ohne Zurücksetzen des Laufzustands könnten ehemals autonome Aufgaben nach dem Wegfall des Filters als reguläre Aufgaben von `AufgabeRecoveryService` oder `KiPluginLimitService` aufgegriffen werden. Die Designentscheidung (Zurücksetzen in der Migration) verhindert das. Die Migrationstests müssen dies abdecken.
- **Pausen-Mechanismus:** Die Entfernung des Autonom-Checks in `SetPauseAsync` ändert das Verhalten nur für Aufgaben, die vorher autonom waren. Für reguläre Aufgaben bleibt das Verhalten identisch. Tests für Pause und Session-Limit müssen grün bleiben.
- **TaskDetailView (Ribbon):** Die Gruppe „Autonome Aufgabe" entfällt. Die Sichtbarkeitsbindungen der übrigen Schaltflächen ändern sich. Mögliche Fehler zeigen sich nur im XAML-Compile oder zur Laufzeit. Der E2E-Lauf für Aufgabenansichten muss geprüft werden.
- **Startpfad:** `App.xaml.cs` verliert mehrere DI-Registrierungen. Fehlt eine Abhängigkeit eines verbleibenden Dienstes, schlägt der Start fehl. Der E2E-Startlauf (`MainTest`) prüft dies.
- **Dokumentation:** Die Entfernung aus `docs/help/` und `README.md` darf keinen Hinweis auf eine frühere Existenz hinterlassen (Ausnahme: Release-Notes-Hinweis zum Datenverlust). Nach der Umsetzung ist eine Suche nach „autonom", „Unteragent", „Projektleiter" und „autonomeaufgaben" in Code und Dokumentation (ohne Migrationen, `changes.log`, `docs/features/`) zu prüfen.
- **Korrekturen gegenüber der Bestandsaufnahme:**
  - `AddRepositoryInitialisierungKonfiguration` ist nicht autonom (siehe Designentscheidungen).
  - `GitArbeitsbereich` (Value Object und `Aufgabe`-Eigenschaft) ist gemeinsam und bleibt.
  - Zusätzlich zu entfernen: `Domain/Exceptions/UnteragentAbbruchException.cs` (nur von der Governance genutzt), `Tests/Domain/Entities/UnteragentSpezifikationTests_GitArbeitsbereich.cs`, `Helpers/TaskDetailViewModelTestFactory.cs` (Anpassung), `Application/Services/GitBranchHelper.cs` (einziger Aufrufer `UnteragentGitProvisioningService`), `Application/Services/StateJsonHelper.cs` (Aufrufer nur `ProjektleiterAgentService` und `SessionManagementService`).
  - Zusätzlich zu bereinigen: `docs/help/terminal/api.md` (Verweis auf `ProjektleiterAgentService`).
  - `IntegrationTests/Scripts/StartPs1IntegrationTests.cs` enthält nur das englische Wort „autonomous" im Kommentar. Keine Änderung nötig.
  - Die Datei `AufgabeExtensions.cs` enthält ausschließlich `IstAutonom` und wird gelöscht.

## Umsetzungsreihenfolge

1. **Kopplungen in gemeinsamem Code entfernen (noch ohne Löschungen)**
   - Voraussetzungen: Keine.
   - Beschreibung: `AufgabeService.SetPauseAsync` und `GetDetailAsync` bereinigen. `AufgabeRecoveryService` (Include, `IstRecoveryStatus`-Signatur, Kandidatenfilter) und `KiPluginLimitService` (Kandidatenfilter) anpassen. `TaskDetailViewModel`-Prädikate vereinfachen. Ergebnis: keine Verweise auf `IstAutonom()` oder `AutonomKonfiguration` außerhalb der zu löschenden Dateien.

2. **Navigation und Extension entfernen**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: `Aufgabe.AutonomKonfiguration` und `AufgabeExtensions.cs` (inkl. `IstAutonom`) entfernen.

3. **DbContext bereinigen**
   - Voraussetzungen: Schritt 2.
   - Beschreibung: DbSets und Mappings der drei autonomen Entitäten und die `Aufgabe`-Navigation entfernen.

4. **Migration `RemoveAutonomAufgabeModels` erzeugen und Datenbereinigung ergänzen**
   - Voraussetzungen: Schritt 3. Die Entity-Typen müssen noch nicht gelöscht sein, damit das Modell im Designzeitpunkt konsistent ist.
   - Beschreibung: `dotnet ef migrations add RemoveAutonomAufgabeModels` mit Projekt `src/Softwareschmiede`. Das generierte `Up()` um die SQL-Schritte (Laufzustand zurücksetzen, `AppEinstellungen`-Zeile löschen) ergänzen, Reihenfolge: SQL Reset, SQL Löschen Einstellung, Config-Tabellen droppen. Snapshot wird von EF aktualisiert. Bestehende Migrationen werden nicht angefasst.

5. **Autonome Services, Helfer und Optionen löschen**
   - Voraussetzungen: Schritt 2 (keine Aufrufer mehr in gemeinsamem Code).
   - Beschreibung: Dateien löschen: `AutonomAufgabenInitialisierungsService`, `ProjektleiterAgentService`, `UnteragentGovernanceService`, `UnteragentGovernanceMonitoringService`, `UnteragentGitProvisioningService`, `SessionManagementService`, `GitBranchHelper`, `StateJsonHelper`, `AutonomAufgabenOptions`, `Domain/Exceptions/UnteragentAbbruchException.cs`.

6. **Autonome Domain-Typen löschen**
   - Voraussetzungen: Schritt 5 (Services sind weg), Schritt 3.
   - Beschreibung: Entitäten `AutonomAufgabeKonfiguration`, `UnteragentSpezifikation`, `SkillDefinition`; Value Objects `RessourcenLimits`, `AutonomAufgabeInitialisierungsAnfrage`; Enums `PersistenzModus`, `PermissionsJsonOption`, `UnteragentStatus`, `UnteragentAktion`, `SkillStatus`. Vor dem Löschen per Grep prüfen, dass keine Aufrufer außerhalb der bereits bereinigten Stellen bleiben.

7. **Konfiguration und Einstellungen bereinigen**
   - Voraussetzungen: Schritt 5.
   - Beschreibung: `appsettings.json` Abschnitt `AutonomAufgaben` entfernen. `AppEinstellungService`: `AutonomAufgabenEnabledKey` und `GetAutonomAufgabenEnabledAsync` entfernen.

8. **App-Startpfad und DI bereinigen**
   - Voraussetzungen: Schritte 5, 7.
   - Beschreibung: `App.xaml.cs` (Startup-Recovery, `ErstelleResumePromptNachAppNeustart`, Options-Binding, DI-Registrierungen). `AutonomAufgabeStartService` und `AutonomAufgabeStartResult` löschen.

9. **Dialoge und Detailansicht löschen**
   - Voraussetzungen: Schritt 8.
   - Beschreibung: `AutonomAufgabeDetailViewModel`/`AutonomAufgabeDetailView` (inkl. `.xaml`), `AutonomAufgabeInitialisierungsDialogViewModel`/`AutonomAufgabeInitialisierungsDialog` (inkl. `.xaml`). `IDialogService` und `WpfDialogService` um `ShowAutonomAufgabeInitialisierungsDialogAsync` bereinigen.

10. **`TaskDetailViewModel` und `TaskDetailView.xaml` bereinigen**
    - Voraussetzungen: Schritt 9.
    - Beschreibung: Felder, Properties, Commands und Methoden entfernen (Liste siehe Änderungen). XAML-Ribbon-Gruppe und Detailansicht entfernen, Visibility-Bindings bereinigen. Konstruktorparameter anpassen.

11. **`SettingsViewModel` und `SettingsView.xaml` bereinigen**
    - Voraussetzungen: Schritt 7.
    - Beschreibung: `IsAutonomAufgabenEnabled`, Options-Parameter und Lade-/Speicherzweige entfernen. Abschnitt „Automatisierung" in `SettingsView.xaml` entfernen.

12. **Solution-Build bis zur Kompilierbarkeit des Produktivcodes**
    - Voraussetzungen: Schritte 1–11.
    - Beschreibung: `dotnet build Softwareschmiede.slnx -c Debug` (vollständiger Build, kein `--no-build`). Fehler beheben, bis alle Projekte inkl. Tests kompilieren. Wenn ein MSB3027-Kopierfehler wegen einer laufenden `Softwareschmiede.App.exe` auftritt, nicht eigenständig beenden, sondern den Anwender informieren (siehe CLAUDE.md, Self-Hosting-Risiko).

13. **Unit-Tests entfernen und anpassen**
    - Voraussetzungen: Schritt 12.
    - Beschreibung: siehe Tabellen unter „Tests". Ziel: Test-Projekt kompiliert und die verbleibenden Tests sind grün.

14. **Integrationstests anpassen und Migrationstest ergänzen**
    - Voraussetzungen: Schritt 4 (Migration existiert), Schritt 12.
    - Beschreibung: `EntwicklungsprozessServiceTests` (IntegrationTests) bereinigen. Neuer Migrationstest, siehe Tests.

15. **E2E-Tests entfernen, anpassen und konsolidieren**
    - Voraussetzungen: Schritt 12.
    - Beschreibung: Autonome E2E-Dateien und Page-Objects löschen. Gemeinsame E2E-Dateien bereinigen. Negativprüfung „Feature-Checkbox fehlt" in eine bestehende, bereits laufende Settings-E2E-Methode einbauen (keine neue Methode, keine Laufzeitverlängerung). Die verbleibenden E2E-Methoden bleiben unverändert in ihrer Anzahl.

16. **Dokumentation bereinigen und Release-Notes-Hinweis ergänzen**
    - Voraussetzungen: Schritte 1–11 (Inhalt steht fest).
    - Beschreibung: `docs/help/aufgaben/autonome-aufgaben/` komplett löschen. Abschnitte in den aufgezählten Dateien entfernen oder bereinigen (siehe Tabelle unter „Tests"/„Dokumentation" in `inventory/dokumentation.md`). `docs/help/terminal/api.md` Verweis entfernen. `README.md` bereinigen. Knapper Hinweis zum Datenverlust (Projektleiter-, Unteragent- und Skill-Daten werden durch die Migration gelöscht) in die Release Notes aufnehmen (über `/update-release-notes`). `changes.log`, `CHANGELOG.md`, `docs/features/` bleiben unverändert.

17. **Rest-Suche**
    - Voraussetzungen: Schritte 1–16.
    - Beschreibung: Suche nach `autonom`, `Autonom`, `Unteragent`, `Projektleiter`, `ProjektleiterAgent`, `SessionManagement`, `autonomeaufgaben`, `AutonomAufgaben`, `Automatisierung` in `src/` (ohne `Migrations/`, `bin/`, `obj/`), `docs/help/`, `README.md`. Treffer müssen Null sein, außer begründeten Ausnahmen (z. B. englisches „autonomous" im Kommentar von `StartPs1IntegrationTests`).

18. **Formatierung**
    - Voraussetzungen: Schritte 1–17.
    - Beschreibung: `dotnet format Softwareschmiede.slnx` (fixt Formatierung), danach `dotnet format Softwareschmiede.slnx --verify-no-changes` zur Prüfung.

19. **Testläufe (Verifikation)**
    - Voraussetzungen: Schritte 1–18; vollständiger Build vorher.
    - Beschreibung: Vollständiger Build. Dann `dotnet test` mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` inline, synchron (nicht mit `run_in_background`), in dieser Reihenfolge: Tests regulär (`Category!=OsInterface`), Tests OS-Interface (`Category=OsInterface`, inkl. FlaUI-E2E), IntegrationTests regulär, IntegrationTests OS-Interface. Vergleich mit Baseline (2 507 Tests, 0 fehlgeschlagen, siehe `inventory/tests.md`). Die Anzahl der Tests sinkt um die gelöschten; alle verbleibenden müssen bestehen.
    - Schlägt ein FlaUI-E2E-Test fehl, wird zuerst geprüft, ob der Build erfolgreich war, und das App-Log (`src/Softwareschmiede.App/bin/<Config>/<TargetFramework>/logs/softwareschmiede-*.log`) auf einen Startup-Fehler untersucht. Danach wird der betroffene Test erneut ausgeführt, da parallele Desktopaktivität des Anwenders die ersten E2E-Tests stören kann. Erst ein wiederholter Fehlschlag gilt als echter Fehler.

## Tests

### Neue Tests

Welche neuen Testmethoden und Hilfsmethoden sind erforderlich?

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `Migration_RemoveAutonomAufgabeModels_EntferntTabellenUndEinstellung` | `RemoveAutonomAufgabeModelsMigrationTests` (IntegrationTests) | Eine SQLite-Datenbank wird bis zur Vorgänger-Migration aufgebaut, die Migration wird angewendet: die drei Tabellen sind weg, die Zeile `autonomeaufgaben.enabled` ist weg, andere `AppEinstellungen` bleiben erhalten. |
| `Migration_RemoveAutonomAufgabeModels_SetztLaufzustandZurueck` | `RemoveAutonomAufgabeModelsMigrationTests` | Aufgabe mit Config-Zeile und Status `Aktiv`, `AktiveRunId`, `PausiertBisUtc` wird nach der Migration `NichtGestartet` mit NULL-Laufzeitfeldern; die Aufgabe-Zeile bleibt bestehen. |
| `Migration_RemoveAutonomAufgabeModels_BehaeltRegulaereAufgaben` | `RemoveAutonomAufgabeModelsMigrationTests` | Reguläre Aufgaben (ohne Config) bleiben unverändert (Status, Pause, Branch). |
| `Migration_RemoveAutonomAufgabeModels_Down_ErzeugtSchemaOhneDaten` | `RemoveAutonomAufgabeModelsMigrationTests` | `Down()` stellt die Tabellen wieder her, ohne Daten. |
| `AufgabeRecoveryServiceTests` neue Fälle: `IstRecoveryStatus` mit neuer Signatur (zwei Parameter) | `AufgabeRecoveryServiceTests` | Gleiche Zustandsmatrix wie bisher für reguläre Aufgaben, Parameter `istAutonom` entfällt. |

Hinweis: Das Hilfsklassen-Factory-Muster für Aufgaben in `Helpers/` wird für den Migrationstest wiederverwendet, sofern verfügbar; sonst eine kleine lokale Hilfsmethode.

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| Gelöscht: `AutonomAufgabeStartServiceTests` (App/Services) | Startpfad entfällt. |
| Gelöscht: `AutonomAufgabeDetailViewModelTests` (14) | Detailansicht entfällt. |
| Gelöscht: `AutonomAufgabeInitialisierungsDialogViewModelTests`, `..._BranchUndVorlagen` (13) | Dialog entfällt. |
| Gelöscht: `AutonomAufgabenInitialisierungsServiceTests` (23) | Service entfällt. |
| Gelöscht: `ProjektleiterAgentServiceTests`, `_CliIntegration`, `_Fehlerfaelle` (26) | Service entfällt. |
| Gelöscht: `SessionManagementServiceTests` (10) | Service entfällt. |
| Gelöscht: `UnteragentGovernanceServiceTests` (10), `UnteragentGovernanceMonitoringServiceTests` (5), `UnteragentGitProvisioningServiceTests` (3) | Services entfallen. |
| Gelöscht: `Domain/Entities/AufgabeTests_IstAutonom` (3) | Methode entfällt. |
| Gelöscht: `Domain/Entities/AutonomAufgabeKonfigurationTests_RessourcenLimits` (2) | Entity und VO entfallen. |
| Gelöscht: `Domain/Entities/UnteragentSpezifikationTests_GitArbeitsbereich` | Entity entfällt. `AufgabeTests_GitArbeitsbereich` bleibt. |
| Gelöscht: `Helpers/AutonomAufgabenInitialisierungsServiceTestFactory`, `Helpers/ProjektleiterAgentServiceTestDatenFactory` | Nur von gelöschten Tests genutzt. |
| Angepasst: `AufgabeRecoveryServiceTests` (12) | Autonome Fälle entfernen, `IstRecoveryStatus`-Aufruf ohne Parameter `istAutonom` (Methode `IstRecoveryStatus_ShouldMatchAllowedStates` mit drei Parametern und die Testdaten in `[InlineData]`). |
| Angepasst: `AufgabeServiceTests_Pause` (7) | Test „autonome Aufgabe kann nicht pausiert werden" entfernen, übrige Pausentests bleiben. |
| Angepasst: `TaskDetailViewModelTests` (115), `_Automatisierung` (12, ganze Datei autonom → löschen, falls keine regulären Fälle enthalten; vor dem Löschen Inhalt prüfen), `_Pause` (9), `_PluginAktivierung` (2), `_CliRawExport` (5), `_CliReplayExport` (6), `_ZeitgesteuerterPrompt` (8), `TaskDetailViewModelTestsBase` | Autonome Verweise (Ribbon-Gruppe, `IsAutonomAufgabe`, Feature-Flag, `AutonomAufgabeDetailViewModel`) entfernen, Konstruktoraufrufe anpassen. Reguläre Pause-, Plugin- und Export-Tests bleiben. |
| Angepasst: `Helpers/TaskDetailViewModelTestFactory` (23 Verweise) | Konstruktorparameter und Fakes entfernen. |
| Angepasst: `SettingsViewModelTests` (35, 16 autonome), `SettingsViewModelTests_IdePlugin` (9, 1 autonom) | Feature-Flag-Tests entfernen, Konstruktoraufrufe (`IOptions<AutonomAufgabenOptions>`) anpassen. |
| Angepasst: `MainWindowViewModelUpdateTestBase` | Ein Verweis auf den Feature-Flag-Konstruktor entfernen. |
| Angepasst: `IntegrationTests/Services/EntwicklungsprozessServiceTests` (11, 7 autonome) | Autonome Fälle entfernen, übrige Startpfad-Tests bleiben. Vorher prüfen, ob ein Test den Pausen-Check mit autonomer Aufgabe prüft (dann anpassen, nicht löschen). |
| Unverändert: `IntegrationTests/Scripts/StartPs1IntegrationTests` | Nur englisches Wort „autonomous" im Kommentar. |
| Unverändert: `KiPluginLimitServiceTests` (8), `ScmEvent*`, `PromptZeitVersandServiceTests`, `ScmEventOrchestratorTests`, `CliOutputProtokollWriterTests`, `DashboardViewModelTests`, `MainWindowViewModelTests`, `AufgabeLaufAktivitaetTests`, `CliUpdateSafetyServiceTests`, `RecoveryServiceIntegrationTests` | Keine autonomen Verweise, Regressionsbasis. |

### E2E-Tests (primärer Funktionsnachweis)

Die Anforderung betrifft eine Entfernung. Der Benutzerfluss, der sich ändert, ist die Einstellungsseite (Entfall der Checkbox und des Abschnitts) und die Aufgabendetailansicht (Entfall der Ribbon-Gruppe „Autonome Aufgabe", Erhalt von Starten/Pausieren). Der Startpfad der App ist ebenfalls betroffen, weil DI und Konfiguration verändert werden.

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | App startet ohne Fehler (Startup-Recovery, DI, ohne `AutonomAufgaben`-Konfiguration) | Bestehende Startmethode in `MainTest` (keine Änderung der Anzahl) | Startpfad läuft ohne Exception durch | DI-Fehler zeigen sich nur beim tatsächlichen Start der App (siehe CLAUDE.md, WPF-Startup-Fehler). |
| Pflicht | Einstellungen, Registerkarte „Allgemein": Checkbox „Autonome Aufgaben aktivieren" und Abschnitt „Automatisierung" sind nicht vorhanden | Bestehende Settings-E2E-Methode (in `E2E_SettingsFeatureFlags` bzw. der Methode, die bereits die Registerkarte „Allgemein" öffnet) — Negativprüfung als zusätzlicher Schritt, keine neue Methode | Feature-Entfernung aus der UI | Sichtbarkeit in der echten UI ist nur über E2E prüfbar. |
| Pflicht | Aufgabendetailansicht einer regulären Aufgabe: Gruppe „Autonome Aufgabe" fehlt, Starten und Pausieren sind vorhanden und funktionieren | Bestehende Methoden `E2E_AufgabePausieren` und die Aufgaben-E2E-Methode, die Starten prüft (in der Umsetzung konkret zuordnen) — Negativprüfung der Ribbon-Gruppe als zusätzlicher Schritt | Pausen-Mechanismus bleibt erhalten, Entfall der Ribbon-Gruppe | Der Pausen-Mechanismus ist ein Benutzerfluss mit UI-Interaktion. |
| Pflicht | Bestehende Pausen- und Session-Limit-Abläufe bleiben funktionsfähig | `E2E_AufgabePausieren`, `E2E_SessionLimitPause` (unverändert, Regressionsnachweis) | Pausen-Mechanismus unverändert | Änderung am gemeinsamen Pausenpfad. |
| Pflicht | Ereignisgetriebene Bearbeitung bleibt funktionsfähig | `E2E_ScmEvents` (unverändert, Regressionsnachweis) | ScmEvent-Kette unverändert | Änderung an gemeinsamen Recovery-/Plugin-Filtern. |

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `E2E_SettingsFeatureFlags` (9 Verweise, keine Testmethode) | Checkbox-Bezug entfernen; Datei prüfen, ob sie nach Entfernung leer ist (dann löschen oder auf die verbleibenden Helfer reduzieren). |
| `E2E_ViewPattern` | Verweise auf autonome Views entfernen. |
| `MainTest` (12 Verweise) | Autonome Views/Feature-Flag-Schritte entfernen; Anzahl der Testmethoden nicht erhöhen. |
| `WpfTestBase` (4 Verweise) | Autonome Hilfsmethoden/Konstanten entfernen. |
| `Views/SettingsView` | Checkbox-Page-Object-Eigenschaft entfernen. |
| `Views/DialogView` | Verweis auf Initialisierungsdialog entfernen. |
| `Views/WindowExtensions` | Verweis auf autonome Views entfernen. |

Gelöscht: `E2E_AutonomAufgabenAgentExecution` (0 Testmethoden), `E2E_AutonomAufgabenInitialisierung` (0 Testmethoden), `E2E_AutonomAufgabenFeatureFlagDisabled` (1 `[SkippableFact]`), `Views/AutonomAufgabeDetailView`, `Views/Dialogs/AutonomAufgabeInitialisierungsDialogView`.

Konsolidierung: Da keine neuen E2E-Methoden entstehen, bleibt die Laufzeit der FlaUI-Suite unter dem Ausgangswert (der Wegfall der autonomen Methode verringert sie). Die Negativprüfungen werden als zusätzliche Schritte in bestehenden Methoden ergänzt.

Die ConPTY-E2E-Tests (14) werden in dieser Sandbox über `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` übersprungen. Ihre Verifikation in einer interaktiven Sitzung ist kein Blocker für diesen Plan.

## Offene Punkte

Keine.

Alle Punkte aus Anforderung und Bestandsaufnahme sind durch die Anwenderantworten entschieden und in Designentscheidungen, Programmabläufen und Umsetzungsreihenfolge eingearbeitet.

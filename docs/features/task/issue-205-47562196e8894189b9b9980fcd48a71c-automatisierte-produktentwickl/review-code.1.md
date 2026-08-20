# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### src/Softwareschmiede/Domain/Entities/Aufgabe.cs (Aufgabe)

- **Struktur — Redundante Zustandsmodellierung** — Ob eine Aufgabe eine "Autonome Aufgabe" ist, wird über zwei unabhängige, nicht synchronisierte Signale ausgedrückt: `AusfuehrungsStatus == AufgabeAusfuehrungsStatus.AutonomAufgabe` (Enum-Wert) und `AutonomKonfiguration != null` (Navigationseigenschaft, Zeile 87). Es gibt keine Invarianten-/Konstruktorlogik, die beide Zustände zusammenhält — reine `{ get; set; }`-Properties erlauben, dass beide auseinanderlaufen (z. B. `AusfuehrungsStatus = AutonomAufgabe` ohne `AutonomKonfiguration` oder umgekehrt).

  Empfehlung: Einen der beiden Signalgeber als alleinige Quelle der Wahrheit festlegen (z. B. Autonomie ausschließlich über `AutonomKonfiguration != null` bestimmen und den Enum-Wert `AutonomAufgabe` entfernen, oder umgekehrt), bzw. eine Validierung/Factory-Methode einführen, die Konsistenz erzwingt.

- **Code Smell — Temporäres Feld / vermischte Verantwortlichkeit** — `ProjektleiterAgentId`, `SessionPauseUtc`, `AktiveUnteragenten` (Zeilen 78, 81, 84) sind laut Doc-Kommentar "nur für Autonome Aufgaben" relevant, werden aber direkt auf `Aufgabe` statt in der bereits existierenden `AutonomAufgabeKonfiguration`-Entität gehalten, obwohl dort bereits alle übrigen Autonomie-spezifischen Daten liegen. Für reguläre Aufgaben bleiben diese Felder dauerhaft ungenutzt/null.

  Empfehlung: Laufzeit-Statusfelder in `AutonomAufgabeKonfiguration` (oder eine dedizierte Laufzeitstatus-Entity/Value Object) verschieben, analog zur bereits getroffenen Trennung für die statische Konfiguration.

### src/Softwareschmiede/Domain/Entities/AutonomAufgabeKonfiguration.cs, src/Softwareschmiede/Domain/Entities/SkillDefinition.cs, src/Softwareschmiede/Domain/Entities/UnteragentSpezifikation.cs

- **Code Smell — Primitive Obsession (Status)** — `SkillDefinition.SkillStatus` und `UnteragentSpezifikation.Status` sind als `string` modelliert, obwohl die Doc-Kommentare jeweils eine geschlossene Werteliste beschreiben ("Entwurf, Review, Freigegeben, Archiviert" bzw. "Erzeugt, Ausgeführt, Abgeschlossen, Fehler"). Das widerspricht der etablierten Konvention der Codebasis, die für praktisch jeden Status ein eigenes Enum mit `HasConversion<string>()` verwendet (`ProjektStatus`, `AufgabeStatus`, `DiffResultStatus`, `BenachrichtigungsEntscheidung`, `AufgabeLaufStatus`).

  Empfehlung: `SkillStatus` und `UnteragentSpezifikation.Status` als eigene Enums modellieren und im DbContext per `.HasConversion<string>()` konfigurieren, wie bei allen übrigen Statuswerten.

- **Code Smell — Primitive Obsession (Persistenzmodus)** — `AutonomAufgabeKonfiguration.PersistenzmModus` und das gleichnamige Feld in `AutonomAufgabeInitialisierungsAnfrage` sind `string` für einen laut Doc-Kommentar ebenfalls kleinen Wertebereich ("z. B. Standard, SessionReset"). In `AufgabeService.cs` (Zeile ~835) wird bereits ein Hardcoded-Literal `"Standard"` zugewiesen.

  Empfehlung: Ebenfalls als Enum modellieren, Magic Strings vermeiden.

- **Namenskonventionen — Tippfehler in Property-Namen** — `PersistenzmModus` (durchgängig in `AutonomAufgabeKonfiguration.cs`, `AutonomAufgabeInitialisierungsAnfrage.cs`, `SoftwareschmiededDbContext.cs`, Migration und Snapshot) und `ArbeitsverzeichnispPfad` (dieselben Dateien) enthalten jeweils einen überzähligen Buchstaben ("Persistenz**m**Modus", "Arbeitsverzeichnis**p**Pfad"). Das bestehende Konzept "Arbeitsverzeichnis" wird an über einem Dutzend anderer Stellen korrekt geschrieben (`ArbeitsverzeichnisResolver`, `IArbeitsverzeichnisResolver`, `ArbeitsverzeichnisSettingsService`).

  Empfehlung: In `PersistenzModus` bzw. `ArbeitsverzeichnisPfad` umbenennen (Entity, Value Object, DbContext-Konfiguration und ggf. neue Migration für Spaltenumbenennung).

- **Struktur — fehlende Kapselung (fehlende Navigation)** — `AutonomAufgabeKonfiguration` besitzt keine Collection-Navigationseigenschaften zu `SkillDefinition` bzw. `UnteragentSpezifikation`, obwohl beide Kind-Entitäten per FK auf sie zeigen (`SoftwareschmiededDbContext.cs`, `HasOne(...).WithMany()` ohne benannte Gegenseite). Das weicht von der durchgängigen bidirektionalen Navigation im übrigen Modell ab (`Projekt.Aufgaben`, `Aufgabe.Protokolleintraege`/`DiffResults`/`Todos`, `PullRequestReferenz.WorkflowRuns`).

  Empfehlung: `List<SkillDefinition> Skills` und `List<UnteragentSpezifikation> Unteragenten` auf `AutonomAufgabeKonfiguration` ergänzen und im DbContext mit `HasMany(...).WithOne(...)` verdrahten, konsistent zum Rest des Modells.

### src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs (SoftwareschmiededDbContext)

- **Fehlerbehandlung/Validierung — fehlende MaxLength** — Für `AutonomAufgabeKonfiguration` und `UnteragentSpezifikation` werden Branch-Name- und Pfad-artige Felder (`ProjektBranchName`, `PermissionsJsonPfad`, `ArbeitsverzeichnispPfad`, `AgentBranch`, `AgentDirectory`, `AgentClone`) nur mit `.IsRequired()` ohne `.HasMaxLength(...)` konfiguriert. Das weicht von der etablierten Konvention ab (`GitRepository.DefaultSourceBranchName` → 255, `RepositoryStartKonfiguration`-Pfade → 512, `PullRequestReferenz.SourceBranch`/`TargetBranch` → 300).

  Empfehlung: Konsistente `HasMaxLength(...)`-Grenzen für Branch-Namen und Pfade ergänzen (freitextige Felder wie `InitialPrompt`, `AgentPrompt`, `SkillContent` bewusst ausnehmen, wie bei `Prompttext`/`Beschreibung`).

- **Doppelter Code — wiederholter DateTimeOffset-Konverter** — Die neuen Property-Konfigurationen für `SkillDefinition.ErstellungsDatum`/`FreigabeDatum`, `UnteragentSpezifikation.ErzeugungsDatum`/`AbschlussDatum` sowie `Aufgabe.SessionPauseUtc` wiederholen erneut denselben inline `DateTimeOffset ↔ long`-Unix-Millisekunden-Konverter, der bereits an über zehn Stellen in derselben Datei dupliziert vorliegt.

  Empfehlung: Gemeinsamen `ValueConverter` (z. B. statische Felder `UnixMillisConverter`/`NullableUnixMillisConverter`) einführen und an allen Stellen — inklusive der neuen — referenzieren.

### src/Softwareschmiede/Application/Services/AufgabeService.cs (AufgabeService)

- **Struktur — Feature Envy** — `ErzeugeAutonomAufgabeAsync` (Zeilen ~810–847) baut die komplette `AutonomAufgabeInitialisierungsAnfrage` inkl. aller Autonom-Aufgaben-spezifischen Defaultwerte selbst zusammen, statt diese Zusammenstellung an `AutonomAufgabenInitialisierungsService` zu delegieren. Damit bekommt die ohnehin schon breite `AufgabeService`-Klasse (Aufgaben-CRUD, DiffResult-Lookups, Status-Lifecycle, Heartbeat/Lauf-Tracking) eine weitere, fachlich fremde Verantwortung.

  Empfehlung: Konfigurationsaufbau (Defaultwerte, Pfadbildung) in `AutonomAufgabenInitialisierungsService` verlagern; `AufgabeService` sollte nur noch delegieren und den Ausführungsstatus setzen.

- **Kopplung/Erweiterbarkeit — hardcodierte Werte statt Konfiguration** — Zeilen ~823–836: `TokenBudget: 500000`, `LaufzeitLimitMinuten: 480`, `PersistenzmModus: "Standard"`, `SkillAutogeneration: false` sowie der Pfad `Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AutonomAufgaben", ...)` sind hartkodierte Duplikate der in `AutonomAufgabenOptions` bereits vorhandenen und per DI registrierten (`App.xaml.cs`) Konfigurationswerte. Die Options-Klasse wird von `AufgabeService` gar nicht injiziert.

  Empfehlung: `IOptions<AutonomAufgabenOptions>` injizieren und diese Werte statt Literalen verwenden.

- **Code Smell — temporäres Feld** — `_autonomAufgabenInitialisierungsService` ist ein optionaler (`= null`) Konstruktor-Parameter, dessen Fehlen erst zur Laufzeit in `ErzeugeAutonomAufgabeAsync` per Exception erkannt wird.

  Empfehlung: Abhängigkeit verpflichtend machen (Aufrufstellen/Tests entsprechend anpassen) oder die Autonom-Erzeugung in einen eigenen, klar optionalen Service auslagern statt eine nullable Kernabhängigkeit in `AufgabeService` zu führen.

- **Ineffizienz** — Zeilen ~838–843: Nach `InitialisiereAsync(aufgabe, ...)` wird die bereits verfügbare `aufgabe`-Entity per `_db.Aufgaben.FindAsync([aufgabe.Id], ct)` erneut aus der DB geladen, nur um `AusfuehrungsStatus` zu setzen — unnötiger zusätzlicher Roundtrip.

  Empfehlung: Vorhandene Entity-Referenz weiterverwenden bzw. Tracking-Zustand prüfen statt Neuladen.

### src/Softwareschmiede/Application/Services/AutonomAufgabenInitialisierungsService.cs (AutonomAufgabenInitialisierungsService)

- **Doppelter Code** — `BuildPermissionsJson` und `BuildDefaultPermissionsJson` sind bis auf die Werte für `token_budget`/`net_runtime_minutes` identisch; ebenso überschneiden sich `BuildStateJson` und `BuildDefaultStateJson` im `governance`-Block fast vollständig (`max_subagents = 5, max_clones = 3, max_feature_branches = 10` ist an vier Stellen im Code dupliziert).

  Empfehlung: Gemeinsamen Builder mit Parametern extrahieren; Governance-Limits als Konstanten/aus `AutonomAufgabenOptions` beziehen statt viermal zu wiederholen.

- **Toter Code — verschwendete Arbeit** — `InitialisiereAsync` ruft zuerst `ErstelleArbeitsverzeichnisStrukturAsync` auf, welches bei fehlenden Dateien `BuildDefaultPermissionsJson()`/`BuildDefaultStateJson()` schreibt, überschreibt aber direkt im Anschluss beide Dateien unconditional mit `BuildPermissionsJson`/`BuildStateJson`. Da `ErstelleArbeitsverzeichnisStrukturAsync` produktiv nur aus `InitialisiereAsync` aufgerufen wird, ist der von den "Default"-Buildern erzeugte Inhalt im einzigen produktiven Ausführungspfad totes/verschwendetes Schreiben.

  Empfehlung: `ErstelleArbeitsverzeichnisStrukturAsync` ohne die Default-JSON-Erzeugung aufrufen (nur Verzeichnisse/plan.md/progress.md/governance.md), oder klarstellen, dass die Methode auch eigenständig aufrufbar sein soll (dann Duplikat zum vorherigen Punkt beheben).

### src/Softwareschmiede/Application/Services/AutonomAufgabenOptions.cs (AutonomAufgabenOptions)

- **Code Smell — Speculative Generality / totes Konfigurationsgerüst** — Die Klasse wird korrekt in `App.xaml.cs` per `services.Configure<AutonomAufgabenOptions>(...)` registriert, aber von keinem der geprüften Services (`AufgabeService`, `AutonomAufgabenInitialisierungsService`, `ProjektleiterAgentService`, `SessionManagementService`) injiziert oder gelesen. Die Business-Logik verdrahtet dieselben Werte stattdessen fest (siehe Befunde zu `AufgabeService.cs` und `AutonomAufgabenInitialisierungsService.cs`).

  Empfehlung: Options-Klasse tatsächlich in die Konfigurationsaufbau-Logik einbinden.

### src/Softwareschmiede/Application/Services/ProjektleiterAgentService.cs (ProjektleiterAgentService)

- **Namenskonventionen — Tippfehler** — Methodenname `StarteAgenAsync` (Zeile ~34) statt `StarteAgentAsync` (fehlendes "t"). Der Fehler wurde konsistent bis in `AutonomAufgabeDetailViewModel.StarteAgenAsync` und die zugehörigen Tests übernommen.

  Empfehlung: Umbenennen in `StarteAgentAsync` über alle referenzierenden Stellen (Service, ViewModel, Tests) hinweg.

- **Doppelter Code — Git-Klon-Logik** — Der Clone-Block in `SteuereUnteragentAsync` (Zeilen ~82–95: Zielverzeichnis prüfen → `CreateDirectory` → `git clone` → `IsSuccess` prüfen → `InvalidOperationException`) dupliziert nahezu wörtlich `AutonomAufgabenInitialisierungsService.KloneHauptRepositoryAsync`, obwohl Letzteres bereits als wiederverwendbare private Methode existiert.

  Empfehlung: Gemeinsame Klon-Hilfsmethode (z. B. in einem geteilten Git-Helper) extrahieren und aus beiden Services nutzen.

- **Fehlerbehandlung — verworfenes Ergebnis** — Zeile ~80: Das Ergebnis von `_cliRunner.RunAsync("git", ["branch", unteragent.AgentBranch], ...)` wird komplett verworfen, während der direkt danach folgende `git clone`-Aufruf korrekt auf `IsSuccess` geprüft wird. Schlägt die Branch-Erstellung fehl, läuft die Methode unbemerkt weiter.

  Empfehlung: Rückgabewert des `git branch`-Aufrufs analog zum Klon-Aufruf prüfen und bei Fehlschlag eine aussagekräftige Exception werfen.

- **Struktur — God-Methode** — `SteuereUnteragentAsync` (Zeilen ~61–108) vereint fünf konzeptionell getrennte Schritte (Validierung, Governance-Check, Verzeichnis-Erstellung, Branch-Erstellung, Klonen, Persistierung) in einer Methode, ohne wie im Schwesterservice in private Hilfsmethoden auszulagern.

  Empfehlung: In benannte private Teilschritte zerlegen (z. B. `ErstelleAgentBranchAsync`, `ErstelleAgentKlonAsync`), analog zu `AutonomAufgabenInitialisierungsService`.

- **Doppelter Code / Fehlerbehandlung — State-JSON-Manipulation** — `AktualisiereSubagentsInStateJsonAsync` (Zeilen ~141–167) dupliziert das "Datei lesen → als `JsonObject` parsen (mit Null-Fallback) → Abschnitt mutieren → mit neuem `JsonSerializerOptions { WriteIndented = true }` zurückschreiben"-Muster aus `SessionManagementService.AktualisierePausedUtcInStateJsonAsync`. Zudem ist `JsonNode.Parse(json)` ungeschützt gegen fehlerhaftes/korruptes `state.json`; da der DB-Save in `IntegriereErgebnisseAsync` vor diesem Aufruf bereits erfolgt, entsteht bei einem Parse-Fehler ein inkonsistenter Zwischenzustand (DB aktualisiert, Datei nicht) ohne Behandlung.

  Empfehlung: Gemeinsame State-JSON-Lese/Schreib-Hilfsklasse extrahieren (auch mit `SessionManagementService.cs` geteilt); Parse-Fehler abfangen/loggen statt ungeschützt zu propagieren.

### src/Softwareschmiede/Application/Services/SessionManagementService.cs (SessionManagementService)

- **Doppelter Code — State-JSON-Manipulation** — `AktualisierePausedUtcInStateJsonAsync` (Zeilen ~121–140) repliziert dasselbe Read-Modify-Write-Muster für `state.json` wie `ProjektleiterAgentService.AktualisiereSubagentsInStateJsonAsync`, inklusive erneuter Instanziierung von `new JsonSerializerOptions { WriteIndented = true }` statt Wiederverwendung der bereits in `AutonomAufgabenInitialisierungsService` vorhandenen statischen `JsonOptions`.

  Empfehlung: Gemeinsamen State-Store/Helper extrahieren und von beiden Services (`ProjektleiterAgentService`, `SessionManagementService`) sowie `AutonomAufgabenInitialisierungsService` nutzen.

### src/Softwareschmiede/Application/Services/UnteragentGovernanceService.cs (UnteragentGovernanceService)

- **Code Smell — Primitive Obsession** — `aktion` wird als roher `string` durchgereicht und gegen das `HashSet<string> VerboteneAktionen` (Zeilen ~12–16) geprüft. Ohne Enum/Konstanten besteht keine Compile-Time-Sicherheit gegen Tippfehler in Aktionsnamen an Aufrufstellen.

  Empfehlung: Aktionsnamen als Enum oder `static readonly string`-Konstanten modellieren, die von Aufrufer und Governance-Service gemeinsam referenziert werden.

- **Toter Code — Enforcement-Lücke** — Die verbotenen Aktionen `"pull_request_erstellen"` und `"skill_modifizieren"` (Zeilen ~14–15) werden im gesamten Produktionscode an keiner Stelle tatsächlich per `VerifiziereBerechtigung` geprüft — der einzige produktive Aufruf (`ProjektleiterAgentService.cs`) verwendet ausschließlich `"arbeitsverzeichnis_erstellen"`. Die beiden Verbotsregeln greifen aktuell an keiner realen Kontrollstelle.

  Empfehlung: An den tatsächlichen PR-Erstellungs- bzw. Skill-Modifikations-Codepfaden (sobald vorhanden) `VerifiziereBerechtigung` mit den passenden Aktionsnamen aufrufen, sonst sind die Regeln wirkungslose Governance-Deklarationen.

### src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs (TaskDetailViewModel)

- **Struktur — God-Klasse verstärkt** — Die Klasse umfasst bereits vor dieser Änderung ca. 2080 Zeilen mit vielen fachlich unabhängigen Verantwortlichkeiten (CLI-Steuerung, Diff/Datei-Explorer, Pull-Request-, Issue-, Promptvorlagen/Zeitversand- und IDE-Verwaltung). Die neue Methode `AutonomAufgabeInitialisierenAsync` (Zeilen ~1205–1247) fügt eine weitere eigenständige Verantwortlichkeit (Autonome-Aufgabe-Orchestrierung: Dialogaufruf, Neuladen der Aufgabe, Anzeige der Detailansicht) direkt in diese bereits überladene Klasse ein.

  Empfehlung: Die Autonome-Aufgabe-Startlogik in einen eigenen Coordinator/Handler auslagern, den `TaskDetailViewModel` nur noch injiziert und über einen einzeiligen Command-Aufruf nutzt.

### src/Softwareschmiede.App/ViewModels/AutonomAufgabeInitialisierungsDialogViewModel.cs (AutonomAufgabeInitialisierungsDialogViewModel)

- **Toter Code — verworfene Eingaben** — `SelectedProjectBranch`, `SelectedPermissionsOption`, `TokenBudget`, `AllowTokenExtension`, `RuntimeLimitMinutes`, `SelectedPersistenceMode` und `AutoGenerateSkills` sind an die View gebunden (`AutonomAufgabeInitialisierungsDialog.xaml`, Zeilen ~38–100) und `TokenBudget`/`RuntimeLimitMinutes` werden in `ValidiereEingaben()` (Zeilen ~172–190) sogar validiert. `BestaetigenAsync()` (Zeile ~151) ruft aber ausschließlich `_aufgabeService.ErzeugeAutonomAufgabeAsync(_aufgabe, InitialPrompt)` auf — eine Methode, die nur `aufgabe` und `initialprompt` entgegennimmt. Alle sieben übrigen Werte (inkl. bereits durchgeführter Validierung) werden verworfen, bevor sie die Domänenschicht erreichen.

  Empfehlung: Entweder die ungenutzten Properties/Validierungen entfernen, oder `ErzeugeAutonomAufgabeAsync` um die entsprechenden Parameter erweitern und tatsächlich befüllen.

### src/Softwareschmiede.App/ViewModels/AutonomAufgabeDetailViewModel.cs (AutonomAufgabeDetailViewModel)

- **Namenskonventionen — Tippfehler** — Methodennamen `StarteAgenAsync` (Zeile ~134), `StoppeAgenAsync` (Zeile ~154), `ResumeAgenAsync` (Zeile ~179): "Agen" statt "Agent" (fehlendes "t"), übernommen aus `ProjektleiterAgentService`.

  Empfehlung: Umbenennen zu `StarteAgentAsync`/`StoppeAgentAsync`/`ResumeAgentAsync` (zusammen mit der Umbenennung in `ProjektleiterAgentService`).

- **Kopplung/Erweiterbarkeit — verworfenes CancellationToken** — `StartCommand = new AsyncRelayCommand(_ => StarteAgenAsync(), () => !IsBusy)` sowie die analogen `StopCommand`/`ResumeCommand` (Zeilen ~91–93) verwerfen das von `AsyncRelayCommand` bereitgestellte `CancellationToken`; die zugehörigen Methoden nehmen keinen `CancellationToken`-Parameter entgegen und sind damit nicht abbrechbar, obwohl `AsyncRelayCommand.Cancel()` genau dafür vorgesehen ist. Im übrigen Code wird das Token konsequent durchgereicht (z. B. `TaskDetailViewModel`: `LadenCommand = new AsyncRelayCommand(ct => LadenAsync(ct))`).

  Empfehlung: `ct`-Parameter an `StarteAgenAsync(ct)`/`StoppeAgenAsync(ct)`/`ResumeAgenAsync(ct)` durchreichen, konsistent zum Rest der Codebasis.

- **Doppelter Code** — `LaedePlanAsync`/`LaedeProgressAsync`/`LaedeGovernanceAsync` (Zeilen ~114–123) sind strukturell identisch (Pfadaufbau via `Path.Combine(Konfiguration.ArbeitsverzeichnispPfad, "<datei>")` + `LadeDateiAsync`-Aufruf, nur Dateiname/Zielproperty unterscheiden sich). Ebenso wiederholen `StarteAgenAsync`/`StoppeAgenAsync`/`ResumeAgenAsync` (Zeilen ~134–201) exakt dasselbe Gerüst `IsBusy=true; ErrorMessage=null; try{…}catch(Exception ex){LogWarning; ErrorMessage=…}finally{IsBusy=false;}`.

  Empfehlung: Gemeinsame private Hilfsmethoden extrahieren, z. B. `LadeDateiInhaltAsync(string dateiname, Action<string> setter, ct)` und `FuehreAgentOperationAsync(Func<Task> operation, string fehlerKontext)`.

- **Struktur — fehlende Kapselung / Bindungsdefekt** — `Unteragenten`/`Skills` (Zeilen ~32/35) sind als `List<T> { get; set; }` statt `ObservableCollection<T>` implementiert. `Initialize()` (Zeilen ~108–109) ersetzt die Listen per Zuweisung, ohne `OnPropertyChanged` auszulösen. Da beide Properties direkt an `ItemsSource` gebunden sind (`AutonomAufgabeDetailView.xaml`, Zeilen ~89/95), aktualisiert die UI sich bei einem erneuten `Initialize()`-Aufruf auf derselben ViewModel-Instanz nicht. Der übrige Code (z. B. `TaskDetailViewModel.Protokolleintraege`) verwendet durchgängig `ObservableCollection` mit stabiler Objektidentität statt Neuzuweisung.

  Empfehlung: `Unteragenten`/`Skills` als `ObservableCollection<T>` mit `Clear()+AddRange`-Befüllung in `Initialize()` umsetzen.

- **Toter Code** — `AktualisierePlanAsync` (Zeile ~126) wird laut Referenzsuche im gesamten Repository ausschließlich aus dem Test `AutonomAufgabeDetailViewModelTests.cs` aufgerufen; in der App-Schicht existiert keine UI-Bindung/Command, die diese Methode erreichbar macht (der Plan-Tab in `AutonomAufgabeDetailView.xaml`, Zeilen ~58–65, bindet `PlanContent` zwar bidirektional, hat aber keinen Speichern-Button/Command).

  Empfehlung: Entweder einen Speichern-Command für den Plan-Tab ergänzen, der `AktualisierePlanAsync` aufruft, oder die Methode als bewusst vorbereitend kennzeichnen/entfernen.

### src/Softwareschmiede.Tests/App/ViewModels/AutonomAufgabeDetailViewModelTests.cs (AutonomAufgabeDetailViewModelTests)

- **Testabdeckung — fehlend** — Die öffentlichen Methoden `StoppeAgenAsync()` und `ResumeAgenAsync()` (inkl. `StopCommand`/`ResumeCommand`) werden von keinem Test aufgerufen. Ebenso ungetestet: `LaedeProgressAsync()` und `LaedeGovernanceAsync()` (nur `LaedePlanAsync()` ist abgedeckt, obwohl alle drei Lademethoden identisch aufgebaut sind). Zudem fehlt für alle drei Agent-Commands ein Test des Fehlerpfads (`catch`-Block, der `ErrorMessage` setzt) — aktuell wird nur der Erfolgsfall geprüft.

  Empfehlung: Tests für `StoppeAgenAsync`/`ResumeAgenAsync` ergänzen (Zustandsänderung im `SessionManagementService`, korrektes Zurücksetzen von `IsBusy`), Tests für `LaedeProgressAsync`/`LaedeGovernanceAsync` analog zu `LaedePlanAsync_LaedesDateiausArbeitsverzeichnis`, sowie mindestens einen Fehlerpfad-Test (Exception im darunterliegenden Service → `ErrorMessage` gesetzt, `IsBusy` wieder `false`).

- **Doppelter Code — Temp-Verzeichnis-Verwaltung** — Konstruktor/`Dispose()` erstellen und löschen `_testRoot` manuell (`Path.Combine(Path.GetTempPath(), "SoftwareschmiedeTests", ...)`, `Directory.CreateDirectory`, `Directory.Delete`), obwohl im Projekt bereits `Softwareschmiede.Tests.Helpers.TestTempDirectoryFixture` für genau diesen Zweck existiert und andernorts verwendet wird (z. B. `TaskDetailViewModelTestsBase`). Dasselbe Muster wiederholt sich identisch in `AutonomAufgabeInitialisierungsDialogViewModelTests.cs`, `AutonomAufgabenInitialisierungsServiceTests.cs`, `ProjektleiterAgentServiceTests.cs`, `SessionManagementServiceTests.cs` und `UnteragentGovernanceServiceTests.cs`.

  Empfehlung: In allen sechs genannten Testdateien `TestTempDirectoryFixture` statt manueller Verzeichnis-Erstellung/-Löschung verwenden.

### src/Softwareschmiede.Tests/App/ViewModels/AutonomAufgabeInitialisierungsDialogViewModelTests.cs (AutonomAufgabeInitialisierungsDialogViewModelTests)

- **Testabdeckung — fehlend** — `ValidiereEingaben()` im ViewModel hat drei unabhängige Validierungsfälle (InitialPrompt leer/<10 Zeichen, TokenBudget außerhalb 1..5.000.000, RuntimeLimitMinutes außerhalb 60..1440). Getestet ist nur der TokenBudget-Fall (`BestaetigenAsync_FailsOnInvalidTokenBudget`).

  Empfehlung: Je einen weiteren Test für ungültigen `InitialPrompt` und ungültiges `RuntimeLimitMinutes` ergänzen, analog zu `BestaetigenAsync_FailsOnInvalidTokenBudget`.

- **Doppelter Code — Temp-Verzeichnis-Verwaltung** — Zwei separat verwaltete Verzeichnisse (`_testRoot` und `_testRoot + "-quelle"`) mit doppelter Lösch-Logik in `Dispose()` (Zeilen ~66–74), was `TestTempDirectoryFixture.CreateTempDirectory` durch zwei Aufrufe ersetzen würde (siehe auch Befund zu `AutonomAufgabeDetailViewModelTests.cs`).

  Empfehlung: `TestTempDirectoryFixture` verwenden.

- **Doppelter Code — Moq-Setup für git clone** — Der Moq-Setup-Block für `git clone` (Zeilen ~33–36) ist inhaltlich nahezu identisch zu dem in `AutonomAufgabenInitialisierungsServiceTests.cs` (Zeilen ~31–44) und `ProjektleiterAgentServiceTests.cs` (Zeilen ~27–38).

  Empfehlung: Gemeinsame Test-Hilfsmethode (z. B. in `Softwareschmiede.Tests.Helpers`) zum Einrichten des `ICliRunner`-Mocks für `git clone` extrahieren und in allen drei Dateien verwenden.

### src/Softwareschmiede.Tests/Application/Services/AutonomAufgabenInitialisierungsServiceTests.cs (AutonomAufgabenInitialisierungsServiceTests)

- **Testabdeckung — fehlend** — `ValidiereAnfrage()` prüft fünf unabhängige Bedingungen (ungültiger `ProjektBranchName`, zu kurzer `InitialPrompt`, `TokenBudget` außerhalb 1..5.000.000, `LaufzeitLimitMinuten` außerhalb 60..1440, relativer `ArbeitsverzeichnispPfad`). Über `InitialisiereAsync` getestet ist nur der `TokenBudget`-Fall; die übrigen vier Validierungspfade der öffentlichen Methode `InitialisiereAsync` sind ungetestet.

  Empfehlung: Weitere Tests für ungültigen Branch-Namen, zu kurzen `InitialPrompt` und ungültiges `LaufzeitLimitMinuten` über `InitialisiereAsync` ergänzen.

- **Testabdeckung — fehlend** — Die Fehlerfälle in `KloneHauptRepositoryAsync` (kein `LokalerKlonPfad` gesetzt → `InvalidOperationException`, sowie fehlgeschlagener Klon → `InvalidOperationException` bei `!ergebnis.IsSuccess`) werden nicht getestet, obwohl beides über `InitialisiereAsync` erreichbare Fehlerpfade sind.

  Empfehlung: Test ergänzen, der eine Aufgabe ohne `LokalerKlonPfad` initialisiert bzw. den `ICliRunner`-Mock einen Fehlschlag simulieren lässt, und die erwartete Exception prüft.

- **Doppelter Code — Temp-Verzeichnis-Verwaltung / Moq-Setup** — Siehe Befunde zu `AutonomAufgabeDetailViewModelTests.cs` und `AutonomAufgabeInitialisierungsDialogViewModelTests.cs`.

### src/Softwareschmiede.Tests/Application/Services/ProjektleiterAgentServiceTests.cs (ProjektleiterAgentServiceTests)

- **Testabdeckung — fehlend** — `SteuereUnteragentAsync` hat mehrere ungetestete Fehlerpfade: (1) `ValidiereUnteragent` wirft `ArgumentException` bei leerem `AgentScope`/`AgentBranch` oder relativen `AgentDirectory`/`AgentClone`; (2) Governance-Ablehnung (`_governanceService.VerifiziereBerechtigung(...) == false` → `InvalidOperationException`, wenn `AgentDirectory` außerhalb des erlaubten Bereichs liegt); (3) fehlgeschlagener Klon (`!kloneErgebnis.IsSuccess` → `InvalidOperationException`). Nur der Erfolgspfad ist getestet (`SteuereUnteragentAsync_ErzeugtUnteragentSpezifikation`).

  Empfehlung: Je einen Test für die drei Fehlerpfade ergänzen, insbesondere für die Governance-Ablehnung, da dies der sicherheitsrelevante Kernaspekt des Unteragenten-Isolationsmodells ist.

- **Doppelter Code — Moq-Setup für git clone/branch** — Zeilen ~27–46 dupliziert Muster aus `AutonomAufgabenInitialisierungsServiceTests.cs` und `AutonomAufgabeInitialisierungsDialogViewModelTests.cs` (siehe dort).

  Empfehlung: Gemeinsame Test-Hilfsmethode verwenden (siehe Befund zu `AutonomAufgabeInitialisierungsDialogViewModelTests.cs`).

### src/Softwareschmiede.Tests/Application/Services/UnteragentGovernanceServiceTests.cs (UnteragentGovernanceServiceTests)

- **Testabdeckung — fehlend** — `ValidiereFehlerBedingungAsync` prüft zwei unabhängige Abbruchbedingungen (Tokenlimit-Überschreitung und Laufzeitlimit-Überschreitung), von denen nur die Tokenlimit-Verletzung getestet ist (`ValidiereFehlerBedingungAsync_ErkenntTokenLimitVerletzung`). Der parallele Zweig `state.RuntimeLimitMinutes > 0 && DateTimeOffset.UtcNow - state.StartedUtc > TimeSpan.FromMinutes(state.RuntimeLimitMinutes)` ist ungetestet.

  Empfehlung: Test ergänzen, der `started_utc` weit in der Vergangenheit und ein kleines `runtime_limit_minutes` setzt, und prüft, dass `UnteragentAbbruchException` mit entsprechender Meldung geworfen wird.

## Geprüfte Dateien

- `src/Softwareschmiede.App/App.xaml.cs`
- `src/Softwareschmiede.App/Services/IDialogService.cs`
- `src/Softwareschmiede.App/Services/WpfDialogService.cs`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml`
- `src/Softwareschmiede.App/ViewModels/AutonomAufgabeDetailViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/AutonomAufgabeInitialisierungsDialogViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/PermissionsJsonOption.cs`
- `src/Softwareschmiede.App/Views/AutonomAufgabeDetailDialog.xaml`
- `src/Softwareschmiede.App/Views/AutonomAufgabeDetailDialog.xaml.cs`
- `src/Softwareschmiede.App/Views/AutonomAufgabeDetailView.xaml`
- `src/Softwareschmiede.App/Views/AutonomAufgabeDetailView.xaml.cs`
- `src/Softwareschmiede.App/Views/AutonomAufgabeInitialisierungsDialog.xaml`
- `src/Softwareschmiede.App/Views/AutonomAufgabeInitialisierungsDialog.xaml.cs`
- `src/Softwareschmiede.Tests/E2E/MainTest.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/AutonomAufgabeDetailViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/AutonomAufgabeInitialisierungsDialogViewModelTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/AutonomAufgabenInitialisierungsServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/ProjektleiterAgentServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/SessionManagementServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/UnteragentGovernanceServiceTests.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_AutonomAufgabenAgentExecution.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_AutonomAufgabenInitialisierung.cs`
- `src/Softwareschmiede/Application/Services/AufgabeService.cs`
- `src/Softwareschmiede/Application/Services/AutonomAufgabenInitialisierungsService.cs`
- `src/Softwareschmiede/Application/Services/AutonomAufgabenOptions.cs`
- `src/Softwareschmiede/Application/Services/ProjektleiterAgentService.cs`
- `src/Softwareschmiede/Application/Services/SessionManagementService.cs`
- `src/Softwareschmiede/Application/Services/UnteragentGovernanceService.cs`
- `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`
- `src/Softwareschmiede/Domain/Entities/AutonomAufgabeKonfiguration.cs`
- `src/Softwareschmiede/Domain/Entities/SkillDefinition.cs`
- `src/Softwareschmiede/Domain/Entities/UnteragentSpezifikation.cs`
- `src/Softwareschmiede/Domain/Enums/AufgabeAusfuehrungsStatus.cs`
- `src/Softwareschmiede/Domain/Exceptions/DirectoryAccessException.cs`
- `src/Softwareschmiede/Domain/Exceptions/UnteragentAbbruchException.cs`
- `src/Softwareschmiede/Domain/ValueObjects/AutonomAufgabeInitialisierungsAnfrage.cs`
- `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs`
- `src/Softwareschmiede/Migrations/20260820175118_AddAutonomAufgabeModels.Designer.cs`
- `src/Softwareschmiede/Migrations/20260820175118_AddAutonomAufgabeModels.cs`
- `src/Softwareschmiede/Migrations/SoftwareschmiededDbContextModelSnapshot.cs`
- `src/Softwareschmiede/appsettings.json`

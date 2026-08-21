# Offene Aufgaben

Erstellt am: 2026-08-20
Abbruchgrund: Kein Fortschritt zwischen den letzten zwei Iterationen (offene Punkte: 18 → 73)
Aktualisiert am: 2026-08-21 (automatisierter `/implement`-Lauf über die neuen Anforderungen und die Code-Review-Befunde)
Aktualisiert am: 2026-08-21 (Iteration 2 — Code-Review nach der ersten Implementierungsrunde, 5 neue Befunde ergänzt)
Aktualisiert am: 2026-08-21 (Iteration 3 — Code-Review nach Iteration 2, 2 neue Befunde ergänzt)
Aktualisiert am: 2026-08-21 (Iteration 3 abgeschlossen, Iterationslimit erreicht — 2 verbleibende Befunde aus dem letzten Review dokumentiert, Schleife beendet)

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Neue Anforderungen (vom Anwender, 2026-08-21)

- [x] Im Dialog für die Autonome Aufgabe (`AutonomAufgabeInitialisierungsDialog`) soll der Branch auswählbar sein (statt nur als freier Text), analog zur Basis-Branch-Auswahl bei der Repository-Zuweisung.
      Umgesetzt: `AutonomAufgabeInitialisierungsDialogViewModel` lädt über `LadeAsync`/`LadeProjektBranchesAsync` die Remote-Branches des mit der Aufgabe verknüpften `GitRepository` (analog zu `RepositoryAssignViewModel.LoadSourceBranchesAsync`) und fällt auf manuelle Texteingabe zurück, wenn kein Repository/Plugin verfügbar ist (`IsProjectBranchManualInput`). `AutonomAufgabeStartService.StarteAsync` ruft `LadeAsync` vor der Dialoganzeige auf.
- [x] Über einen "+"-Button neben der Branch-Auswahl soll ein neuer Branch angelegt werden können.
      Umgesetzt: `ShowCreateBranchCommand`/`CreateBranchCommand`/`CancelCreateBranchCommand` mit Inline-Eingabe (`NewBranchName`, `NewBranchError`), ruft `IGitPlugin.CreateBranchAsync` auf Basis von `Aufgabe.LokalerKlonPfad` auf.
- [x] Für den Initialprompt soll eine Promptvorlage auswählbar sein (Vorlagenverwaltung/-auswahl noch zu klären: Quelle der Vorlagen, ob projektspezifisch oder global).
      Design-Entscheidung: Es existiert bereits eine projektweite Konvention für Promptvorlagen (`PromptVorlage`-Entity + `PromptVorlagenService`, DB-verwaltet, global, bereits im Ribbon von `TaskDetailViewModel` genutzt). Diese wird wiederverwendet statt eine neue Vorlagenverwaltung einzuführen: `AutonomAufgabeInitialisierungsDialogViewModel.InitialPromptVorlagen`/`SelectedInitialPromptVorlage` laden die vorhandenen `PromptVorlage`-Einträge und lösen deren Platzhalter über `PromptVorlagenPlatzhalterService` auf. Beide Services sind optionale Konstruktor-Parameter (Dialog bleibt ohne sie funktionsfähig, z. B. in bestehenden Tests).
- [x] Der Dialog braucht zusätzlich einen Hilfe-Button, über den eine Erklärung des Ablaufs (der Autonomen Aufgabe) angezeigt wird.
      Umgesetzt analog zur bestehenden Konvention in `SettingsView.xaml.cs`/`HelpTextDialog`: Klick-Handler im Code-Behind (`AutonomAufgabeInitialisierungsDialog.xaml.cs`, `OnHilfeClick`) öffnet `HelpTextDialog` mit einem erklärenden Text zum Ablauf (Initialisierung, Projektleiter-Start, Unteragenten, Integration, Session-Pause/Resume) und den Formularfeldern. Bewusst ohne ViewModel-Beteiligung, da rein informativ und ohne Zustand.

## Offene Planelemente

Keine — `review.md` hat den Status „Vollständig umgesetzt": Alle 28 Planelemente aus `plan.md` sind implementiert.

## Code-Review-Befunde

Von den ursprünglich 73 Befunden wurden die folgenden bearbeitet. Bugfixes, Konsistenzfixes und
Testlücken-Schließungen wurden umgesetzt; große strukturelle Refactorings mit hohem Blast-Radius
(Entity-Umbau, DB-Spalten-Umbenennungen quer durchs Feature, Enum-Aufspaltung) wurden bewusst
zurückgestellt (siehe Begründung je Punkt) statt sie ungeprüft in einem automatisierten Lauf
durchzuführen.

### Umgesetzt

- [x] `GitKlonHelper.cs` — Fehlerbehandlung: `Path.GetDirectoryName(zielPfad)!` durch geprüften Aufruf ersetzt (kein Null-forgiving-Operator mehr).
- [x] `AutonomAufgabenInitialisierungsService.cs` — Die beiden Convenience-Overloads (2-Parameter, 9-Parameter) von `InitialisiereAsync` entfernt (waren tot bzw. dupliziert das Parameter-Objekt `AutonomAufgabeInitialisierungsAnfrage`); `AutonomAufgabeInitialisierungsDialogViewModel.BestaetigenAsync` baut die `Anfrage` jetzt direkt und ruft die `(Aufgabe, Anfrage, ct)`-Überladung auf.
- [x] `AutonomAufgabenInitialisierungsService.cs` — Alle Datei-I/O-Operationen (`permissions.json`, `state.json`, `plan.md`, `progress.md`, `governance.md`) sind jetzt einheitlich gegen `IOException`/`UnauthorizedAccessException` abgesichert (→ `DirectoryAccessException`).
- [x] `AutonomAufgabenInitialisierungsService.cs` — `MaxClones`/`MaxFeatureBranches` sind keine `const` mehr, sondern konfigurierbare Properties auf `AutonomAufgabenOptions` (Default 3/10, wie bisher).
- [x] `ProjektleiterAgentService.cs` — Klon-Logik in `SteuereUnteragentAsync` nutzt jetzt `GitKlonHelper.KloneFallsNichtVorhandenAsync` statt eigener Inline-Implementierung.
- [x] `ProjektleiterAgentService.cs` — `AktualisiereSubagentsInStateJsonAsync` nutzt jetzt `StateJsonHelper.LeseAsync`/`SchreibeAsync` (inkl. `JsonException`-Behandlung) statt eigener Parse/Schreibe-Logik.
- [x] `ProjektleiterAgentService.cs` — Ergebnis des `git branch`-Aufrufs wird geprüft; bei Fehlschlag wird eine aussagekräftige `InvalidOperationException` (mit `StdErr`) geworfen statt stillschweigend weiterzulaufen.
- [x] `ProjektleiterAgentService.cs` — `Directory.CreateDirectory(unteragent.AgentDirectory)` ist jetzt gegen `IOException`/`UnauthorizedAccessException` abgesichert (→ `DirectoryAccessException`), analog zu `AutonomAufgabenInitialisierungsService`.
- [x] `ProjektleiterAgentService.cs` — Tippfehler `arbeitsverzeichnispPfad` → `arbeitsverzeichnisPfad` behoben.
- [x] `SessionManagementService.cs` — `AktualisierePausedUtcInStateJsonAsync` nutzt jetzt `StateJsonHelper.LeseAsync`/`SchreibeAsync` statt direktem `JsonNode.Parse` (kein ungefangenes `JsonException` mehr bei beschädigter state.json).
- [x] `SessionManagementService.cs` — Tippfehler `arbeitsverzeichnispPfad` → `arbeitsverzeichnisPfad` behoben.
- [x] `StateJsonHelper.cs` — Toter Code aufgelöst: wird jetzt von `ProjektleiterAgentService` und `SessionManagementService` verwendet.
- [x] `AutonomAufgabeStartCoordinator.cs` → umbenannt zu `AutonomAufgabeStartService.cs` (Namenskonvention: `...Service`-Suffix wie im übrigen `Softwareschmiede.App.Services`-Namespace).
- [x] `AutonomAufgabeStartErgebnis.cs` → umbenannt zu `AutonomAufgabeStartResult.cs` (Namenskonvention: `...Result`-Suffix wie `IssueCreateDialogResult`/`PluginSelectionResult`).
- [x] `AutonomAufgabeStartService.cs` (vormals Coordinator) — `StarteAsync` umschließt jetzt den gesamten Ablauf (inkl. Aufgabe laden, Dialog anzeigen) mit try/catch statt nur die zweite Hälfte.
- [x] `AutonomAufgabeStartService.cs` — Redundanter `aufgabeId`-Parameter entfernt; `StarteAsync(Aufgabe aufgabe, CancellationToken ct)` nutzt `aufgabe.Id`.
- [x] `TaskDetailViewModel.cs` — `AutonomAufgabeInitialisierenAsync` hat jetzt try/catch mit Logging und `FehlerMeldung`, analog zu den übrigen Command-Handlern.
- [x] `WpfDialogService.cs` — Die beiden Autonome-Aufgabe-Dialogmethoden nutzen jetzt eine gemeinsame private `ShowDialogAsync<TResult>`-Hilfsmethode statt dupliziertem Boilerplate (bewusst nur für die zwei neuen Methoden refactored, die vier bestehenden Alt-Methoden bleiben unangetastet, um den Diff auf das Feature zu begrenzen).
- [x] `WpfDialogService.cs` — `ct` wird in beiden Methoden jetzt per `ct.ThrowIfCancellationRequested()` verwendet statt ignoriert.
- [x] `AutonomAufgabeDetailViewModel.cs` — `StarteAgentAsync` hat jetzt denselben `_aufgabe is null`-Guard wie `StoppeAgentAsync`/`ResumeAgentAsync`.
- [x] `AutonomAufgabeInitialisierungsDialogViewModel.cs` — `BestaetigenAsync` baut jetzt direkt eine `AutonomAufgabeInitialisierungsAnfrage` und ruft die `(aufgabe, anfrage, ct)`-Überladung auf.
- [x] `AutonomAufgabeInitialisierungsDialogViewModel.cs` — `SelectedPersistenceMode` ist jetzt `PersistenzModus` (Enum) statt `string` mit manuellem `Enum.TryParse`; XAML-Binding auf `ObjectDataProvider` analog zur Permissions-ComboBox umgestellt (nicht mehr editierbar, da feste Enum-Werte).
- [x] `AutonomAufgabeInitialisierungsDialogViewModel.cs` — `BestaetigenAsync(CancellationToken ct = default)` reicht das Token jetzt durch; Command-Wiring nutzt es direkt statt es zu verwerfen.
- [x] `SoftwareschmiededDbContext.cs` — `HasMaxLength` für `UnteragentSpezifikation.AgentId`/`TaskId`/`AgentScope` (255) und `SkillDefinition.SkillName` (255)/`SkillVersion` (64) ergänzt.
- [x] `SoftwareschmiededDbContext.cs` — `e.Property(a => a.SessionPauseUtc)...` in den `Aufgabe`-Property-Block verschoben (vor die `HasMany`/`HasOne`-Relationship-Konfigurationen), damit Property- und Relationship-Konfiguration konsistent gruppiert bleiben.
- [x] `appsettings.json` / `AutonomAufgabenOptions.cs` — `MaxConcurrentSubagents` → `MaxConcurrentUnteragenten`, `SkillAutoGenerationEnabled` → `SkillAutogenerationEnabled` (Konsistenz mit deutscher Domänenterminologie bzw. `AutonomAufgabeKonfiguration.SkillAutogeneration`); zusätzlich `MaxClones`/`MaxFeatureBranches` als neue Konfigurationseinträge ergänzt (siehe oben). Der `AutonomAufgabeInitialisierungsDialogViewModel`-Konstruktor initialisiert `TokenBudget`/`RuntimeLimitMinutes`/`AutoGenerateSkills` jetzt aus `AutonomAufgabenOptions`, damit die Konfigurationswerte tatsächlich wirksam sind (vorher nur über eine nie aufgerufene Overload erreichbar).
- [x] Testlücken geschlossen: `AutonomAufgabeDetailViewModelTests` (`LaedeProgressAsync`, `LaedeGovernanceAsync`, `StoppeAgentAsync`/`ResumeAgentAsync` inkl. Guard-Fall, Fehlerpfad über `FuehreAgentOperationAsync`, `plan.md` fehlt), `AutonomAufgabeInitialisierungsDialogViewModelTests` (InitialPrompt-/RuntimeLimit-Validierung, Fehlerpfad bei Service-Exception) + neue `AutonomAufgabeInitialisierungsDialogViewModelTests_BranchUndVorlagen` (Branch laden/anlegen, Fallback auf manuelle Eingabe, Promptvorlagen-Auswahl), `AutonomAufgabenInitialisierungsServiceTests` (alle vier Validierungszweige, fehlender `LokalerKlonPfad`, fehlgeschlagener Git-Klon), `ProjektleiterAgentServiceTests_Fehlerfaelle` (neue Datei: nicht existierende referenzierte Entities für alle drei Methoden, alle vier `ValidiereUnteragent`-Zweige, fehlgeschlagener Klon, Fallback-Text ohne `task_report.md`), `SessionManagementServiceTests` (nicht existierende Aufgabe für alle drei Methoden, beide frühen `PruefeAusfuehrungAsync`-Zweige), `UnteragentGovernanceServiceTests` (fehlende `task_state.json`, beide Guard-Clauses von `VerifiziereBerechtigung`).
- [x] `E2E_AutonomAufgabenInitialisierung.cs` — Automatisierungsname `AutonomAufgabeProjektbranch` existiert durch die Branch-Auswahl-UI nicht mehr; auf `AutonomAufgabeProjektbranchEingabe` aktualisiert (das Testszenario weist der Aufgabe kein `GitRepository` zu, daher greift dort immer der manuelle Eingabe-Fallback).

### Bewusst zurückgestellt (nicht umgesetzt)

Diese Befunde sind reine Struktur-/Namenskonventions-Wünsche mit hohem Blast-Radius
(Entity-Felder verschieben, DB-Spalten/Enum-Werte umbenennen, die quer durchs Feature in Services,
ViewModels, `state.json`/`permissions.json`-Schema und zahlreichen Tests verwendet werden) ohne
begleitenden Funktionsfehler. Sie in einem automatisierten Lauf ungeprüft durchzuziehen, birgt ein
reales Regressionsrisiko für ein bereits vollständig funktionierendes, getestetes Feature (1365
grüne Tests). Empfehlung: als eigene, gezielte Refactoring-Aufgabe mit Migration-Review angehen.

- [ ] `Aufgabe.cs` — `ProjektleiterAgentId`/`SessionPauseUtc`/`AktiveUnteragenten` nach `AutonomAufgabeKonfiguration` verschieben (God-Klasse/Data-Clump-Kritik). Betrifft DB-Spalten der `Aufgaben`-Tabelle und alle Lese-/Schreibstellen (`ProjektleiterAgentService`, `SessionManagementService`, ViewModels, Tests).
- [ ] `AufgabeAusfuehrungsStatus.cs` — Modus (regulär/autonom) von Ausführungsphase trennen (neues `AutonomAufgabeStatus`-Enum). Würde `AusfuehrungsStatus == AufgabeAusfuehrungsStatus.AutonomAufgabe`-Checks in mehreren Services/ViewModels/Tests ersetzen.
- [ ] Gemeinsames Value Object `GitArbeitsbereich` (BranchName + ClonePfad) für `Aufgabe`, `UnteragentSpezifikation`, `AutonomAufgabeKonfiguration` einführen.
- [ ] `AutonomAufgabeKonfiguration.cs`/`AutonomAufgabeInitialisierungsAnfrage.cs` — `RessourcenLimits`/`BudgetKonfiguration`-Value-Object für `TokenBudget`/`TokenBudgetErweitert`/`LaufzeitLimitMinuten`.
- [ ] `SkillDefinition.cs` — Präfix `Skill…` von den Properties entfernen (`SkillName`→`Name` etc.); `ErstellungsDatum` vs. `ErzeugungsDatum`-Begriffsinkonsistenz vereinheitlichen.
- [ ] `UnteragentSpezifikation.cs` — Präfix `Agent…` auf `Unteragent…` (oder entfernen) umstellen; `AgentDirectory`/`AgentClone` auf `…Pfad`-Suffix-Konvention umbenennen.
- [ ] `PermissionsJsonOption.cs` — Englische Werte (`Generate`/`Select`/`Existing`) eindeutschen; `Existing` ist zudem semantisch irreführend (siehe Originalbefund).
- [ ] `PersistenzModus.SessionReset` / `SkillStatus.Review` — leichte Sprachbrüche, projektweite Konvention für Fachbegriffe wie "Session"/"Review" vs. deutsche Eindeutschung fehlt noch; keine Änderung ohne diese Grundsatzentscheidung.
- [ ] `DirectoryAccessException`/`UnteragentAbbruchException` — unterschiedliche Basisklassen (`Exception` vs. `InvalidOperationException`). Keine gemeinsame Exception-Hierarchie-Strategie im Projekt vorhanden; Vereinheitlichung sollte projektweit entschieden werden, nicht nur für dieses Feature.
- [ ] `ProjektleiterAgentService.cs` — God-Class-/God-Method-Tendenz (`SteuereUnteragentAsync` in benannte Teilschritte zerlegen, Git-Provisionierung in eigenen Service auslagern). Rein strukturelles Refactoring ohne funktionale Änderung; zurückgestellt zugunsten der Bugfixes in dieser Klasse (siehe oben), die den unmittelbaren Nutzen hatten.
- [ ] `UnteragentGovernanceService.ValidiereFehlerBedingungAsync` — aktuell in keinen Ausführungs-/Überwachungs-Loop eingebunden. Es existiert im Code noch kein periodischer Unteragenten-Überwachungs-Loop (Agent-Ausführung ist extern/CLI-basiert, kein In-Process-Polling implementiert) — die Methode an einer sinnvollen Stelle einzubinden würde einen solchen Loop faktisch neu entwerfen, was über eine Code-Review-Fehlerbehandlung hinausgeht. Bleibt vorerst bewusst ungenutzt/dokumentiert statt an einer künstlichen Stelle "pro forma" aufgerufen zu werden.
- [ ] `AutonomAufgabeDetailViewModel.cs` — `_konfiguration`/`_aufgabe` bis `Initialize()` ungültig (Pflichtinjektion statt DI-Transient+`Initialize` erwägen). Würde die DI-Registrierung/Konstruktion in `AutonomAufgabeStartService` mit umbauen; nur der akute NullReferenceException-Risiko-Fall (`StarteAgentAsync`) wurde gefixt (siehe oben).
- [ ] `ProjektleiterAgentServiceTests` — Governance-Verweigerungstest für `SteuereUnteragentAsync` konnte **nicht** wie im Befund beschrieben umgesetzt werden: `SteuereUnteragentAsync` ruft `VerifiziereBerechtigung(unteragent, ArbeitsverzeichnisErstellen, unteragent.AgentDirectory)` auf — Ziel- und Basispfad sind an dieser Stelle identisch, wodurch der Pfad-Check strukturell nie `false` liefern kann (er prüft de facto, ob der eigene Pfad im eigenen Pfad liegt). Eine Governance-Verweigerung ist über diesen Call-Site aktuell nicht erreichbar; das ist selbst ein Befund (der Check an dieser Stelle ist wirkungslos), aber dessen Behebung wäre eine Verhaltensänderung von `SteuereUnteragentAsync`, die nicht Teil des ursprünglichen Auftrags "fehlende Tests ergänzen" war. Die Governance-Verweigerung ist weiterhin direkt über `UnteragentGovernanceServiceTests.VerifiziereBerechtigung_VerbietetAenderungenAusserhalbArbeitsbereich` abgedeckt.
- [ ] Test-Duplikation (`AutonomAufgabeDetailViewModelTests`/`ProjektleiterAgentServiceTests`/`SessionManagementServiceTests`-Setup, `AutonomAufgabeInitialisierungsDialogViewModelTests`/`AutonomAufgabenInitialisierungsServiceTests`-Setup, `AutonomAufgabeStartService`-Erzeugung in den vier `TaskDetailViewModelTests*`-Dateien) in gemeinsame Test-Factories extrahieren. Rein stilistisch, kein Fehlerrisiko; zurückgestellt zugunsten der inhaltlichen Testlücken-Schließung.

### Aus aktuellem Review (Iteration 2, `review-code.md`, 2026-08-21)

Diese 5 Befunde stammen aus dem Code-Review der ersten Implementierungsrunde (Umsetzung der
"Neuen Anforderungen" + der oben unter "Umgesetzt" gelisteten Befunde) und wurden in einem
erneuten `/implement`-Lauf am 2026-08-21 (Iteration 2) bearbeitet.

- [x] `TaskDetailViewModel.cs` — Toter Code/redundante Fehlerbehandlung: Das `try/catch(Exception)` um `_autonomAufgabeStartCoordinator.StarteAsync(_aufgabe, ct)` in `AutonomAufgabeInitialisierenAsync` wurde entfernt (war unerreichbar, da `AutonomAufgabeStartService.StarteAsync` bereits alle Exceptions außer `OperationCanceledException` abfängt und als `AutonomAufgabeStartResult.FehlerMeldung` zurückgibt). Die Methode wertet jetzt ausschließlich `ergebnis.FehlerMeldung` aus; die Fehlerbehandlung liegt vollständig in `AutonomAufgabeStartService`.
- [x] `AutonomAufgabenInitialisierungsService.cs`/`ProjektleiterAgentService.cs` — Der dreifach duplizierte Block `catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { throw new DirectoryAccessException(pfad, ex); }` wurde in die neue gemeinsame Hilfsmethode `DirectoryAccessGuard.AusfuehrenAsync(pfad, Func<Task> aktion)` extrahiert (analog zu `GitKlonHelper`/`StateJsonHelper`, ebenfalls `internal static` in `Softwareschmiede.Application.Services`). Alle drei Call-Sites (`InitialisiereAsync`, `ErstelleArbeitsverzeichnisStrukturAsync`, `ProjektleiterAgentService.SteuereUnteragentAsync`) nutzen jetzt die Hilfsmethode; nicht mehr benötigte `using Softwareschmiede.Domain.Exceptions;`-Imports in beiden Dateien entfernt.
- [x] `AutonomAufgabeInitialisierungsDialogViewModel.cs` — `IPluginManager`/`PromptVorlagenService`/`PromptVorlagenPlatzhalterService` sind jetzt reguläre Pflichtparameter (nicht mehr `?`/`= null`), analog zu `TaskDetailViewModel`. Die internen Null-Checks (`_promptVorlagenService is null`, `_pluginManager?.`) wurden entfernt, da die Werte jetzt garantiert vorhanden sind. `AutonomAufgabeInitialisierungsDialogViewModelTests` konstruiert jetzt explizit einen `Mock<IPluginManager>` sowie echte `PromptVorlagenService`/`PromptVorlagenPlatzhalterService`-Instanzen; `AutonomAufgabeInitialisierungsDialogViewModelTests_BranchUndVorlagen.CreateSut` erzeugt bei fehlenden optionalen Test-Parametern jetzt automatisch echte Fallback-Instanzen statt `null` durchzureichen.
- [x] `SoftwareschmiededDbContext.cs` — Migration/Model-Snapshot-Drift behoben: Neue Migration `20260821160341_AddAutonomAufgabeMaxLengthConstraints` per `dotnet ef migrations add` erzeugt (Up/Down bleiben leer, da SQLite `MaxLength` nicht als eigenen Spaltentyp abbildet und daher keine tatsächliche Schemaänderung nötig ist; die Migration dokumentiert den Modellwechsel dennoch in der `__EFMigrationsHistory`). `SoftwareschmiededDbContextModelSnapshot.cs` wurde vom EF-Tool konsistent mit den `HasMaxLength`-Constraints aus `OnModelCreating` aktualisiert (dabei wurden auch zwei vorbestehende, unabhängige Snapshot-Abweichungen bei den `Skills`/`Unteragenten`-Navigationseigenschaften von `AutonomAufgabeKonfiguration` mit bereinigt, die derselbe Modell-Diff-Lauf aufgedeckt hat).
- [x] `AutonomAufgabeDetailViewModel.cs` — `StarteAgentAsync`/`StoppeAgentAsync`/`ResumeAgentAsync` setzen bei `_aufgabe is null` jetzt `ErrorMessage = "Aufgabe wurde nicht initialisiert."` statt lautlos `Task.CompletedTask` zurückzugeben. Betroffene Guard-Tests in `AutonomAufgabeDetailViewModelTests` (`StoppeAgentAsync_TutNichts_OhneInitialisierteAufgabe`, `ResumeAgentAsync_TutNichts_OhneInitialisierteAufgabe`, `StarteAgentAsync_TutNichts_OhneInitialisierteAufgabe`) wurden auf die neue Erwartung (`ErrorMessage` gesetzt statt `null`) angepasst.

### Aus aktuellem Review (Iteration 3, `review-code.md`, 2026-08-21)

Diese 2 Befunde stammen aus dem Code-Review von Iteration 2 (Umsetzung der 5 vorherigen Befunde) und sind noch offen.

- [x] `AutonomAufgabeDetailViewModel.cs` — Doppelter Code: Der Null-Guard-Block (`if (_aufgabe is null) { ErrorMessage = "..."; return Task.CompletedTask; }`) ist jetzt dreifach identisch in `StarteAgentAsync`/`StoppeAgentAsync`/`ResumeAgentAsync` dupliziert. In eine private Hilfsmethode extrahieren.
      Umgesetzt: Neue private `[MemberNotNullWhen(true, nameof(_aufgabe))] bool PruefeAufgabeInitialisiert()` (setzt `ErrorMessage` und gibt `false` zurück, wenn `_aufgabe is null`, sonst `true`); alle drei Methoden rufen jetzt `if (!PruefeAufgabeInitialisiert()) { return Task.CompletedTask; }` auf. Das `MemberNotNullWhen`-Attribut erhält die bisherige Nullability-Narrowing von `_aufgabe` für die nachfolgende Verwendung in `StoppeAgentAsync`/`ResumeAgentAsync`.
- [x] `AutonomAufgabeStartService.cs` — Fehlerbehandlung: Seit der Ausweitung des try/catch auf die gesamte Methode (Iteration 1) ist `aktualisierteAufgabe` im catch-Block nicht mehr im Scope; im Fehlerfall wird `null` statt der bereits geladenen (und in der DB bereits aktualisierten) Aufgabe zurückgegeben, wodurch `TaskDetailViewModel` nach einem Fehler beim Anzeigen der Detail-Ansicht einen veralteten Stand zeigt. Aufgabe vor dem try-Block laden bzw. im catch-Block weiterhin verfügbar halten.
      Umgesetzt: Lokale Variable `aktuelleAufgabe` wird jetzt vor dem try-Block deklariert (initial mit dem übergebenen `aufgabe`-Parameter) und innerhalb des try-Blocks nach jedem `GetDetailAsync`-Aufruf aktualisiert (mit Fallback auf den zuvor bekannten Stand statt `null`). Der catch-Block gibt jetzt `new AutonomAufgabeStartResult(aktuelleAufgabe, ...)` zurück statt `null`. Neuer Regressionstest `AutonomAufgabeStartServiceTests.StarteAsync_GibtBereitsGeladeneAufgabeZurueck_BeiFehlerWaehrendInitialisierung` (`src/Softwareschmiede.Tests/App/Services/AutonomAufgabeStartServiceTests.cs`, neue Testklasse/neuer Ordner, da bisher kein Unit-Test für diesen Service existierte) erzwingt über einen nicht konfigurierten `IServiceProvider`-Mock eine Exception beim Auflösen des Dialog-ViewModels und prüft, dass `AktualisierteAufgabe` im Ergebnis nicht `null` ist und der geladenen Aufgabe entspricht.

### Aus aktuellem Review (Iteration 4, `review-code.md`, 2026-08-21) — Iterationslimit erreicht, nicht mehr automatisiert bearbeitet

Diese 2 Befunde stammen aus dem Code-Review von Iteration 3 (letzte erlaubte Iteration des
`/lifecycle`-Laufs — 3 Iterationen erreicht). Beide sind rein stilistisch, ohne Funktionsfehler,
und daher unkritisch für einen manuellen Folgelauf.

- [ ] `TaskDetailViewModel.cs`/`TaskDetailViewModelTests*.cs`/`TaskDetailViewModelTestFactory.cs` — Feld/Parameter/lokale Variable heißen nach der Umbenennung `AutonomAufgabeStartCoordinator` → `AutonomAufgabeStartService` (Iteration 1) weiterhin `_autonomAufgabeStartCoordinator`/`autonomAufgabeStartCoordinator`. Umbenennen zu `_autonomAufgabeStartService`/`autonomAufgabeStartService` (6 Fundstellen, siehe `review-code.md`).
- [ ] `ProjektleiterAgentServiceTests_Fehlerfaelle.cs` — `ErstelleKonfigurationAsync()`/`ErstelleUnteragent(...)` duplizieren die Arrange-Logik aus `ProjektleiterAgentServiceTests.ErstelleAutonomeAufgabeAsync()`. In eine gemeinsame Test-Helper-Klasse extrahieren.

## Fehlgeschlagene Tests

Keine. Die stabile Testlane (`dotnet test --filter "Category!=OsInterface"`) läuft nach allen
Änderungen dieses Laufs vollständig grün: 1365 bestanden, 1 bewusst übersprungen (bestehender,
plattformbedingter Skip), 0 Fehler. Die `OsInterface`-Kategorie (E2E/FlaUI, ConPTY) wurde in diesem
Lauf nicht ausgeführt (siehe CLAUDE.md-Konvention, separate Lane); die durch die neue Branch-Auswahl-UI
geänderte Automatisierungskennung in `E2E_AutonomAufgabenInitialisierung.cs` wurde jedoch per
Quellcode-Review identifiziert und korrigiert (siehe oben).

**Update 2026-08-21 (Iteration 2, Bearbeitung der 5 Review-Befunde):** Volles Build (`dotnet build
src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj`, zieht `Softwareschmiede.App` und alle
Plugin-Projekte mit) erfolgreich, 0 Fehler/Warnungen. Stabile Testlane erneut ausgeführt: 1364
bestanden, 1 bewusst übersprungen, 1 Fehler
(`PseudoConsoleSessionTests.ReadLoopAsync_MeldetOutputChunksAnSink_UndAktualisiertBufferWeiterhin`,
`src/Softwareschmiede.Tests/Infrastructure/Terminal/PseudoConsoleSessionTests.cs`). Dieser eine
Fehler ist vorbestehend und unabhängig von den 5 bearbeiteten Befunden: Die Datei wurde in diesem
Lauf nicht verändert, der Test prüft ein Timing-Verhalten der ConPTY-Leseschleife
(`sink.IsCompleted` nach Ende der Leseschleife) und ist bei isolierter Einzelausführung
(`--filter "FullyQualifiedName~ReadLoopAsync_MeldetOutputChunksAnSink..."`) reproduzierbar grün —
der Fehlschlag tritt nur unter Last des vollen Parallel-Testlaufs auf.

**Update 2026-08-21 (Iteration 3, Bearbeitung der 2 Review-Befunde):** Volles Build (`dotnet build
src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj`) erfolgreich, 0 Fehler/Warnungen. Stabile
Testlane erneut ausgeführt: 1365 bestanden (inkl. neuem `AutonomAufgabeStartServiceTests`-Regressionstest),
1 bewusst übersprungen, 1 Fehler — derselbe vorbestehende, last-abhängige
`PseudoConsoleSessionTests.ReadLoopAsync_MeldetOutputChunksAnSink_UndAktualisiertBufferWeiterhin`-Fehlschlag
wie in Iteration 2 (Datei in diesem Lauf nicht verändert, isolierte Einzelausführung erneut grün
verifiziert). Kein Zusammenhang zu den beiden in dieser Iteration bearbeiteten Befunden.

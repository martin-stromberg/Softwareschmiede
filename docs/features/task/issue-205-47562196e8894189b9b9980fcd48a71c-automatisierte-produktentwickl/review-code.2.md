# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### Aufgabe.cs (Aufgabe)

- **Struktur/Verantwortlichkeiten (God-Klasse verstärkt)** — Die neuen Felder `ProjektleiterAgentId`, `SessionPauseUtc`, `AktiveUnteragenten` sind laut eigenem XML-Kommentar "(nur für Autonome Aufgaben)" relevant, werden aber direkt auf `Aufgabe` statt auf der bereits vorhandenen `AutonomAufgabeKonfiguration` (1:1-Beziehung über `AufgabeId`) untergebracht. `Aufgabe` trägt damit drei orthogonale Zustandsdimensionen gleichzeitig: Basis-Metadaten, reguläre CLI-Ausführung und Projektleiter-/Unteragenten-Laufzeitstatus.

  Empfehlung: `ProjektleiterAgentId`, `SessionPauseUtc`, `AktiveUnteragenten` nach `AutonomAufgabeKonfiguration` verschieben, damit `Aufgabe` nicht weiter mit modusabhängigen Feldern wächst.

- **Code Smell (Temporäres Feld)** — Alle drei neuen Felder (`ProjektleiterAgentId`, `SessionPauseUtc`, `AktiveUnteragenten`) sind nur gültig, wenn `AusfuehrungsStatus == AufgabeAusfuehrungsStatus.AutonomAufgabe`, sonst bedeutungslos/null.

  Empfehlung: Durch Verschiebung in `AutonomAufgabeKonfiguration` (siehe oben) auflösen.

- **Doppelter Code/fehlende Kapselung (Data Clump über Dateien hinweg)** — `BranchName`/`LokalerKlonPfad` bilden dasselbe Konzept wie `UnteragentSpezifikation.AgentBranch`/`AgentClone`/`AgentDirectory` und `AutonomAufgabeKonfiguration.ProjektBranchName`/`ArbeitsverzeichnisPfad`. Branch-Name + lokaler Pfad wird in drei Entitäten mit jeweils eigenem Namensschema neu modelliert statt als gemeinsamer Typ wiederverwendet.

  Empfehlung: Gemeinsames Value Object (z. B. `GitArbeitsbereich` mit `BranchName` + `ClonePfad`) einführen und in allen drei Entitäten referenzieren statt drei Mal Ad-hoc-Strings.

### AufgabeAusfuehrungsStatus.cs (AufgabeAusfuehrungsStatus)

- **Code Smell (Enum vermischt zwei Achsen)** — Die Werte `NichtGestartet`, `Aktiv`, `Beendet` beschreiben eine Ausführungs-*Phase*, `AutonomAufgabe` beschreibt hingegen einen *Modus/Typ* der Aufgabe, keine Phase. Dadurch ist unklar, welchen Ausführungsstatus (aktiv/pausiert/beendet) eine Autonome Aufgabe hat — diese Information wird stattdessen implizit über andere Felder (`SessionPauseUtc`, `ProjektleiterAgentId`) auf `Aufgabe` nachgebildet.

  Empfehlung: Modus (regulär vs. autonom) von der Ausführungsphase trennen, z. B. eigenes `AutonomAufgabeStatus`-Enum (Aktiv/Pausiert/Beendet) analog zu `UnteragentStatus`, statt einen zusätzlichen Enum-Wert in `AufgabeAusfuehrungsStatus` zu verschmelzen.

### AutonomAufgabeKonfiguration.cs (AutonomAufgabeKonfiguration)

- **Code Smell (Data Clump)** — `TokenBudget`, `TokenBudgetErweitert`, `LaufzeitLimitMinuten` bilden eine zusammengehörige Gruppe ("Ressourcenlimits"), die unverändert auch in `AutonomAufgabeInitialisierungsAnfrage` auftaucht. Kein eigener Typ dafür vorhanden.

  Empfehlung: Value Object `RessourcenLimits`/`BudgetKonfiguration` (TokenBudget, TokenBudgetErweitert, LaufzeitLimitMinuten) einführen, in Entity und Value Object wiederverwenden.

### SkillDefinition.cs (SkillDefinition)

- **Namenskonvention (inkonsistentes Präfixing)** — `SkillName`, `SkillVersion`, `SkillContent`, `SkillStatus` präfixen jede Property mit dem Klassennamen "Skill". Im bestehenden Code (`Aufgabe.cs`, `AutonomAufgabeKonfiguration.cs`) werden Properties nicht mit dem eigenen Entity-Namen präfixt (`Titel`, nicht `AufgabeTitel`). `UnteragentSpezifikation.cs` verwendet mit "Agent"-Präfix ein drittes, wieder anderes Schema.

  Empfehlung: Präfixe entfernen (`Name`, `Version`, `Content`, `Status`) analog zum Stil in `Aufgabe`/`AutonomAufgabeKonfiguration`.

- **Namenskonvention (Begriffsinkonsistenz "Erstellung" vs. "Erzeugung")** — `ErstellungsDatum` passt zu `Aufgabe.ErstellungsDatum`, weicht aber von `UnteragentSpezifikation.ErzeugungsDatum` ab, obwohl beides denselben Zeitpunkt-Begriff beschreibt.

  Empfehlung: Einheitlichen Begriff für alle Entitäten wählen (z. B. durchgängig `ErstellungsDatum`).

### UnteragentSpezifikation.cs (UnteragentSpezifikation)

- **Namenskonvention (inkonsistentes Präfixing)** — Properties sind mit "Agent" statt mit dem Klassennamen "Unteragent" präfixt (`AgentId`, `AgentScope`, `AgentPrompt`, `AgentDirectory`, `AgentBranch`, `AgentClone`). Weder passend zum eigenen Klassennamen noch zum Präfix-Schema von `SkillDefinition` ("Skill…").

  Empfehlung: Präfixe entfernen oder einheitlich auf "Unteragent" setzen; Konvention projektweit für neue Entities festlegen.

- **Namenskonvention (fehlende "…Pfad"-Suffix-Konvention)** — `AgentDirectory` und `AgentClone` sind laut eigenem Kommentar Pfade, folgen aber nicht der im übrigen Code etablierten Suffix-Konvention `…Pfad` (vgl. `Aufgabe.LokalerKlonPfad`, `AutonomAufgabeKonfiguration.ArbeitsverzeichnisPfad`/`PermissionsJsonPfad`).

  Empfehlung: Umbenennen zu z. B. `AgentVerzeichnisPfad`, `AgentClonePfad` (oder ohne Präfix: `VerzeichnisPfad`, `ClonePfad`).

- **Doppelter Code/Data Clump** — `AgentBranch`/`AgentClone`/`AgentDirectory` bilden dasselbe "Branch+lokaler Pfad"-Konzept wie `Aufgabe.BranchName`/`LokalerKlonPfad` — dritte, wiederum andere Modellierung desselben Sachverhalts.

  Empfehlung: siehe Empfehlung zu Aufgabe.cs (gemeinsames Value Object).

### PermissionsJsonOption.cs (PermissionsJsonOption)

- **Namenskonvention (Sprachbruch)** — Einziges der neuen Enums, dessen Werte vollständig englisch sind (`Generate`, `Select`, `Existing`), während alle Geschwister-Enums (`PersistenzModus`, `SkillStatus`, `UnteragentAktion`, `UnteragentStatus`, `AufgabeAusfuehrungsStatus`) deutsche Bezeichner verwenden.

  Empfehlung: Auf deutsche Bezeichner umstellen, z. B. `Generieren`, `Auswaehlen`, `Vordefiniert`.

- **Namenskonvention/Semantik (irreführender Wert `Existing`)** — `Select` ist laut Kommentar "Eine **bestehende** permissions.json wird ausgewählt", `Existing` ist laut Kommentar "Eine **vordefinierte** permissions.json wird verwendet". Der Name `Existing` beschreibt somit nicht das, was tatsächlich "bestehend" ist (das ist `Select`), sondern eine vordefinierte/mitgelieferte Datei.

  Empfehlung: `Existing` in `Predefined`/`Vordefiniert` umbenennen, um die Verwechslung mit `Select` zu vermeiden.

### PersistenzModus.cs (PersistenzModus)

- **Namenskonvention (leichter Sprachbruch)** — `SessionReset` ist ein englischer Verbund-Begriff, während `Standard` sprachneutral ist. Das Projekt verwendet für "Lauf/Session"-Konzepte bereits ein deutsches Pendant (`LaufStatus`, `LaufzeitLimitMinuten`).

  Empfehlung: Konsistent zu deutschen Enum-Namen z. B. `SitzungZuruecksetzen`, oder projektweite Ausnahme für "Session" als etablierten Fachbegriff explizit festlegen.

### SkillStatus.cs (SkillStatus)

- **Namenskonvention (leichter Sprachbruch)** — `Review` ist ein englisches Lehnwort inmitten sonst durchgehend deutscher Werte (`Entwurf`, `Freigegeben`, `Archiviert`).

  Empfehlung: Falls konsequente Eindeutschung gewünscht ist, z. B. `Pruefung`; andernfalls als bewusste Ausnahme dokumentieren.

### DirectoryAccessException.cs (DirectoryAccessException)

- **Kopplung/Konsistenz (Exception-Hierarchie)** — Erbt direkt von `Exception`, während die zweite neue Exception-Klasse im selben Feature (`UnteragentAbbruchException`) von `InvalidOperationException` erbt. Keine erkennbare Begründung für die unterschiedliche Basisklassenwahl.

  Empfehlung: Einheitliche Basisklassen-Strategie für Domain-Exceptions festlegen (z. B. gemeinsame abstrakte Basisklasse, oder explizit dokumentieren, warum IO-Fehler anders klassifiziert werden als Abbruchbedingungen).

### AutonomAufgabeInitialisierungsAnfrage.cs (AutonomAufgabeInitialisierungsAnfrage)

- **Code Smell (Long Parameter List)** — Record-Konstruktor mit 9 Parametern, deutlich über dem Schwellenwert von 3–4. Enthält u. a. den Ressourcenlimit-Clump (`TokenBudget`, `TokenBudgetErweitert`, `LaufzeitLimitMinuten`) ein zweites Mal unverändert (siehe `AutonomAufgabeKonfiguration.cs`).

  Empfehlung: Parameter in Untergruppen zusammenfassen (z. B. `RessourcenLimits`-Value-Object), um die Parameterliste zu verkürzen und Duplikation zu vermeiden.

- **Fehlerbehandlung (fehlende Validierung von Vorbedingungen)** — Kein Konstruktor-Body/keine Validierung für Invarianten (z. B. `TokenBudget > 0`, `ProjektBranchName`/`InitialPrompt`/`ArbeitsverzeichnisPfad` nicht leer, `LaufzeitLimitMinuten > 0`).

  Empfehlung: Validierende Factory-Methode oder Konstruktor-Body mit Guard-Clauses ergänzen, statt Validierung implizit dem Aufrufer/Service zu überlassen.

- **Namenskonvention (Property-Name weicht vom Typnamen ab)** — Property heißt `PermissionsQuelle`, während der Typ `PermissionsJsonOption` heißt. Im selben Record folgt `PersistenzModus` hingegen der Konvention "Property-Name = Typname".

  Empfehlung: Entweder konsequent `PropertyName == TypName` (`PermissionsJsonOption`) oder konsequent sprechende Property-Namen für alle Enum-Properties verwenden.

### GitKlonHelper.cs (GitKlonHelper)

- **Fehlerbehandlung** — `Directory.CreateDirectory(Path.GetDirectoryName(zielPfad)!)` verwendet den Null-forgiving-Operator ohne Prüfung. Ist `zielPfad` ein Wurzelverzeichnis oder anderweitig ohne übergeordneten Pfad, liefert `GetDirectoryName` `null` und der Aufruf wirft eine wenig aussagekräftige `ArgumentNullException`.

  Empfehlung: Vor dem Aufruf prüfen (`var parent = Path.GetDirectoryName(zielPfad); if (parent is not null) Directory.CreateDirectory(parent);`) oder explizit eine aussagekräftige Exception werfen.

### AutonomAufgabenInitialisierungsService.cs (AutonomAufgabenInitialisierungsService)

- **Code Smell (Long Parameter List)** — `InitialisiereAsync(Aufgabe, string, string?, int, int?, int, PersistenzModus, bool, PermissionsJsonOption, CancellationToken)` hat 9 fachliche Parameter, obwohl bereits das passende Parameter-Objekt `AutonomAufgabeInitialisierungsAnfrage` samt kompakter Überladung existiert.

  Empfehlung: Aufrufer sollen direkt ein `AutonomAufgabeInitialisierungsAnfrage`-Objekt bauen und die `(Aufgabe, Anfrage, ct)`-Überladung nutzen; die 9-Parameter-Überladung entfernen.

- **Toter Code** — `InitialisiereAsync(Aufgabe aufgabe, string initialPrompt, CancellationToken ct = default)` hat repo-weit keinen einzigen Aufrufer (weder Produktionscode noch Tests).

  Empfehlung: Entfernen, oder falls für einen zukünftigen Aufrufer vorgesehen, das explizit dokumentieren bzw. durch einen Test absichern.

- **Kopplung/Konfiguration: hardcodierte Werte** — `MaxClones = 3` und `MaxFeatureBranches = 10` sind `const int` direkt in der Service-Klasse, während der eng verwandte `MaxConcurrentSubagents`-Wert korrekt über `AutonomAufgabenOptions` konfigurierbar ist.

  Empfehlung: `MaxClones`/`MaxFeatureBranches` als weitere Properties nach `AutonomAufgabenOptions` verschieben.

- **Fehlerbehandlung** — `File.WriteAllTextAsync` für `permissions.json` und `state.json` in `InitialisiereAsync(Aufgabe, Anfrage, ct)` sind nicht gegen `IOException`/`UnauthorizedAccessException` abgesichert, obwohl `ErstelleArbeitsverzeichnisStrukturAsync` exakt diese Exceptions für `Directory.CreateDirectory` in eine `DirectoryAccessException` übersetzt. Zusätzlich liegen auch innerhalb von `ErstelleArbeitsverzeichnisStrukturAsync` die `File.WriteAllTextAsync`-Aufrufe für `plan.md`/`progress.md`/`governance.md` außerhalb des try/catch, das nur die vorangehenden `CreateDirectory`-Aufrufe umschließt.

  Empfehlung: Alle Datei-I/O-Operationen der Methode einheitlich in denselben try/catch-Block wie die Verzeichniserstellung einbeziehen bzw. einen gemeinsamen Fehlerbehandlungs-Wrapper für Datei-I/O einführen.

### ProjektleiterAgentService.cs (ProjektleiterAgentService)

- **Struktur: God-Class-Tendenz** — Die Klasse vereint drei fachlich unterschiedliche Verantwortlichkeiten: Projektleiter-Lifecycle (`StarteAgentAsync`), Git-Provisionierung für Unteragenten (`SteuereUnteragentAsync`), Ergebnis-Integration (`IntegriereErgebnisseAsync`).

  Empfehlung: Git-Provisionierung (Branch/Klon) in einen eigenen Service oder eine Erweiterung von `GitKlonHelper` auslagern; state.json-Patch-Logik über `StateJsonHelper` statt eigener Implementierung.

- **Struktur: God-Method-Tendenz** — `SteuereUnteragentAsync` (ca. 48 Zeilen) validiert, prüft Governance-Berechtigung, legt Verzeichnis an, erstellt Branch, klont, persistiert und loggt — sechs konzeptuell getrennte Schritte in einer Methode.

  Empfehlung: In benannte Teilschritte extrahieren (z. B. `ErstelleFeatureBranchUndKlonAsync`), analog zur Zerlegung in `AutonomAufgabenInitialisierungsService`.

- **Doppelter Code** — "Zielverzeichnis existiert/ist nicht leer → überspringen, sonst Verzeichnis anlegen + `git clone` + `IsSuccess`-Prüfung" ist exakt die Logik aus `GitKlonHelper.KloneFallsNichtVorhandenAsync`, hier aber inline neu implementiert statt den vorhandenen Helper zu verwenden.

  Empfehlung: `GitKlonHelper.KloneFallsNichtVorhandenAsync(...)` aufrufen und die eigene Implementierung entfernen.

- **Doppelter Code** — `AktualisiereSubagentsInStateJsonAsync` dupliziert das Lese/Parse/Schreibe-Muster für `state.json` aus `StateJsonHelper.LeseAsync`/`SchreibeAsync`, jedoch **ohne** dessen `JsonException`-Behandlung — ein beschädigtes `state.json` propagiert hier ungefangen, nachdem `plan.md`/`progress.md` bereits geschrieben wurden (inkonsistenter Zwischenzustand).

  Empfehlung: `StateJsonHelper.LeseAsync`/`SchreibeAsync` verwenden statt eigener Implementierung.

- **Fehlerbehandlung** — `await _cliRunner.RunAsync("git", ["branch", unteragent.AgentBranch], ...)`: das Ergebnis wird nicht auf `IsSuccess` geprüft, im Gegensatz zum direkt folgenden Klon-Aufruf. Schlägt die Branch-Erstellung fehl, läuft der Code weiter und der nachfolgende `git clone --branch` scheitert mit einer irreführenden Fehlermeldung statt der eigentlichen Ursache.

  Empfehlung: Ergebnis prüfen und bei Fehlschlag mit Kontext (`ergebnis.StdErr`) eine `InvalidOperationException` werfen, analog zum Klon-Aufruf.

- **Fehlerbehandlung** — `Directory.CreateDirectory(unteragent.AgentDirectory);` ohne try/catch, im Gegensatz zu `AutonomAufgabenInitialisierungsService.ErstelleArbeitsverzeichnisStrukturAsync`, die dieselbe Art Operation in try/catch mit `DirectoryAccessException` kapselt.

  Empfehlung: Gleiche Fehlerbehandlung (`DirectoryAccessException`) anwenden, idealerweise über eine gemeinsame Hilfsmethode.

- **Namenskonvention** — Parametername `arbeitsverzeichnispPfad` (in `AktualisiereSubagentsInStateJsonAsync`) enthält einen Tippfehler (doppeltes „p").

  Empfehlung: Umbenennen zu `arbeitsverzeichnisPfad`.

### SessionManagementService.cs (SessionManagementService)

- **Doppelter Code** — `AktualisierePausedUtcInStateJsonAsync` dupliziert das identische Lese/Parse/Schreibe-Muster für `state.json` wie `ProjektleiterAgentService.AktualisiereSubagentsInStateJsonAsync`, ebenfalls ohne die `JsonException`-Behandlung von `StateJsonHelper`.

  Empfehlung: `StateJsonHelper.LeseAsync`/`SchreibeAsync` verwenden.

- **Namenskonvention** — Gleicher Tippfehler `arbeitsverzeichnispPfad` (Parametername) wie in `ProjektleiterAgentService` — deutet auf Copy-Paste zwischen beiden Stellen hin.

  Empfehlung: Umbenennen zu `arbeitsverzeichnisPfad`.

- **Fehlerbehandlung** — Kein try/catch um `JsonNode.Parse`. Ein beschädigtes `state.json` lässt `PauseAufgabeBeiBudgetLimitAsync`/`SetzeFortAsync` mit ungefangener `JsonException` abbrechen, **nachdem** die DB bereits gespeichert wurde (`SaveChangesAsync` davor) — inkonsistenter Zustand zwischen DB und state.json.

  Empfehlung: Über `StateJsonHelper.LeseAsync` lesen (behandelt `JsonException` bereits durch Warnung + `null`-Rückgabe) statt direkt `JsonNode.Parse` aufzurufen.

### StateJsonHelper.cs (StateJsonHelper)

- **Toter Code** — Die Klasse (`LeseAsync`/`SchreibeAsync`) wird im gesamten Repository nirgends aufgerufen. Sowohl `ProjektleiterAgentService` als auch `SessionManagementService` implementieren exakt diese Funktionalität stattdessen inline und dupliziert (siehe dortige Befunde).

  Empfehlung: `StateJsonHelper.LeseAsync`/`SchreibeAsync` in beiden Aufrufern einsetzen und die duplizierten privaten Methoden dort entfernen.

### UnteragentGovernanceService.cs (UnteragentGovernanceService)

- **Toter Code / Speculative Generality** — `ValidiereFehlerBedingungAsync` wird außerhalb dieser Klasse und ihrer Unit-Tests nirgends aufgerufen. Die Governance-Abbruchprüfung (Tokenlimit/Laufzeitlimit) ist implementiert, aber in keinen Ausführungs-/Überwachungs-Loop eingebunden.

  Empfehlung: Entweder in den Unteragenten-Ausführungspfad einbinden (z. B. periodischer Aufruf durch `ProjektleiterAgentService` nach jedem Unteragenten-Schritt/Heartbeat), oder Nichtverwendung explizit dokumentieren.

### AutonomAufgabeStartCoordinator.cs (AutonomAufgabeStartCoordinator)

- **Fehlerbehandlung** — `StarteAsync` schützt nur die zweite Hälfte des Ablaufs per try/catch; die erste Hälfte (Aufgabe laden, Init-Dialog-VM auflösen/anzeigen) ist ungeschützt. Kombiniert mit dem fehlenden `OnError`-Handler des aufrufenden Commands werden Exceptions dort komplett verschluckt.

  Empfehlung: try/catch auch um den ersten Teil legen bzw. gesamte Methode einheitlich behandeln.

- **Data Clump** — `StarteAsync(Guid aufgabeId, Aufgabe aufgabe, ...)`: `aufgabeId` ist redundant, da `aufgabe.Id` denselben Wert liefert.

  Empfehlung: Parameter entfernen, `aufgabe.Id` verwenden.

- **Namenskonvention** — „Coordinator"-Suffix weicht vom in `Softwareschmiede.App.Services` durchgängigen „...Service"-Suffix ab (`PluginSelectionDialogService`, `ArbeitsverzeichnisOeffnenService`).

  Empfehlung: Umbenennen zu `AutonomAufgabeStartService`.

### AutonomAufgabeStartErgebnis.cs (AutonomAufgabeStartErgebnis)

- **Namenskonvention** — Sibling-DTOs im selben Ordner heißen `IssueCreateDialogResult`, `PluginSelectionResult` (Suffix „Result"); der neue Record bricht mit „Ergebnis" das lokale Schema.

  Empfehlung: Umbenennen zu `AutonomAufgabeStartResult`.

### TaskDetailViewModel.cs (TaskDetailViewModel)

- **Fehlerbehandlung** — `AutonomAufgabeInitialisierenAsync` ist die einzige Command-Handler-Methode der Klasse ohne try/catch (alle Geschwistermethoden loggen und setzen `FehlerMeldung`). Da der Command keinen `OnError`-Handler hat, bleibt ein Fehler unsichtbar.

  Empfehlung: Gleiches try/catch-Muster wie in `CliStoppenAsync` etc. ergänzen.

### WpfDialogService.cs (WpfDialogService)

- **Doppelter Code** — Die beiden neuen Dialogmethoden wiederholen ein bereits viermal vorhandenes Boilerplate-Muster, statt es in eine generische Hilfsmethode zu extrahieren.

  Empfehlung: `Task<TResult> ShowDialogAsync<TResult>(Func<Window> dialogFactory, Func<TResult> resultSelector)` einführen.

- **Ungenutzter Parameter** — `ct` wird in beiden neuen Methoden nie verwendet.

  Empfehlung: `ct.ThrowIfCancellationRequested()` ergänzen oder Parameter entfernen.

### AutonomAufgabeDetailViewModel.cs (AutonomAufgabeDetailViewModel)

- **Fehlende Validierung** — `StarteAgentAsync` prüft (anders als `StoppeAgentAsync`/`ResumeAgentAsync`) nicht, ob `_aufgabe`/`Initialize` gesetzt ist, bevor `Konfiguration` (`null!`-initialisiert) verwendet wird → potenzielle `NullReferenceException`.

  Empfehlung: Gleichen Guard wie in `StoppeAgentAsync` ergänzen.

- **Temporäres Feld** — `_konfiguration`/`_aufgabe` sind bis zum separaten `Initialize()`-Aufruf ungültig; mehrere öffentliche Methoden würden vorher fehlschlagen.

  Empfehlung: Pflichtinjektion über Konstruktor statt DI-Transient + nachträglichem `Initialize`, oder expliziten Guard ergänzen.

### AutonomAufgabeInitialisierungsDialogViewModel.cs (AutonomAufgabeInitialisierungsDialogViewModel)

- **Long Parameter List / ungenutztes Parameter-Objekt** — `BestaetigenAsync` ruft die 9-Parameter-Überladung von `InitialisiereAsync` auf, obwohl bereits ein passendes Parameter-Objekt `AutonomAufgabeInitialisierungsAnfrage` samt kompakter Überladung existiert.

  Empfehlung: `AutonomAufgabeInitialisierungsAnfrage` direkt bauen und die `(aufgabe, anfrage, ct)`-Überladung nutzen.

- **Primitive Obsession** — `SelectedPersistenceMode` ist `string` mit manuellem `Enum.TryParse`, während die strukturell gleiche `SelectedPermissionsOption` korrekt als Enum modelliert ist.

  Empfehlung: `SelectedPersistenceMode` auf `PersistenzModus` umstellen, XAML-Binding analog zur Permissions-ComboBox per `ObjectDataProvider`.

- **Fehlende Cancellation** — `BestaetigenAsync()` hat kein `CancellationToken`; das Command verwirft es explizit (`_ => BestaetigenAsync()`), anders als praktisch alle übrigen Commands im Projekt.

  Empfehlung: Token durchreichen (`BestaetigenAsync(CancellationToken ct = default)`).

### SoftwareschmiededDbContext.cs (SoftwareschmiededDbContext)

- **Fehlerbehandlung / fehlende Constraints** — In den neuen Entity-Konfigurationen für `UnteragentSpezifikation` und `SkillDefinition` fehlt `HasMaxLength()` bei den kurzen Identifier-/Namensfeldern `AgentId`, `TaskId`, `AgentScope`, `SkillName`, `SkillVersion` — nur `IsRequired()` ist gesetzt. Alle vergleichbaren kurzen Identifier-/Namensfelder im übrigen Kontext sind durchgängig längenbegrenzt (z. B. `Provider` 200, `AlertType` 100, `SourceKey` 700).

  Empfehlung: Für `AgentId`, `TaskId`, `AgentScope`, `SkillName`, `SkillVersion` jeweils ein sinnvolles `HasMaxLength(...)` ergänzen, sofern die Werte nicht bewusst unbegrenzt bleiben sollen.

- **Struktur/Verantwortlichkeiten** — Im `Aufgabe`-Konfigurationsblock folgt das sonst übliche Muster "erst alle Property-Konfigurationen, dann alle Relationship-Konfigurationen" nicht durchgängig: `e.Property(a => a.SessionPauseUtc).HasConversion(...)` und `e.HasOne(a => a.AutonomKonfiguration)...` stehen nach den bereits abgeschlossenen `HasMany`-Blöcken für `PullRequests`, `Protokolleintraege`, `DiffResults`, `Todos`.

  Empfehlung: `e.Property(a => a.SessionPauseUtc)...` zu den übrigen Property-Konfigurationen im oberen Block verschieben, damit Property- und Relationship-Konfiguration konsistent gruppiert bleiben.

### appsettings.json

- **Namenskonventionen / Inkonsistenz** — Der neue Schlüssel `AutonomAufgaben.MaxConcurrentSubagents` verwendet den englischen Begriff "Subagents" für dasselbe Domänenkonzept, das im selben Feature mit der neuen Entity `UnteragentSpezifikation` und der Spalte `Aufgabe.AktiveUnteragenten` konsequent deutsch als "Unteragent" benannt ist.

  Empfehlung: Konfigurationsschlüssel an die deutsche Terminologie angleichen, z. B. `MaxConcurrentUnteragenten`.

- **Namenskonventionen / Inkonsistenz** — `AutonomAufgaben.SkillAutoGenerationEnabled` (Groß-G) bezeichnet dasselbe Konzept wie `AutonomAufgabeKonfiguration.SkillAutogeneration` (klein-g) im Domain-Modell — unterschiedliche Schreibweise für denselben Begriff.

  Empfehlung: Schreibweise vereinheitlichen, z. B. `SkillAutogenerationEnabled`, damit Config-Schlüssel und Domain-Property-Name konsistent sind.

### AutonomAufgabeDetailViewModelTests.cs (AutonomAufgabeDetailViewModelTests)

- **Fehlende Testabdeckung** — `LaedeProgressAsync` und `LaedeGovernanceAsync` sind überhaupt nicht getestet, obwohl sie dieselbe Datei-Existenz-Logik wie `LaedePlanAsync` kapseln.

  Empfehlung: Je einen Test für `LaedeProgressAsync` und `LaedeGovernanceAsync` ergänzen (analog zu `LaedePlanAsync_LaedesDateiausArbeitsverzeichnis`).

- **Fehlende Testabdeckung (Fehlerpfad)** — `StoppeAgentAsync` und `ResumeAgentAsync` sind komplett ungetestet, inkl. des Frühausstiegs bei fehlender Aufgabe.

  Empfehlung: Tests für `StoppeAgentAsync`/`ResumeAgentAsync` ergänzen (Delegation an `SessionManagementService` sowie Fall "keine Aufgabe initialisiert").

- **Fehlende Testabdeckung (Fehlerpfad)** — `FuehreAgentOperationAsync` fängt Exceptions ab und setzt `ErrorMessage`; dieser Pfad ist für keinen der drei Commands getestet.

  Empfehlung: Mind. einen Test ergänzen, der eine Exception im zugrunde liegenden Service simuliert und prüft, dass `ErrorMessage` gesetzt und `IsBusy` zurückgesetzt wird.

- **Fehlende Testabdeckung (Edge Case)** — Der Fall "plan.md existiert nicht" (`PlanContent` wird `string.Empty`) ist nicht getestet.

  Empfehlung: Test ergänzen, der `LaedePlanAsync` ohne vorhandene `plan.md` aufruft und `PlanContent == string.Empty` erwartet.

- **Doppelter Code** — Konstruktor-Setup (DbContext, Projekt-/Aufgabe-Anlage mit identischen Beispielwerten) ist nahezu identisch zu `ProjektleiterAgentServiceTests.ErstelleAutonomeAufgabeAsync()` und `SessionManagementServiceTests.ErstelleAutonomeAufgabeAsync()`.

  Empfehlung: Gemeinsame Test-Factory-Methode extrahieren (z. B. in `Softwareschmiede.Tests/Helpers`), analog zum Vorbild `TaskDetailViewModelTestFactory.cs`.

### AutonomAufgabeInitialisierungsDialogViewModelTests.cs (AutonomAufgabeInitialisierungsDialogViewModelTests)

- **Fehlende Testabdeckung (Validierung)** — `ValidiereEingaben()` hat drei Validierungszweige; nur der TokenBudget-Zweig ist getestet, die beiden anderen (InitialPrompt, RuntimeLimit) sind ungetestet.

  Empfehlung: Zwei weitere Tests ergänzen: `BestaetigenAsync_FailsOnInvalidInitialPrompt` und `BestaetigenAsync_FailsOnInvalidRuntimeLimit`.

- **Fehlende Testabdeckung (Fehlerpfad)** — Der `catch (Exception ex)`-Block in `BestaetigenAsync` (setzt `ErrorMessage`, wenn der Initialisierungsservice wirft, z. B. bei Git-Klon-Fehler) ist nicht getestet.

  Empfehlung: Test ergänzen, der den Service fehlschlagen lässt und prüft, dass `ErrorMessage` gesetzt, `CloseRequested` nicht ausgelöst und `IsSubmitting` wieder `false` ist.

- **Doppelter Code** — Konstruktor-Setup (`ICliRunner`-Mock mit `git clone`-Callback, Service-Konstruktion, Projekt-/Aufgabe-Anlage) ist nahezu identisch zum Setup in `AutonomAufgabenInitialisierungsServiceTests.cs`.

  Empfehlung: In eine gemeinsame Test-Helper-Methode extrahieren.

### AutonomAufgabenInitialisierungsServiceTests.cs (AutonomAufgabenInitialisierungsServiceTests)

- **Fehlende Testabdeckung (Fehlerpfad, Git-Klon-Fehler)** — Weder der Fall `aufgabe.LokalerKlonPfad == null` (erwartet `InvalidOperationException`) noch ein fehlgeschlagener `git clone` (nicht-erfolgreicher `CliResult`) sind getestet; der `ICliRunner`-Mock liefert im gesamten Testfile ausschließlich erfolgreiche Ergebnisse.

  Empfehlung: Zwei Tests ergänzen für beide Fehlerpfade.

- **Fehlende Testabdeckung (Validierung)** — `ValidiereAnfrage` hat vier Validierungszweige; nur `TokenBudget` ist abgedeckt.

  Empfehlung: Drei weitere Tests ergänzen für ungültigen `ProjektBranchName`, zu kurzen `InitialPrompt` und ungültiges `LaufzeitLimitMinuten`.

- **Fehlende Testabdeckung** — Die beiden Convenience-Overloads von `InitialisiereAsync` werden auf Service-Ebene nicht direkt getestet.

  Empfehlung: Mindestens einen Test für eine der beiden Overloads ergänzen.

### ProjektleiterAgentServiceTests.cs (ProjektleiterAgentServiceTests)

- **Fehlende Testabdeckung (Fehlerpfad, Git-Klon-Fehler)** — `SteuereUnteragentAsync` wirft `InvalidOperationException` bei fehlgeschlagenem Klon; dieser Pfad ist nicht getestet.

  Empfehlung: Test ergänzen, der für `git clone` einen fehlgeschlagenen `CliResult` mockt und `InvalidOperationException` erwartet.

- **Fehlende Testabdeckung (Governance-Verweigerung)** — `SteuereUnteragentAsync` wirft `InvalidOperationException`, wenn `VerifiziereBerechtigung` `false` liefert. Diese zentrale Sicherheitsprüfung ist komplett ungetestet.

  Empfehlung: Test ergänzen, der einen Unteragenten mit `AgentDirectory` außerhalb des erlaubten Bereichs übergibt und `InvalidOperationException` erwartet.

- **Fehlende Testabdeckung (Validierung)** — `ValidiereUnteragent` hat vier Validierungszweige; keiner ist getestet.

  Empfehlung: Mind. einen parametrisierten oder mehrere Tests ergänzen, die je einen ungültigen Wert setzen und `ArgumentException` erwarten.

- **Fehlende Testabdeckung (Fehlerpfad)** — `StarteAgentAsync`/`SteuereUnteragentAsync`/`IntegriereErgebnisseAsync` werfen `InvalidOperationException` bei nicht existierenden referenzierten Entities; keiner dieser Pfade ist getestet.

  Empfehlung: Je einen Test pro Methode ergänzen, der eine nicht-persistierte ID übergibt.

- **Fehlende Testabdeckung (Edge Case)** — `IntegriereErgebnisseAsync` hat einen Fallback-Text, wenn `task_report.md` fehlt; dieser Zweig ist nicht getestet.

  Empfehlung: Test ergänzen ohne vorhandene `task_report.md`, der den Fallback-Text in `progress.md` prüft.

### SessionManagementServiceTests.cs (SessionManagementServiceTests)

- **Fehlende Testabdeckung (Fehlerpfad)** — Alle drei öffentlichen Methoden werfen `InvalidOperationException` bei nicht existierender `Aufgabe`; keiner dieser Pfade ist getestet.

  Empfehlung: Je einen Test ergänzen mit nicht-persistierter ID.

- **Fehlende Testabdeckung (Edge Case)** — `PruefeAusfuehrungAsync` hat zwei weitere frühe `return true`-Zweige (`SessionPauseUtc is not null`, `LastHeartbeatUtc is null`), die nicht getestet sind.

  Empfehlung: Zwei Tests ergänzen für beide Zweige.

- **Doppelter Code** — `ErstelleAutonomeAufgabeAsync()` dupliziert nahezu identisch die gleichnamige Methode in `ProjektleiterAgentServiceTests.cs`.

  Empfehlung: In eine gemeinsame Test-Factory extrahieren.

### UnteragentGovernanceServiceTests.cs (UnteragentGovernanceServiceTests)

- **Fehlende Testabdeckung (Edge Case)** — Der Normalfall "task_state.json existiert nicht" (früher `return` in `ValidiereFehlerBedingungAsync`) ist nicht getestet.

  Empfehlung: Test ergänzen, der `ValidiereFehlerBedingungAsync` ohne vorhandene `task_state.json` aufruft und `NotThrowAsync()` erwartet.

- **Fehlende Testabdeckung (Validierung)** — `VerifiziereBerechtigung` wirft `ArgumentNullException`/`ArgumentException` bei `unteragent == null` bzw. leerem `zielPfad`; nicht getestet.

  Empfehlung: Zwei Tests ergänzen für die beiden Guard-Clauses.

### TaskDetailViewModelTests.cs, TaskDetailViewModelTestsBase.cs, TaskDetailViewModelTests_PluginAktivierung.cs, TaskDetailViewModelTests_ZeitgesteuerterPrompt.cs, TaskDetailViewModelTestFactory.cs

- **Doppelter Code** — Der Block zur Erzeugung des `AutonomAufgabeStartCoordinator` (`new Mock<IServiceProvider>()` + Konstruktoraufruf) ist identisch in allen fünf Dateien wiederholt, obwohl `TaskDetailViewModelTestFactory` als gemeinsame Factory für genau solche Fälle existiert.

  Empfehlung: Eine `TaskDetailViewModelTestFactory.CreateAutonomAufgabeStartCoordinator(...)`-Hilfsmethode ergänzen und in allen vier Testklassen darüber referenzieren statt den Konstruktoraufruf zu duplizieren.

## Geprüfte Dateien

- `src/Softwareschmiede.App/App.xaml.cs`
- `src/Softwareschmiede.App/Services/IDialogService.cs`
- `src/Softwareschmiede.App/Services/WpfDialogService.cs`
- `src/Softwareschmiede.App/Services/AutonomAufgabeStartCoordinator.cs`
- `src/Softwareschmiede.App/Services/AutonomAufgabeStartErgebnis.cs`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/AutonomAufgabeDetailViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/AutonomAufgabeInitialisierungsDialogViewModel.cs`
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml`
- `src/Softwareschmiede.App/Views/AutonomAufgabeDetailDialog.xaml`
- `src/Softwareschmiede.App/Views/AutonomAufgabeDetailDialog.xaml.cs`
- `src/Softwareschmiede.App/Views/AutonomAufgabeDetailView.xaml`
- `src/Softwareschmiede.App/Views/AutonomAufgabeDetailView.xaml.cs`
- `src/Softwareschmiede.App/Views/AutonomAufgabeInitialisierungsDialog.xaml`
- `src/Softwareschmiede.App/Views/AutonomAufgabeInitialisierungsDialog.xaml.cs`
- `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`
- `src/Softwareschmiede/Domain/Entities/AutonomAufgabeKonfiguration.cs`
- `src/Softwareschmiede/Domain/Entities/SkillDefinition.cs`
- `src/Softwareschmiede/Domain/Entities/UnteragentSpezifikation.cs`
- `src/Softwareschmiede/Domain/Enums/AufgabeAusfuehrungsStatus.cs`
- `src/Softwareschmiede/Domain/Enums/PermissionsJsonOption.cs`
- `src/Softwareschmiede/Domain/Enums/PersistenzModus.cs`
- `src/Softwareschmiede/Domain/Enums/SkillStatus.cs`
- `src/Softwareschmiede/Domain/Enums/UnteragentAktion.cs`
- `src/Softwareschmiede/Domain/Enums/UnteragentStatus.cs`
- `src/Softwareschmiede/Domain/Exceptions/DirectoryAccessException.cs`
- `src/Softwareschmiede/Domain/Exceptions/UnteragentAbbruchException.cs`
- `src/Softwareschmiede/Domain/ValueObjects/AutonomAufgabeInitialisierungsAnfrage.cs`
- `src/Softwareschmiede/Application/Services/AufgabeService.cs`
- `src/Softwareschmiede/Application/Services/AutonomAufgabenInitialisierungsService.cs`
- `src/Softwareschmiede/Application/Services/AutonomAufgabenOptions.cs`
- `src/Softwareschmiede/Application/Services/GitKlonHelper.cs`
- `src/Softwareschmiede/Application/Services/ProjektleiterAgentService.cs`
- `src/Softwareschmiede/Application/Services/SessionManagementService.cs`
- `src/Softwareschmiede/Application/Services/StateJsonHelper.cs`
- `src/Softwareschmiede/Application/Services/UnteragentGovernanceService.cs`
- `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs`
- `src/Softwareschmiede/Migrations/SoftwareschmiededDbContextModelSnapshot.cs`
- `src/Softwareschmiede/Migrations/20260820175118_AddAutonomAufgabeModels.cs`
- `src/Softwareschmiede/Migrations/20260820175118_AddAutonomAufgabeModels.Designer.cs`
- `src/Softwareschmiede/appsettings.json`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTestsBase.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_PluginAktivierung.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_ZeitgesteuerterPrompt.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/AutonomAufgabeDetailViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/AutonomAufgabeInitialisierungsDialogViewModelTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/AutonomAufgabenInitialisierungsServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/ProjektleiterAgentServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/SessionManagementServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/UnteragentGovernanceServiceTests.cs`
- `src/Softwareschmiede.Tests/E2E/MainTest.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_AutonomAufgabenInitialisierung.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_AutonomAufgabenAgentExecution.cs`
- `src/Softwareschmiede.Tests/Helpers/TaskDetailViewModelTestFactory.cs`

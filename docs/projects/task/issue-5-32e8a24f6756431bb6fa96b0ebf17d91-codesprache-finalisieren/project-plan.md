# Projektplan – Codesprache finalisieren

## Übersicht

**Projekttitel:** Codesprache finalisieren
**Gesamtziel:** Sämtliche Bezeichner im C#-Code (Typen, Member, Parameter, lokale
Variablen, Dateinamen) werden auf Englisch vereinheitlicht; XML-Dokumentationskommentare
und Dokumentationsdateien werden/verbleiben auf Deutsch. Die Umbenennung erfolgt
schrittweise je Schicht, mit durchgängig kompilierbarem Zwischenstand und Schutz
persistierter Daten.
**Betroffene Bereiche:** `src/Softwareschmiede` (Domain, Application, Infrastructure),
`src/Softwareschmiede.App` (inkl. XAML), `src/Softwareschmiede.Plugin.Contracts`,
`plugins/`, beide Testprojekte, `docs/`, `.github/`-Dokumente.

## Grobe Vorgehensentscheidungen

1. **Schichtweise, repo-weit konsistente Schritte statt Feature-Slices.**
   Domänentypen werden von allen Schichten referenziert; ein Schritt benennt einen
   Typbereich um und aktualisiert *alle* Referenzen repo-weit — jeder Merge bleibt
   kompilier- und testbar. Begründung: Zwischenstände ohne Compile-Fehler sind bei
   layerübergreifenden Bezeichnern nur so erreichbar.

2. **Persistente Daten bleiben bitkompatibel (Storage-Pinning statt Datenmigration).**
   DB-Tabellen-/Spaltennamen, persistierte Enum-/Status-Strings, Settings-Werte und
   Wire-Feldnamen sind *gespeicherte Daten/Protokollverträge*, kein Programmcode.
   Nach Umbenennung der CLR-Bezeichner werden die bisherigen Speicher-/Wire-Namen
   explizit gepinnt:
   - `ToTable`/`HasColumnName` bzw. beibehaltene DbSet-Property→ToTable-Mapping für
     EF-Entitäten (kein Schema-Change, kein Datenverlust-Risiko); ebenso
     `HasDatabaseName`-Indexnamen und Raw-SQL-Filter wie `HasFilter("[Rolle] = 'ReviewSource'")`
     pinnen (Rename-Skripte dürfen solche Literale nicht anfassen),
   - explizite `ValueConverter<enum,string>` (Legacy-String-Mapping) für alle
     `HasConversion<string>`-Enums,
   - Mapping/Pinning beim Laden/Speichern für alle ToString-/Literal-persistierten
     Werte: `AufgabenListenModus`-Settings-Werte (`"Datum"`/`"Repository"`/`"Ki"`),
     `updates.pfad`-Werte (`"Produktivversionen"`/`"Vorabversionen"`),
     die vollständige `ScmIncomingEvent.Status`-Literalmenge (~9 Werte:
     `"Aufgenommen"`, `"Zugestellt"`, `"InBearbeitung"`, `"Verzögert"`,
     `"VerarbeitungUngeklärt"`, `"ZustellungVorbereitet"`, `"ZustellungUngewiss"`,
     `"Verarbeitet"`, `"Wartend"`), `Zweck`-Literal `"IssueAufbereitung"`,
     Plugin-Präfix-Literale wie `"Softwareschmiede.KiSimulator"` sowie deutsche
     Settings-**Keys** (`"aktiveaufgaben.listenmodus*"`, `"updates.pfad"`,
     `"ki.plugin.default"`, `"autoshutdown.toleranz.minuten"`,
     `"scm.issueaufbereitung.*"` — die Key-Literale bleiben, nur die
     C#-Konstantennamen werden englisch),
   - `[ConfigurationKeyName]`-Pinning für Konfigurationsbindungen, deren
     Property-Name zugleich externer Konfigurationsschlüssel ist
     (`Terminal:AufzeichnungByteBudget` in `TerminalSessionOptions`),
   - `[JsonPropertyName]` für das SCM-Austauschprotokoll — gepinnt wird der
     tatsächlich emittierte Wire-Name (`aufgabeId`, `konto`, …); für numerisch
     serialisierte Enums (`ScmKontoModus`, `ScmResponseKind`) reicht das
     Property-Pinning, Member-Attribute wären wirkungslos,
   - `DiffCache.CachedData` (TTL-Cache, `DiffResult`-Payload mit CLR-Namen):
     bewusster, begründeter Verzicht auf Pinning — veraltete Cache-Zeilen werden
     tolerant als Miss behandelt und neu befüllt (Deserialisierungsweg prüfen/
     härten), weil die Daten regenerierbar sind und kein Informationsverlust droht.
   Begründung: Issue betrifft „Programmcode", nicht gespeicherte Daten; Bestands-
   installationen aktualisieren sich ohne jede Datenmigration risikofrei.
   Konsequenz: es entsteht eine neue EF-Migration, die nach dem Pinning im Idealfall
   leer ist — sie wird dennoch per `dotnet ef` erzeugt/geprüft (Skill-Regel:
   jede Modelländerung braucht Migration oder Begründung; Begründung = stabiles Schema).
   Der `ModelSnapshot` wird von `dotnet ef` auf das neue Modell aktualisiert
   (Tooling-Output, legitime Ausnahme unter `Migrations/`).

3. **Übersetzungsglossar vorab festlegen.** Zentrale Begriffe:
   `Aufgabe`→`WorkItem` (vermeidet Kollision mit `System.Threading.Tasks.Task` und
   `Todo`-Entität), `Ki*`→`Ai*`, `Protokoll*`→`Log*`, `Einstellung(en)`→`Setting(s)`,
   `Benachrichtigung*`→`Notification*`, `Verzeichnis`→`Directory`/`Folder`,
   `Arbeitsverzeichnis`→`WorkingDirectory`, `Vorlage`→`Template`, `Ergebnis`→`Result`,
   `Ausführung/Ausfuehrung`→`Execution`, `Überwachung/Ueberwachung`→`Monitoring`,
   `Zustand`→`State`, `Modus`→`Mode`, `Kanal`→`Channel`, `Entscheidung`→`Decision`,
   `Referenz`→`Reference`, `Konfiguration`→`Configuration`, `Aufzeichnung`→`Recording`,
   `Bereinigung`→`Cleanup`, `Auswahl`→`Selection`, `Ansicht`→`View`, `Anzeige`→`Display`,
   `Fehlgeschlagen`→`Failed`, `Uebersprungen`→`Skipped`, `Bestanden`→`Passed`,
   `Verwerfen`→`Discard`, `Prüfmodus/Pruefmodus`→`CheckMode`, `Aufbereitung`→`Preparation`.
   `SoftwareschmiededDbContext`→`SoftwareschmiedeDbContext` (Tippfehler korrigiert).
   Enum-Member erhalten fachlich passende englische Namen (z. B. `Neu`→`New`,
   `Gestartet`→`Started`, `Wartend`→`Waiting`, `Beendet`→`Completed`, `Archiviert`→`Archived`,
   `Laeuft`→`Running`, `WartetAufEingabe`→`WaitingForInput`, `Deaktiviert`→`Disabled`,
   `Gesendet`→`Sent`, `Unterdrueckt`→`Suppressed`, `Zurueckgestellt`→`Deferred`,
   `Datum`→`Date`, `Benutzerkonto`→`UserAccount`, `SeparatesKonto`→`SeparateAccount`).

4. **Mechanisch-skriptgestützte Umbenennung + Compiler-getriebene Nacharbeit.**
   Pro Schritt wird eine explizite Umbenennungsliste (alt→neu, Wortgrenzen,
   PascalCase/camelCase) per Skript angewendet, Dateien umbenannt und der Rest über
   `dotnet build`-Fehlerlisten geschlossen. Deutsche Locals/Parameter werden mit
   demselben Verfahren je Bereich umgesetzt. Begründung: ~1.500+ deutsche Bezeichner
   sind nur so zuverlässig und nachprüfbar zu schließen.

5. **Historisches bleibt fachlich unverändert, referenztechnisch minimal:**
   `src/Softwareschmiede/Migrations/**` ist eingefrorene EF-Historie —
   Migrationsklassennamen, `[Migration]`-IDs (=`__EFMigrationsHistory`-Schlüssel)
   und Up/Down-Rümpfe werden niemals geändert. Zulässig und erforderlich ist
   ausschließlich die mechanische Referenzkorrektur des Kontexttyps
   (`[DbContext(typeof(SoftwareschmiededDbContext))]`→`SoftwareschmiedeDbContext`
   samt using) in den ~40 `*.Designer.cs`, weil sonst der Build bricht, sowie das
   `dotnet ef`-generierte `ModelSnapshot`-Update. Generierte `CHANGELOG.md`
   (semantic-release) wird nicht angefasst. Fremd-Artefakte unter
   `.github/upgrades/**` und `.github/skills/razor-viewmodel/` werden nicht
   übersetzt (kein Produktinhalt).

6. **Lebenszyklus pragmatisch ausgeführt:** Pro Schritt eigener Branch
   `{basis}-schritt-{n}-{slug}`, Implementierung durch den Orchestrierer (kein
   Codex-Umfeld; Devin-Subagenten übernehmen unabhängige Reviews/Abnahmen),
   Build + stabile Testlane (`Category!=OsInterface`, `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`),
   unabhängige fachliche Abnahme durch Prüf-Subagent, dann `--no-ff`-Merge.
   Abweichung zur Codex-Delegationsform ist dokumentiert.

## Entwicklungsschritte

### Schritt 1: Domänenmodell und Persistenz-Kompatibilität auf Englisch

Alle Typen unter `src/Softwareschmiede/Domain/**` (Entities, Enums samt Membern,
Interfaces, ValueObjects, Exceptions, PluginImpl-/Terminal-Domänentypen) sowie die
zugehörige EF-Konfiguration werden ins Englische überführt; sämtliche Referenzen
repo-weit (inkl. Tests, XAML-Bindings auf Entitäts-Properties wie `Aufgabe.Titel`,
`Aufgabe.AnforderungsBeschreibung`) werden aktualisiert. Storage-Pinning gemäß
Vorgehensentscheidung 2 wird vollständig eingerichtet (ToTable/HasColumnName,
Enum-Legacy-Converter, Settings-Mapping, ScmIncomingEvent.Status-Konstanten).
`docs/help/**`-Verweise auf umbenannte Domänentypen werden mitgezogen.
`SoftwareschmiededDbContext`→`SoftwareschmiedeDbContext`.

*Akzeptanzkriterien:* Build grün mit TreatWarningsAsErrors-Standardsauberkeit;
`dotnet ef migrations add` produziert keine Schema-Operationen (bzw. genau eine
geprüfte, datensichere Migration, falls unvermeidbar); stabile Testlane grün;
kein deutscher Bezeichner unter `Domain/**` (Ausnahme: dokumentierte
Legacy-Konstanten für persistierte Strings); alle Enum-Legacy-Mappings sind
durch Unit-Tests belegt.

*Abhängigkeiten:* Keine.
*Betroffene Bereiche:* `Domain/**`, `Infrastructure/Data`, `Migrations/ModelSnapshot`,
Referenzen in allen Projekten, `docs/help/**` (soweit Domänenverweise).

### Schritt 2: Application- und Infrastructure-Schicht auf Englisch

Alle deutschen Typen/Member unter `Application/**` und `Infrastructure/**`
(Services, DTOs/Records, Optionen, SCM-Protokolltypen, Terminal-Infrastruktur,
Updates-Bereich) werden umbenannt; Referenzen repo-weit aktualisiert. Das
SCM-Austauschprotokoll erhält `[JsonPropertyName]`-Pinning für alle bisherigen
Wire-Feldnamen (`aufgabeId`, `konto`, …); die deutschen Protocol-Texte an externe
Agenten bleiben deutsch (Agenten-Prosa), aber die sie tragenden Bezeichner werden
englisch. Persistierte Literale (`"IssueAufbereitung"`-Zweck, Status-Strings)
bleiben als dokumentierte Konstanten deutsch.

*Akzeptanzkriterien:* Build grün; Austauschprotokoll-Tests belegen unveränderte
Wire-Felder (Serialization-Roundtrip-Test `aufgabeId`); stabile Testlane grün;
kein deutscher Bezeichner unter `Application/**` und `Infrastructure/**`.

*Abhängigkeiten:* Schritt 1.
*Betroffene Bereiche:* `Application/**`, `Infrastructure/**`, Referenzen repo-weit.

### Schritt 3: Plugin-Verträge und Plugins auf Englisch

`src/Softwareschmiede.Plugin.Contracts/**` (`ScmKontoModus` + Member, `TestStatus`
+ Member, `PullRequestReferenzRolle`, `ScmEventRepository.Konto`, übrige deutsche
Member) sowie deutsche Bezeichner unter `plugins/**` werden umbenannt, inklusive
`JsonPropertyName`-Pinning für serialisierte/persistierte Stellen
(`ObservationJson`/`RequestJson`-Felder und Enum-Strings).

*Akzeptanzkriterien:* Build grün; stabile Testlane grün; kein deutscher Bezeichner
in Plugin.Contracts und `plugins/**`; Persistenz-Pinning der Vertrags-DTOs durch
Tests belegt.

*Abhängigkeiten:* Schritt 1 (Domain-Typen werden referenziert).
*Betroffene Bereiche:* `Softwareschmiede.Plugin.Contracts/**`, `plugins/**`, Referenzen.

### Schritt 4: App-Schicht inklusive XAML auf Englisch

Alle deutschen Typen/Member in `src/Softwareschmiede.App/**` (ViewModels,
App-Services, Option-Records, `*Texte`-Klassen, Converter, Behaviors, Dialoge
`x:Class`+Code-Behind, Testing-Helfer) werden umbenannt; zugehörige XAML-
Referenzen (`x:Class`, `x:Name`, `{Binding}`-Pfade, `DataType`, Converter-Keys)
und deutsche `AutomationId`/`AutomationProperties.Name` werden im selben Schritt
mitangepasst — zusammen mit den E2E-Selektoren in `src/Softwareschmiede.Tests/E2E/**`,
die dieselben IDs referenzieren (gleiche Commit-Einheit, sonst bricht FlaUI).
Ebenso werden zur Laufzeit geparste Enum-Namen synchronisiert: XAML-
`ConverterParameter`-Werte (`Datum`/`Repository`/`Ki`/`Alle`/`Aktiv`/`Archiviert`,
per `Enum.Parse` in `AppConverters.cs` ausgewertet) müssen gemeinsam mit den
umbenannten Enum-Membern gezogen werden, sonst bricht die Bindung lautlos.
Gleiches gilt für deutsche `x:Key`-Ressourcenschlüssel (z. B.
`AufgabenKachelTemplate`, `StatusNeuColor`/`Status*Color` in `Themes/*.xaml`,
`FileExplorerBaumItemContainerStyle`): sie werden über `{StaticResource}`/
`{DynamicResource}` referenziert und müssen in Definition und allen
Verweisstellen gemeinsam umbenannt werden.

*Akzeptanzkriterien:* Build grün; stabile Testlane grün; kein deutscher Bezeichner
unter `src/Softwareschmiede.App/**` (ausgenommen deutsch verbleibende UI-Texte und
persistierte Settings-Werte); XAML-Bindings und AutomationIds sind konsistent zum
ViewModel-Stand; E2E-Selektoren verweisen auf die neuen IDs.

*Abhängigkeiten:* Schritte 1–3.
*Betroffene Bereiche:* `src/Softwareschmiede.App/**`, `*.xaml`, E2E-Selektorkonstanten.

### Schritt 5: Testprojekte und Rest-Sweep auf Englisch

Deutsche Testklassen-/Helfer-/Fixture-Namen in `src/Softwareschmiede.Tests/**` und
`src/Softwareschmiede.IntegrationTests/**` werden umbenannt; anschließend Sweep über
alle Projekte für verbliebene deutsche Member, Parameter und lokale Variablen
(automatisierter Suchlauf über das Glossar + Resttokens, Ziel: 0 Treffer deutscher
Bezeichner außerhalb dokumentierter Ausnahmen). Deutsche Testnamen in Methoden
(`*_Tests_*`) werden ins Englische überführt, ohne die Testabdeckung zu ändern.

*Akzeptanzkriterien:* Build grün; stabile Testlane grün; Suchlauf belegt keine
deutschen Bezeichner mehr (außer dokumentierte Ausnahmen: Migrations-Historie,
Legacy-String-Konstanten, UI-Texte); Test-Namensänderungen sind reine Umbenennungen.

*Abhängigkeiten:* Schritte 1–4.
*Betroffene Bereiche:* beide Testprojekte, Restbestände in allen Projekten.

### Schritt 6: Kommentar- und Dokumentations-Sprache finalisieren

Englische `///`-XML-Kommentare (~30 Dateien, ~130–150 Blöcke, Liste liegt in
`inventory.md`-Quelle vor) werden ins Deutsche übersetzt; englische `//`-Kommentare
im SCM-Exchange-Cluster werden aus Konsistenzgründen ebenfalls übersetzt.
Englische Dokumentationsanteile werden ins Deutsche überführt:
`docs/RELEASE_NOTES.md` (ältere englische Abschnitte), `CONTRIBUTING.md`
(englische Überschriften), `.github/ISSUE_TEMPLATE/*.md` (3 Dateien).
Außerdem werden veraltete Bezeichner-Verweise in übrigen Dokumenten synchronisiert:
`docs/help/**` (z. B. `aufgabeId` in `docs/help/**/api.md`, `Terminal:AufzeichnungByteBudget`),
`README.md` (~50 Verweise wie `KiAusfuehrungsService`, `CliReplayAufzeichnungStore`,
`UpdatePfadKanalResolver`), `CLAUDE.md` (Regeln nennen u. a. `KiAusfuehrungsService`,
`PseudoConsoleSession.Buffer`, `E2E_ConPty*.cs`) und `.github/copilot-instructions.md`,
soweit dort umbenannte Bezeichner referenziert werden.

*Nicht-Ziele dieses Schritts:* `CHANGELOG.md` (generiert), `.github/upgrades/**`,
`.github/skills/razor-viewmodel/SKILL.md` (Fremd-Artefakte), `site/**` (bereits deutsch).

*Akzeptanzkriterien:* Kein englischer `///`-Block mehr (Schnellscan);
genannte Dokumente deutsch; Hilfeseiten-Verweise konsistent zu den neuen Bezeichnern;
Build und stabile Testlane grün.

*Abhängigkeiten:* Schritte 1–5 (Bezeichner müssen final stehen).
*Betroffene Bereiche:* `src/**/*.cs` (Kommentare), `docs/**`, `CONTRIBUTING.md`,
`.github/ISSUE_TEMPLATE/**`.

## Übersichtstabelle

| # | Titel | Abhängigkeiten | Betroffene Bereiche |
|---|-------|----------------|---------------------|
| 1 | Domänenmodell & Persistenz-Kompatibilität | Keine | `Domain/**`, `Infrastructure/Data`, ModelSnapshot, Referenzen repo-weit, `docs/help` (Domänenverweise) |
| 2 | Application- & Infrastructure-Schicht | 1 | `Application/**`, `Infrastructure/**`, SCM-Protokoll-Pinning |
| 3 | Plugin-Verträge & Plugins | 1 | `Plugin.Contracts/**`, `plugins/**` |
| 4 | App-Schicht inkl. XAML | 1–3 | `Softwareschmiede.App/**`, `*.xaml`, E2E-Selektoren |
| 5 | Testprojekte & Rest-Sweep | 1–4 | `*.Tests/**`, `*.IntegrationTests/**`, Restbestände |
| 6 | Kommentar- & Doku-Sprache | 1–5 | `///`-Kommentare, `docs/**`, `CONTRIBUTING.md`, Issue-Templates |

## Offene Punkte

1. **Zielname für `Aufgabe`/Verbundbegriffe:** Vorschlag `WorkItem` (vermeidet
   Kollision mit `System.Threading.Tasks.Task` und bestehender `Todo`-Entität).
   *Behandelt als dokumentierte Annahme — fachlich begründet; bei Widerspruch im
   Issue-Kommentar nachjustierbar.*
2. **Deutsch verbleibende String-Literale** (persistierte Status-/Zweck-Werte,
   Settings-Inhalte, Wire-Felder): Vorschlag: als klar markierte Kompatibilitäts-
   Konstanten stehen lassen — keine Bezeichner im Sinne der Anforderung.
   *Annahme dokumentiert.*
3. **Historische Migrationen & generierte Dateien** bleiben unverändert
   (Upgrade-Sicherheit; semantic-release regeneriert `CHANGELOG.md`). *Annahme.*
4. **Fremd-Artefakte** `.github/upgrades/**`, `.github/skills/razor-viewmodel/`:
   Vorschlag unverändert lassen (keine Produktdokumentation, fremder Projektkontext).
   *Annahme.*
5. **`Todo`-Entität** ist bereits englisch und bleibt fachlich von `WorkItem`
   (ehem. `Aufgabe`) getrennt; Namensnähe `Todo`↔`WorkItem` ist beabsichtigt
   (Todo-Listen-Eintrag vs. verwaltete Entwicklungsaufgabe). *Annahme.*

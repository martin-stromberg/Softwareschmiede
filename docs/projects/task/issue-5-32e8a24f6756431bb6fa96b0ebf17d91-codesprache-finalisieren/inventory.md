# Bestandsaufnahme – Codesprache finalisieren

Stand: 2026-11-20, Basis `origin/staging` (633a3af2). Quellen: zwei Bestandsaufnahme-Subagenten
(Bezeichner/Persistenz bzw. Doku/Kommentare) plus eigene Stichproben (DbContext, CI-Workflows,
SCM-Austauschprotokoll).

## 1. Umfang der deutschen Bezeichner

- **~788 .cs-Dateien** insgesamt; ~270 Dateien tragen deutsch klingende Namen
  (u. a. ~193 eindeutig deutsche Dateinamen außerhalb `Migrations/`).
- **Deutsche Typnamen**: grob 150–200 (Entities, Enums, Interfaces, ValueObjects,
  Services, ViewModels, Dialoge, Converter, Behaviors, Testklassen/-helfer).
- **Deutsche Member** (Properties, Methoden, Felder, Enum-Member): mehrere hundert,
  konzentriert in `AppEinstellungService` (~30 Methoden), `AufgabenListenModusService`,
  `SettingsViewModel`, `TaskDetailViewModel`, `MainWindowViewModel`, SCM-Services.
- **Deutsche lokale Variablen/Parameter**: geschätzt ~800–1.500 im Kern+App,
  plus mehrere hundert in Tests (`var aufgabe`, `einstellung`, `schluessel`,
  `anzeige`, `meldung`, `liste`, …). Parameter `aufgabeId` allein in ~206 Dateien.
- **Namespaces/Ordner**: durchgängig englisch — keine Umbenennung nötig.

## 2. Schichten-Überblick deutscher Typen (Auswahl, vollständig in Arbeitslisten)

- `Domain/Entities`: `Projekt`, `Aufgabe`, `AppEinstellung`, `Protokolleintrag`,
  `TestErgebnis`, `IssueReferenz`, `AlertReferenz`, `PullRequestReferenz`,
  `PromptVorlage`, `BenachrichtigungsEinstellung`, `BenachrichtigungsAudioDatei`,
  `BenachrichtigungsDispatchLog`, `RepositoryStartKonfiguration`,
  `RepositoryInitialisierungKonfiguration`, `PluginKonfiguration`, `ZeitgesteuerterPrompt`.
- `Domain/Enums`: `AufgabeStatus`, `AufgabeAusfuehrungsStatus`, `AufgabeLaufStatus`,
  `AufgabenFilterTyp`, `ProtokollTyp`, `ProjektStatus`, `PluginKategorie`,
  `KiAusfuehrungsZustand`, `FolgeanweisungsKontextmodus`, `BenachrichtigungsKanal/-Modus/-Entscheidung`,
  `ScmBackmergeModus`, `ScmTaskCompletionPruefmodus`, `VerwerfenAktion` + Extensions-Klassen.
- `Domain/Interfaces`/`ValueObjects`: `IArbeitsverzeichnisResolver`,
  `IBenutzerkontextService`, `IBenachrichtigungsAudioService`, `IBenachrichtigungsBannerService`,
  `ArbeitsverzeichnisResolutionResult`, `GitArbeitsbereich`, `ProzessStartAnfrage`.
- `Application/Services`: `AppEinstellungService`, `AufgabeService`, `AufgabeRecoveryService`,
  `AufgabeLaufAktivitaet`, `AufgabeLaufdatenChangedNotifier`, `IAktiveAufgabenService`,
  `Arbeitsverzeichnis*`, `Benachrichtigungs*`, `CliOutputProtokollWriter`,
  `EntwicklungsprozessService`, `GitKlonHelper`, `KiAufgaben*`, `KiAusfuehrungs*`,
  `LaufendeAutomatisierungenZaehlung`, `ProjektService`, `ProtokollService`,
  `PromptVorlagen*`, `PromptZeitVersandService`/`ZeitgesteuerterPromptStore`,
  `PullRequestReferenzService`, `ScmDispatchPrioritaet` (Records `EreignisAuswahl`,
  `AufgabenZustand`), `ScmIssueAufbereitung*`, `ScmStartPlatz*`, `ScmStartPruefungsService`,
  `UpdatePfadKanalResolver`, `UpdateModels` (Enum `UpdateKanal`, Member `Erfolg`/`Fehlgeschlagen`/`KeinTreffer`).
- `Infrastructure`: `DatenbankPfadResolver`, `ArbeitsverzeichnisResolver`,
  `BenutzerkontextService`, `CliOutputAufzeichnung`, `CliReplayAufzeichnungStore`,
  `TerminalBackendEmpfehlung` (+ `PtyVerfuegbar`, `BackendEmpfehlung`), `CliRuntimeStatus`-Member,
  `AufzeichnenderProzessStarter`, `AufzeichnenderSystemShutdownService`,
  `SoftwareschmiededDbContext` (Tippfehler „dd" im Namen).
- `Softwareschmiede.App`: ViewModels (`AufgabenListenAnsicht`, `AufgabenListenModus`,
  `DateibrowserAnsichtsmodus`, `AktiveAufgabePanelItem`, `ArbeitsverzeichnisBearbeitenViewModel`,
  `AufgabePausierenDialogViewModel`, `KonsolenTestViewModel`, `MainWindowOptionaleDienste`,
  `MainWindowUpdateDienste`, `CliChunkAnzeigeEintrag`, `CliChunkQuelltextFormatter`,
  `DetailAnsicht`, `PromptVorlageEntry`, `IssueAufbereitung*Option/Entry`),
  Services (`AufgabenListenModusService/-Bindung`, `AufgabePausierenErgebnis`,
  `AutoShutdownEinstellungenService`, `IUpdateVersuchProtokoll`, `UpdateE2E*`,
  `ProtokollierenderUpdateService`), Dialoge (`ArbeitsverzeichnisBearbeitenDialog`,
  `AufgabePausierenDialog`, `KonsolenTestDialog`), Converter (`KiAusfuehrungsStatus*Converter`),
  Behaviors (`StatusAenderungsErkennung`, `StatusUebergangsAnimation`),
  Option-Records + `*Texte`-Klassen (`UpdatePfadTexte`, `UpdateModusTexte`,
  `ScmKontoModusTexte`, `ScmBackmergeModusTexte`, `ScmTaskCompletionPruefmodusTexte`).
- `Softwareschmiede.Plugin.Contracts`: `ScmKontoModus` (`Benutzerkonto`, `SeparatesKonto`),
  `TestStatus` (`Bestanden`, `Fehlgeschlagen`, `Uebersprungen`), `PullRequestReferenzRolle`,
  Record-Member `Konto` in `ScmEventRepository`.
- Tests: `AufgabeServiceTests*`, `ProjektServiceTests`, `ProtokollServiceTests`,
  `AufgabeRecoveryServiceTests`, `*ListenModus*Tests`, `Arbeitsverzeichnis*Tests`,
  `CliReplayAufzeichnungStoreTests`, `Aufzeichnender*Tests`, `ScmIssueAufbereitung*Tests`,
  `RemoveAutonomAufgabeModelsMigrationTests`, `E2E_AufgabenListeModi`,
  `E2E_TaskWechselUeberMenue`, `E2E_VersionAnzeige`, `E2E_RepositoryInitialisierung*Tests`,
  E2E-Page-Objects mit deutschen Helfer-Membern (`AufgabenListeAutomationId`,
  `GetAufgabenGruppenNamen`, `WaitForAufgabenGruppe`).

## 3. Persistenz- und Kompatibilitätsrisiken (KERNbefund)

### 3.1 EF Core (SQLite, `SoftwareschmiededDbContext`)

- Tabellennamen = DbSet-Eigenschaftsname (deutsch: `Aufgaben`, `Projekte`,
  `Protokolleintraege`, …); Spaltennamen = Property-Namen (viele deutsch:
  `ErstellungsDatum`, `LaufStatus`, `Schluessel`, `Wert`, `Titel`, `Zweck`, …).
- **~20 Properties mit `HasConversion<string>()`** → Enum-*Membernamen* sind
  persistierte Strings in SQLite: `Aufgabe.Status` (`Neu`/`Gestartet`/`Wartend`/`Beendet`/`Archiviert`),
  `Aufgabe.LaufStatus`, `Protokolleintrag.Typ` (`ProtokollTyp`), `PluginKategorie`,
  `BannerModus`/`TonModus`, `Kanal`/`Modus`/`Entscheidung`, `DiffType`, `LineStatus`,
  `CachingStrategy`, `Provider`, `MergeStatus`, `MonitoringPhase`, Workflow `Status`/`Conclusion`.
- `ScmIncomingEvent.Status` = rohe deutsche String-Literale (`"Aufgenommen"`,
  `"Zugestellt"`, `"InBearbeitung"`, `"Verzögert"`) — persistiert.
- `Aufgabe.Zweck`/`ScmIncomingEvent.Zweck` = Literal `"IssueAufbereitung"` — persistiert.
- Settings-Keys in `AppEinstellung.Schluessel` sind englisch → sicher; aber
  `AufgabenListenModus`-Membernamen (`Datum`/`Repository`/`Ki`) werden per `ToString()`
  unter `aktiveaufgaben.listenmodus*`-Keys als String persistiert → Umbenennung setzt
  Nutzereinstellung still zurück (Mapping nötig).
- `GitRepository.PluginTyp`/`PluginKonfiguration.PluginTyp` = Plugin-Präfixe wie `"GitHub"` → englisch, sicher.
- Historische `Migrations/**` sind eingefrorene Historie und werden nicht umbenannt;
  der `ModelSnapshot` muss dagegen zum neuen Modell passen.

### 3.2 SCM-Austauschprotokoll (`.softwareschmiede-scm-exchange`)

- `ScmExchangeRequest(..., Guid AufgabeId, ...)` serialisiert mit
  `JsonSerializerDefaults.Web` → Wire-Feld **`aufgabeId`** in `request.json`;
  `ScmEventRepository.Konto` + `ScmKontoModus`-Member → `"konto"`, `"benutzerkonto"`,
  `"separatesKonto"` in `ObservationJson`/`RequestJson` (persistierte DB-Spalten!)
  und in Austauschdateien. Leser: App + externe CLI-Agenten + Tests +
  `docs/help/aufgaben/api.md` (dokumentiertes API). Umbenennung ohne Pinning = Breaking.
- Binärformat `.clireplay` (`SWCLRPLY`) ist positional → umbenennungssicher.

### 3.3 XAML/WPF

- Deutsche `x:Class` in 3 Dialogen; ~40 deutsche `AutomationId`/`AutomationProperties.Name`
  (FlaUI-Selektoren mit 73+ Testreferenzen); **~100+ deutsche `{Binding}`-Pfade**
  (WPF-Bindings brechen *lautlos zur Laufzeit* — kein Compile-Check); deutsche `x:Name`.
- UI-Texte sind und bleiben deutsch (harte Strings in XAML/ViewModels).

### 3.4 Sonstiges

- `docs/help/**` dokumentiert teils technische Bezeichner (z. B. `aufgabeId` in
  `docs/help/aufgaben/api.md`, `docs/help/terminal/api.md`) → Dokumentation muss
  mit den gewählten Zielnamen synchronisiert werden.
- E2E-Tests adressieren UI-Elemente über deutsche AutomationIds/Namen → Test-Selektoren
  müssen im selben Schritt wie XAML angepasst werden.

## 4. Doku- und Kommentar-Sprache (Audit)

- `///`-XML-Kommentare: fast ausschließlich deutsch; **~30 Dateien mit ~130–150
  englischen Blöcken**, ~95 % in Testprojekten (SCM-Event-/Dispatch-Cluster,
  Plugin-Testdateien); produktiv nur `ScmRestrictedProcessAccess.cs`,
  `ScmEventExchangeProtocol.cs`, `ScmCliAccessEvaluator.cs`, `App/Services/Testing/ScmE2E*`.
- `//`-Kommentare: überwiegend deutsch, englische Cluster im SCM-Exchange-Bereich
  (nicht gefordert, optional mitübersetzen).
- `.md`-Dokumentation: fast alles deutsch. Englisch/uneindeutig:
  `docs/RELEASE_NOTES.md` (ältere Abschnitte), `CHANGELOG.md` (Überschriften + einzelne
  Einträge), `CONTRIBUTING.md` (Überschriften), `.github/ISSUE_TEMPLATE/*.md` (3 Defaults),
  `.github/skills/razor-viewmodel/SKILL.md` (**Fremd-Artefakt**, referenziert
  `FinanceManager.Web`), `.github/upgrades/**` (4 Fremd-Artefakte, FinanceManager-Bezug).
- `site/index.html` deutsch. Keine `.resx` vorhanden.

## 5. CI/Qualitätsgates (PR → staging)

- `dotnet format Softwareschmiede.slnx --verify-no-changes`
- `dotnet build -p:TreatWarningsAsErrors=true` (Warnungen = Fehler!)
- `dotnet test` beider Testprojekte `Category!=OsInterface` + Coverage-Schwelle
- OS-Interface-Lane (`Category=OsInterface`, inkl. FlaUI-E2E auf Runner)
- Lokale Regeln (CLAUDE.md): voller Build vor Tests; `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`
  für `dotnet test` in dieser Sandbox; `dotnet test` niemals im Hintergrund;
  nie `Softwareschmiede.App.exe` beenden (Self-Hosting); EF-Migrationen nur via `dotnet ef`.

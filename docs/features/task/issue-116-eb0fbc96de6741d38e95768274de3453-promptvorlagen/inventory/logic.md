# Logik und UI

## `SettingsViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|--------------|------------------|
| `LadenAsync` | `private` | Laedt Arbeitsverzeichnis, Standard-KI-/SCM-Plugin, Designmodus und Plugin-Setting-Gruppen. |
| `SpeichernAsync` | `private` | Validiert Plugin-Pflichtfelder und speichert Arbeitsverzeichnis, Default-Plugins, Designmodus und Plugin-Settings. |
| `VerwerfenAsync` | `private` | Laedt gespeicherte Einstellungen erneut. |
| `LoadScmPluginSettings` | `private` | Laedt Einstellungsgruppen fuer ein SCM-Plugin. |
| `LoadKiPluginSettings` | `private` | Laedt Einstellungsgruppen fuer ein KI-Plugin. |
| `LadePluginEinstellungen` | `private` | Baut `PluginSettingGroupEntry`/`PluginSettingEntry` aus Plugin-Setting-Definitionen. |
| `SpeicherePluginEinstellungen` | `private` | Schreibt Plugin-Settings ueber `PluginSettingsService`. |
| `ValidierePflichtfelder` | `private` | Validiert SCM- und KI-Plugin-Settings. |
| `Dispose` | `public` | Meldet das DarkMode-Event ab. |

Abonnierte Events: `DarkModeService.ModeChanged`.

Publizierte Events: keine.

`SettingsView.xaml` zeigt Ribbon-Aktionen `Speichern` und `Verwerfen` sowie Tabs `Allgemein`, `Quellcodeverwaltung` und `KI`. Plugin-Felder werden dynamisch ueber `PluginSettingFieldTemplateSelector` dargestellt. Es existiert kein Tab, keine Liste und kein Eingabemodell fuer Promptvorlagen.

## `TaskDetailViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|--------------|------------------|
| `LadenAsync` | `private` | Laedt Aufgabe inklusive `Projekt`, `GitRepository`, `IssueReferenz`, `Protokolleintraege`; startet bei Status `Gestartet` ggf. die CLI automatisch neu. |
| `LadeVerfuegbarePluginsAsync` | `private` | Laedt verfuegbare KI-Plugin-Prefixe. |
| `CliStoppenAsync` | `private` | Stoppt die CLI fuer die aktuelle Aufgabe. |
| `CliNeustartenAsync` | `private` | Startet eine vorhandene CLI-Aufgabe erneut. |
| `AufgabeAbschliessenAsync` | `private` | Schliesst die Aufgabe ueber `EntwicklungsprozessService` ab. |
| `SpeichernAsync` | `private` | Speichert Titel und Beschreibung. |
| `LoeschenAsync` | `private` | Loescht die Aufgabe nach Dialogbestaetigung. |
| `IssueZuweisenAsync` | `private` | Oeffnet Issue-Auswahl und speichert die Issue-Referenz. |
| `IssueBrowserOeffnen` | `private` | Oeffnet die aktuelle Issue-URL per Shell. |
| `InfoCliToggle` | `private` | Wechselt zwischen CLI- und Info-Ansicht. |
| `StartenAsync` | `private` | Loest KI-Plugin auf, startet Entwicklungsprozess und CLI. |
| `PluginWechselAsync` | `private` | Stoppt laufende CLI und startet sie mit anderem KI-Plugin neu. |
| `CliAutomatischNeustartenAsync` | `private` | Startet CLI bei bereits gestarteter Aufgabe erneut. |
| `StartCliAndUpdateStateAsync` | `private` | Ruft `KiAusfuehrungsService.StartWithPseudoConsoleAsync` auf und meldet die Session an die View. |
| `ResolvePluginViaDialogAsync` | `private` | Oeffnet Plugin-Auswahl und speichert ggf. Projekt-Default. |
| `AttachCliStatusSession` | `private` | Bindet Laufzeitstatus-Events der PseudoConsoleSession. |
| `UpdateCliStatusText` | `private` | Mappt `CliRuntimeStatus` auf Statusleistentext. |
| `GetPseudoConsoleSession` | `public` | Liefert die aktive PseudoConsoleSession fuer die Aufgabe. |
| `Dispose` | `public` | Meldet CLI-Events ab und beendet lokale Lade-Cancellation. |

Abonnierte Events: `KiAusfuehrungsService.CliProcessStatusChanged`, `PseudoConsoleSession.RuntimeStatusChanged`.

Publizierte Events: `PseudoConsoleSessionGestartet`, `CliGestoppt`.

`TaskDetailView.xaml` enthaelt Ribbon-Gruppen `Navigation`, `Aufgabe`, `CLI` und `Issue`. Es gibt keine ComboBox fuer Promptvorlagen. Die CLI-Anzeige ist `TerminalControl` und wird ueber `TaskDetailView.xaml.cs` an die aktuelle `PseudoConsoleSession` gebunden.

## `TerminalControl`
Datei: `src/Softwareschmiede.App/Controls/TerminalControl.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|--------------|------------------|
| `OnSessionChanged` | `private` | Wechselt die gebundene `PseudoConsoleSession` und abonniert `BufferChanged`. |
| `OnPreviewKeyDown` | `protected override` | Kodiert Tastatureingaben und schreibt sie in `Session.InputStream`; behandelt auch `Ctrl+V`. |
| `OnTextInput` | `protected override` | Kodiert Texteingaben und schreibt sie in `Session.InputStream`. |
| `WriteToInputStream` | `private` | Schreibt Bytes synchron an die Session und markiert Eingabeaktivitaet. |
| `ReadClipboardAndInsertAsync` | `private` | Schreibt Clipboard-Text kodiert in die Session. |
| `WriteToInputStreamAsync` | `private` | Schreibt Bytes asynchron an die Session und markiert Eingabeaktivitaet. |

Abonnierte Events: `PseudoConsoleSession.BufferChanged`.

Publizierte Events: keine.

Der vorhandene Eingabepfad ist UI-nah und bytebasiert. Eine oeffentliche Methode zum Senden eines bereits aufgeloesten Prompttexts existiert nicht.

## `KiAusfuehrungsService`
Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|--------------|------------------|
| `StartCliAsync` | `public` | Startet klassischen CLI-Prozess ueber `ProcessStartInfo`. |
| `StartWithPseudoConsoleAsync` | `public` | Startet `cmd.exe` via ConPTY, baut Plugin-Befehl und sendet ihn verzoegert. |
| `GetPseudoConsoleSession` | `public` | Liefert die laufende PseudoConsoleSession einer Aufgabe. |
| `StopCliAsync` | `public` | Stoppt laufenden CLI-Prozess. |
| `GetLastExitCode` | `public` | Liefert letzten Exit-Code, falls bekannt. |
| `UpdateHeartbeat` | `public` | Aktualisiert den In-Memory-Heartbeat des Handles. |
| `Dispose` | `public` | Beendet und entsorgt laufende Prozesse/Sessions. |
| `SendCommandDelayedAsync` | `private` | Sendet den initialen Plugin-Befehl an `cmd.exe`. |
| `BuildCliCommand` | `private static` | Baut den CLI-Befehl aus `ProcessStartInfo.FileName` und `Arguments`. |

Abonnierte Events: `Process.Exited`.

Publizierte Events: `CliProcessStatusChanged`, `RunningCountChanged`.

## `PseudoConsoleSession`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|--------------|------------------|
| `MarkOutputActivity` | `public` | Markiert Ausgabeaktivitaet und setzt Runtime-Status auf laufend. |
| `MarkInputActivity` | `public` | Markiert Eingabeaktivitaet und setzt Runtime-Status auf laufend. |
| `Resize` | `public` | Aendert die ConPTY-Groesse. |
| `Dispose` | `public` | Beendet Leseschleife und gibt Streams/Handles frei. |
| `ReadLoopAsync` | `private` | Liest Ausgabe, parsed ANSI-Sequenzen und aktualisiert `TerminalBuffer`. |

Publizierte Events: `RuntimeStatusChanged`, `BufferChanged`.

## `AppEinstellungService`
Datei: `src/Softwareschmiede/Application/Services/AppEinstellungService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|--------------|------------------|
| `GetSettingAsync` | `public` | Liest einen stringbasierten Setting-Wert. |
| `GetIntSettingAsync` | `public` | Liest einen Integer-Setting-Wert. |
| `GetBoolSettingAsync` | `public` | Liest einen Boolean-Setting-Wert. |
| `SetSettingAsync` | `public` | Erstellt oder aktualisiert ein Key-Value-Setting. |
| `SetIntSettingAsync` | `public` | Speichert Integer als String. |
| `SetBoolSettingAsync` | `public` | Speichert Boolean als String. |
| `GetWindowGeometryAsync` | `public` | Liest Fenstergeometrie-Settings gesammelt. |
| `SetWindowGeometryAsync` | `public` | Speichert Fenstergeometrie-Settings gesammelt. |

Vorhandene Keys: Fensterposition/-groesse, `ui.designmode.name`, `ki.plugin.default`, `scm.plugin.default`, `logging.level`. Keine Promptvorlagen-Keys.

## `AufgabeService`
Datei: `src/Softwareschmiede/Application/Services/AufgabeService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|--------------|------------------|
| `GetDetailAsync` | `public` | Laedt Aufgabe mit `Projekt`, `IssueReferenz`, `GitRepository.StartKonfiguration` und Protokoll. |
| `CreateAsync` | `public` | Erstellt Aufgabe im Status `Neu`. |
| `UpdateAsync` | `public` | Aktualisiert Titel, Beschreibung und KI-Plugin-Prefix. |
| `SavePromptVorschlagAsync` | `public` | Speichert einen aufgabenspezifischen Vorschlagsprompt und optionalen Zeitpunkt. |
| `ClearPromptVorschlagAsync` | `public` | Entfernt den aufgabenspezifischen Vorschlagsprompt. |
| `StartenAsync` | `public` | Setzt Status `Gestartet`, Branch und Klonpfad. |
| `AktivenLaufSetzenAsync` | `public` | Persistiert aktive Run-ID, Heartbeat, CLI-Startzeit und Laufstatus. |
| `AktivenLaufBeendenAsync` | `public` | Entfernt aktive Run-ID und Laufstatus. |
| `AktualisiereLaufStatusAsync` | `public` | Aktualisiert `Aufgabe.LaufStatus`, solange ein aktiver Lauf existiert. |

`GetDetailAsync` stellt die Kontextdaten fuer Platzhalter bereit. `SavePromptVorschlagAsync` ist kein Vorlagenmechanismus, sondern speichert genau einen Vorschlag pro Aufgabe.

## `App`
Datei: `src/Softwareschmiede.App/App.xaml.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|--------------|------------------|
| `StartupAsync` | `private` | Baut Host, startet Services, initialisiert `CliProcessManager`, fuehrt `db.Database.MigrateAsync()` aus und zeigt `MainWindow`. |
| `ConfigureServices` | `private static` | Registriert DbContext, Services, Plugins, ViewModels und MainWindow. |

Persistenz: SQLite unter `%LocalAppData%/Softwareschmiede/softwareschmiede.db`, alternativ `SOFTWARESCHMIEDE_TEST_DB_PATH`. Migrationen werden beim Start angewendet. Ein Seed-Aufruf fuer Promptvorlagen existiert nicht.

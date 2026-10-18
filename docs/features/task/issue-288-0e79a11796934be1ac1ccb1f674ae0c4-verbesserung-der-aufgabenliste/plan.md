# Umsetzungsplan: Verbesserung der Aufgabenliste im Menü — kompakte Kacheln mit Symbolen und umschaltbaren Anzeigemodi

## Übersicht

Die Kacheln der "Aktive Aufgaben"-Liste (`ActiveTasksListControl`, genutzt in der `MainWindow`-Seitenleiste und im `DashboardView`) werden von sechs auf drei Zeilen verdichtet: Titel, Repository-Name als Untertitel und eine Symbolzeile (SCM-, KI-, Statussymbol sowie der bestehende To-do-Button). Der separate "→"-Button entfällt; die gesamte Kachel wird über einen vollflächigen `Button` klickbar. Zusätzlich wird die Liste über drei kleine Modus-Buttons zwischen "Datum" (flach, bisheriges Verhalten), "Repository" und "KI" (gruppiert) umschaltbar — in der Seitenleisten-Titelleiste und im Dashboard. Der gewählte Modus wird **pro Ansicht persistiert** (`AppEinstellung`-Tabelle) und beim App-Start wiederhergestellt. Dafür wird der Plugin-Vertrag binärkompatibel um ein `PluginSymbol` erweitert, die Darstellungslogik (Symbol-Converter, Gruppierungs-/Sortier-Deskriptoren, Modus-Zustand) ergänzt und ein kleiner Persistenz-Service nach dem `DarkModeService`-Muster angelegt. Keine Schema- oder Domain-Änderung (die `AppEinstellung`-Schlüssel/Wert-Tabelle existiert bereits).

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Kachel-Klickmechanik | Der Kachel-Wurzelknoten wird ein `Button` mit eigenem `ControlTemplate` (bisheriger `Border` wandert ins Template); `AutomationProperties.Name="AufgabeNavigieren:{Titel}"` wird auf den Button übernommen | Der bisherige `Border` erzeugt keinen `AutomationPeer` und ist per UIA unsichtbar — ein echter `Button` liefert den klickbaren E2E-/Barrierefreiheits-Anker ohne zusätzliches Overlay-Element. Der bestehende Anker `AufgabeNavigieren:{Titel}` bleibt dadurch für `MenuView.NavigateToTask` und `E2E_AufgabePausieren` (Warte-Anker) unverändert nutzbar. `MouseBinding` und das `AufgabenKachelMitNavigationButtonTemplate` entfallen. |
| Plugin-Symbol im Vertrag | `string PluginSymbol` als Default-Interface-Member an `IPlugin` (z. B. `=> "◻"`), überschreibbar als `virtual`-Eigenschaft an `GitPluginBase<TPlugin>` und `CliKiPluginBase` | Plugins werden separat kompiliert und per `AssemblyLoadContext` geladen (`PluginManager.DiscoverPlugins`); ein Default-Member ist binärkompatibel (alte DLLs laden weiter). Das Muster "neue Member als Default/`virtual`" ist im Vertrag bereits etabliert. Ein Mitglied an `IPlugin` gilt automatisch auch für IDE-Plugins; eine Trennung auf `IGitPlugin`/`IKiPlugin` wäre redundante Doppel-Deklaration. |
| Gruppierung | Eigene `ListCollectionView` pro `ActiveTasksListControl`-Instanz über der `ItemsSource` + `ItemsControl.GroupStyle` mit `HeaderTemplate`; die Sort-/Gruppendeskriptoren liefert eine neue statische Hilfsklasse `AufgabenListenAnsicht` | `AktiveAufgabenListe` wird zwischen Seitenleiste und Dashboard geteilt — `CollectionViewSource.GetDefaultView` wäre eine gemeinsame Default-View und würde die Gruppierung einer Ansicht in die andere leaken. Explizite Gruppen-ViewModels (Header + Items) wurden verworfen: Sie würden den `ItemsSource`-Typ und den `DashboardViewModel.Initialize`-Vertrag verändern, ohne fachlichen Mehrwert. Gruppenschlüssel sind flache Properties am `AktiveAufgabePanelItem`. |
| Sortierung | `SortDescriptions` auf der `ListCollectionView`: `[*GruppeIstFallback` aufsteigend, `*GruppenName` aufsteigend, `SortierZeitpunkt` absteigend`]`; im Modus "Datum" nur `SortierZeitpunkt` absteigend | Gruppen erscheinen in der Reihenfolge ihrer ersten Elemente — die Item-Sortierung erzeugt damit alphabetische Gruppen mit Fallback-Gruppe ("Nicht ausgeführt"/"Ohne Repository") am Ende und gruppeninterner Reihenfolge nach letzter Ausführung (identisch zur Datenquelle `GetAktiveAufgabenAsync`). Bei der Umsetzung prüfen, ob die Quell-Tie-Breaker (`ThenBy(Titel)`/`ThenBy(Id)` bei Zeitstempel-Gleichstand) als weitere `SortDescriptions` nachgezogen werden müssen. |
| Testbare Gruppierungslogik | `AufgabenListenAnsicht` liefert die Deskriptoren als Daten (`IReadOnlyList<SortDescription>` + optionale `PropertyGroupDescription`), das Control wendet sie an | Reine Datenrückgabe ist ohne WPF-Dispatcher/Visual-Tree unit-testbar; das Control bleibt eine dünne Anwender-Schicht. |
| Modus-Zustand und -Persistenz | Laufzeit-Property `AufgabenListenModus` je ViewModel (`MainWindowViewModel` für die Seitenleiste, `DashboardViewModel` für das Dashboard), an die Dependency Property `ListenModus` des Controls gebunden; Umschaltung über drei `RadioButton`s mit dem vorhandenen `EnumToBoolConverter`. Änderungen werden über den neuen `AufgabenListenModusService` in `AppEinstellung` persistiert, beim Start wiederhergestellt. | Persistenz ist vom Anwender gefordert. `EnumToBoolConverter` ist das etablierte Muster für Enum-Umschaltung über RadioButtons (Vorbild `AufgabenFilter` in `ProjectDetailView.xaml`). Eigenständige Zustände je Ansicht erlauben unabhängige Modi; die Buttons werden in beiden Titelleisten angeboten. |
| Persistenz-Schlüssel: geteilt vs. je Ansicht | **Zwei eigene Schlüssel**: `aktiveaufgaben.listenmodus` (Menü/Seitenleiste, Pflichtteil) und `aktiveaufgaben.listenmodus.dashboard` (Dashboard), als Konstanten auf `AppEinstellungService` | Nächstliegende konsistente Variante zur bereits festgelegten unabhängigen Zustandsführung je Ansicht: Jede Ansicht stellt exakt den Modus wieder her, den der Anwender dort zuletzt gewählt hat. Ein geteilter Schlüssel würde bei einer Änderung im Dashboard den persistierten Menü-Modus still ändern, ohne den Live-Zustand der Seitenleiste zu synchronisieren (zwischen den beiden ViewModels existiert kein Benachrichtigungsmechanismus) — Live- und Persistenzzustand würden divergieren. |
| Zugriff auf `AppEinstellung` aus den ViewModels | Neuer Singleton `AufgabenListenModusService` (`src/Softwareschmiede.App/Services/`) mit `IServiceScopeFactory` + `ILogger`; pro Operation `CreateScope()` → scoped `AppEinstellungService` auflösen | Exaktes Vorbild: `DarkModeService` persistiert auf demselben Weg eine UI-Anzeigeoption (`ui.designmode.name`). Die direkte Injektion des scoped `AppEinstellungService` in die langlebigen, am Root-Provider aufgelösten ViewModels würde dessen `DbContext` mit den bereits injizierten Root-Scope-Services (`AufgabeService`/`TodoService` — 5-Sekunden-Refresh!) teilen; ein paralleles `SetSettingAsync` zum Moduswechsel könnte dann mit `GetAktiveAufgabenAsync` auf derselben Context-Instanz kollidieren (EF-Core-"second operation"-Fehler). Die Scope-Factory-Kapselung ist auch das Muster von `TerminalSessionService` und `MainWindowViewModel.RefreshScmCountAsync`. |
| Wertformat des gespeicherten Modus | Enum-Name als String (`AufgabenListenModus.Datum.ToString()` → `"Datum"`); Laden per `Enum.TryParse` (Groß-/Kleinschreibung tolerant) + `Enum.IsDefined`-Prüfung; fehlender oder ungültiger Wert → Default `Datum` | Entspricht der Konvention von `ui.designmode.name` (Name statt Zahl) und bleibt bei Enum-Umbenennung lesbar. Die Parse-Fallback-Logik liegt im `AufgabenListenModusService` (der Enum lebt in `Softwareschmiede.App`, der `AppEinstellungService` in der Kern-Assembly kann ihn nicht kennen). |
| Status-Symbol | Neuer `KiAusfuehrungsStatusSymbolConverter` (nur Glyph: `⏸`/`⏳`/`▶`/`✓`); die Statusklassifikation (Pausiert → ScheduledPrompt → nicht aktiv → aktiv+LaufStatus → Wartend → Bereit) wird aus `KiAusfuehrungsStatusConverter` in einen gemeinsamen internen Helper (`KiAusfuehrungsStatusKlassifikation` + `KiAusfuehrungsZustand`) extrahiert | Volltext- und Symbol-Variante dürfen fachlich nicht divergieren. Der Volltext bleibt unverändert der `HelpText`/ToolTip-Vertrag (`WaitForTaskStatus`, `E2E_AufgabePausieren` erwarten z. B. "⏸ Pausiert (noch …)"). |
| `AufgabeStatus:{Titel}`-Anker | Das Status-`TextBlock` trägt weiterhin `AutomationProperties.Name="AufgabeStatus:{Titel}"` und den Volltext im `HelpText`; der sichtbare `Text` ist der Glyph. `StatusUebergangsAnimation.Status` wird an den **Symbol**-Converter gebunden (nicht an den Volltext) | Der Glyph je Zustand ist konstant; der Volltext enthält beim Pausiert-Status einen Countdown, der sich bei jedem 5-s-Refresh ändert und die Fade-Animation sonst dauerhaft re-triggern würde. |
| `StatusUebergangsAnimation` | `OnStatusChanged` akzeptiert zusätzlich `DataContext is AktiveAufgabePanelItem` (Id als Änderungsschlüssel) | Die Animation greift im Kachel-Template heute faktisch nicht (DataContext ist nie `Aufgabe`); ohne Erweiterung würde die angehängte Binding-Zeile im neuen Template weiterhin toter Code sein. |
| Untertitel der Kachel | `AktiveAufgabePanelItem.ProjektName` wird durch `RepositoryName` ersetzt: `GitRepository?.RepositoryName`, Fallback `Projekt?.Name` bei Aufgaben ohne Repository-Verknüpfung | Entspricht der Anforderung ("Name des Repository … als Untertitel") und passt zum Repository-Gruppierungsmodus; der Projektfallback verhindert leere Untertitel bei repo-losen Aufgaben (vom Anwender bestätigt). |
| Symbol-Elemente / UIA | SCM- und KI-Symbol-`TextBlock` tragen `AutomationId` (`AktiveAufgabeScmPlugin_{Titel}` bzw. bestehend `AktiveAufgabeKiPlugin_{Titel}`) und `ToolTip`/`AutomationProperties.HelpText="SCM: {Name}"`/`"KI: {Name}"`, aber **kein** `AutomationProperties.Name` | Ohne Name-Override ist der UIA-`Name` der gerenderte Glyph — die Symbolanzeige ist so per E2E direkt prüfbar. `MenuView.GetActiveTaskKiPluginName` liest künftig den `HelpText` statt `Name` (kleine, vereinzelte Anpassung). |
| UIA-Anker-Scoping (Ansicht-Doppelung) | Beide `ActiveTasksListControl`-Instanzen erhalten eine ansichtsspezifische `AutomationProperties.AutomationId` (`AktiveAufgabenListeMenue` in `MainWindow.xaml`, `AktiveAufgabenListeDashboard` in `DashboardView.xaml`); die E2E-Helfer suchen alle Template-Anker (`AufgabenGruppe:*`, `AufgabeNavigieren:*`, `AufgabeStatus:*`, `AktiveAufgabe*Plugin_*`, `OffeneTodos:*`) gescoped unterhalb des jeweiligen Control-Elements. Die außerhalb des Controls liegenden Modus-RadioButtons erhalten ansichtsspezifische Namen: `AufgabenModus:{m}` (Seitenleiste) vs. `DashboardAufgabenModus:{m}` (Dashboard) | Sobald das Dashboard sichtbar ist, existieren die neuen Anker `AufgabenModus:*` und `AufgabenGruppe:*` (ebenso die bestehenden Kachel-Anker) in beiden Ansichten; ein fensterweites `FindFirstDescendant` wäre mehrdeutig — ein Klick könnte den Dashboard-Modus schalten, während der Test auf Seitenleisten-Gruppen wartet (und umgekehrt). Scoping plus eigene Button-Namen machen beide Benutzerflüsse eindeutig adressierbar, ohne die bestehenden Seitenleisten-Anker umzubenennen. |

## Programmabläufe

### Kachel-Anzeige (Refresh)

1. `MainWindowViewModel.AktiveAufgabenAktualisierenAsync` lädt wie bisher `_aufgabeService.GetAktiveAufgabenAsync` (Filter `Gestartet`/`Wartend`, Sortierung `LetzterCliStartUtc ?? ErstellungsDatum` absteigend, max. 20) und `TodoService.GetOpenTodoCountsAsync`.
2. `MapAktiveAufgabePanelItem` befüllt zusätzlich `RepositoryName`, `ScmPluginSymbol`/`KiPluginSymbol` (über die verallgemeinerte `ResolvePlugin`-Methode), `RepositoryGruppenName`/`RepositoryGruppeIstFallback`, `KiGruppenName`/`KiGruppeIstFallback` und `SortierZeitpunkt`.
3. `AktiveAufgabenListe.ReplaceAll(...)` ersetzt die Items; die interne `ListCollectionView` des Controls aktualisiert sich automatisch (CollectionChanged) und wendet die zum aktuellen `ListenModus` gehörigen Sort-/Gruppendeskriptoren an.
4. Das Template rendert je Kachel drei Zeilen: `Titel`, `RepositoryName`, Symbolzeile (SCM-Glyph, KI-Glyph, Status-Glyph via `KiAusfuehrungsStatusSymbolConverter`, `OffeneTodos:{Titel}`-Button).

Beteiligte Klassen/Komponenten: `MainWindowViewModel`, `AufgabeService` (`IAktiveAufgabenService`), `TodoService`, `IPluginManager`, `AktiveAufgabePanelItem`, `ActiveTasksListControl`, `AufgabenListenAnsicht`, `KiAusfuehrungsStatusSymbolConverter`.

### Navigation per Kachelklick

1. Die Kachel ist ein `Button` (`Cursor="Hand"`, `Command={Binding NavigateCommand, RelativeSource=UserControl}`, `CommandParameter={Binding Id}`).
2. Click → `MainWindowViewModel.NavigateZuAufgabe(aufgabeId)` → `TaskDetailViewModel` als `CurrentView` (unverändert).
3. Der To-do-Button bleibt als verschachtelter `Button` eigenständig klickbar: Sein Click-Handling löst den äußeren Button nicht aus; `OffeneTodosButton_Click` (`e.Handled = true`) bleibt als Absicherung bestehen.

Beteiligte Klassen/Komponenten: `ActiveTasksListControl` (Template + Code-Behind), `MainWindowViewModel.NavigateZuAufgabe`, `TaskDetailViewModel`.

### Wiederherstellung des gespeicherten Modus beim Start

1. Der `MainWindowViewModel`-Konstruktor stößt `GespeichertenAufgabenListenModusLadenAsync()` per `SafeFireAndForget` an (Muster `VersionLadenImHintergrund`); der `DashboardViewModel`-Aufruf `Initialize(...)` (einmalig beim ersten `NavigateToDashboard`) stößt das Analogon für den Dashboard-Schlüssel an.
2. Der `AufgabenListenModusService` liest in einem eigenen DI-Scope über `AppEinstellungService.GetSettingAsync` den je Ansicht eigenen Schlüssel (`aktiveaufgaben.listenmodus` bzw. `aktiveaufgaben.listenmodus.dashboard`) und parst den Wert (`Enum.TryParse` + `Enum.IsDefined`; fehlend/ungültig → `Datum`).
3. Das ViewModel setzt den geladenen Modus über `_dispatcherInvoke` (MainWindowViewModel) bzw. direkt (DashboardViewModel) auf das Backing-Feld und löst `PropertyChanged` aus — **ohne** den Wert zurückzuschreiben (eigener Setzen-ohne-Speichern-Pfad, verhindert einen Schreibzugriff beim Start).
4. Race-Absicherung: Hat der Anwender bereits umgeschaltet, bevor der Laden-Task zurückkehrt (Flag `_aufgabenListenModusGeaendert`, gesetzt im Speicherpfad des Setters), wird der geladene Wert verworfen.
5. Die `RadioButton`s (`EnumToBoolConverter`) und die `ListenModus`-DP folgen dem PropertyChanged automatisch; `AufgabenListenAnsicht` konfiguriert die `ListCollectionView` entsprechend — bei gruppiertem Start sind Gruppenheader ohne Benutzeraktion sichtbar.

Beteiligte Klassen/Komponenten: `MainWindowViewModel`, `DashboardViewModel`, `AufgabenListenModusService`, `AppEinstellungService`, `ActiveTasksListControl`, `AufgabenListenAnsicht`.

### Modusumschaltung, Gruppierung und Persistenz

1. Drei `RadioButton`s (`AufgabenModus:Datum`/`AufgabenModus:Repository`/`AufgabenModus:KI`) binden `IsChecked` über `EnumToBoolConverter` mit `ConverterParameter` an `AufgabenListenModus` (zwei-Wege: `ConvertBack` parst den Enum-Wert).
2. Der Setter von `AufgabenListenModus` im ViewModel triggert `PropertyChanged`; die am Control gebundene DP `ListenModus` ändert sich.
3. Bei echter Wertänderung (`SetProperty` liefert `true`) setzt das ViewModel das Flag `_aufgabenListenModusGeaendert` und stößt `AufgabenListenModusService.SpeichereMenueModusAsync` bzw. `SpeichereDashboardModusAsync` per `SafeFireAndForget` an; der Service schreibt im eigenen Scope über `AppEinstellungService.SetSettingAsync` den Enum-Namen unter den Ansichts-Schlüssel.
4. Das Control ruft `AufgabenListenAnsicht.Konfiguriere(view, modus)` auf: "Datum" → nur `SortierZeitpunkt desc`, keine Gruppierung; "Repository" → `PropertyGroupDescription(RepositoryGruppenName)` + Sort `[RepositoryGruppeIstFallback asc, RepositoryGruppenName asc, SortierZeitpunkt desc]`; "KI" → analog mit `KiGruppenName`/`KiGruppeIstFallback`.
5. `ItemsControl.GroupStyle.HeaderTemplate` rendert den Gruppennamen (`CollectionViewGroup.Name`) mit `AutomationProperties.Name="AufgabenGruppe:{Name}"`.

Beteiligte Klassen/Komponenten: `MainWindowViewModel`/`DashboardViewModel` (`AufgabenListenModus`), `AufgabenListenModusService`, `AppEinstellungService`, `ActiveTasksListControl` (`ListenModus`-DP, `GroupStyle`), `AufgabenListenAnsicht`, `EnumToBoolConverter`.

### Plugin-Symbol-Auflösung

1. `MainWindowViewModel.ResolvePlugin(pluginType, prefix)` (Nachfolger von `ResolvePluginName`) findet das Plugin über `IPluginManager` (`GetSourceCodeManagementPlugins`/`GetDevelopmentAutomationPlugins`, Prefix-Vergleich `OrdinalIgnoreCase`).
2. Rückgabe `(Name, Symbol)`: `Name` = `PluginName` bzw. roher Prefix als Fallback; `Symbol` = `PluginSymbol` bzw. Fallback-Glyph, wenn das Plugin nicht auflösbar ist oder `PluginSymbol` leer/`null` liefert (Moq-Mocks ohne Setup liefern `null` — der Mapper muss das tolerieren).
3. `KiGruppenName` = aufgelöster `PluginName`, sonst roher Prefix (eigene Gruppe), sonst "Nicht ausgeführt" bei leerem Prefix (`KiGruppeIstFallback = true`). `RepositoryGruppenName` = `GitRepository?.RepositoryName` bzw. "Ohne Repository" (`RepositoryGruppeIstFallback = true`).

Beteiligte Klassen/Komponenten: `MainWindowViewModel`, `IPluginManager`, `IPlugin.PluginSymbol`, `AufgabenListenAnsicht` (Gruppen-Konstanten).

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `AufgabenListenModus` | Enum (`src/Softwareschmiede.App/ViewModels/`) | Werte `Datum`, `Repository`, `Ki`; Namenskonvention analog `DateibrowserAnsichtsmodus`. |
| `AufgabenListenModusService` | Singleton-Service (`src/Softwareschmiede.App/Services/`) | Lädt (`LadeMenueModusAsync`/`LadeDashboardModusAsync`) und speichert (`SpeichereMenueModusAsync`/`SpeichereDashboardModusAsync`) den Anzeigemodus je Ansicht über `AppEinstellungService` in eigenen DI-Scopes (`IServiceScopeFactory`, Muster `DarkModeService`); kapselt Enum↔String-Serialisierung inkl. `Datum`-Fallback bei fehlendem/ungültigem Wert. |
| `KiAusfuehrungsStatusSymbolConverter` | `IValueConverter` (in `src/Softwareschmiede.App/Converters/AppConverters.cs`) | Liefert nur den Status-Glyph (`⏸`/`⏳`/`▶`/`✓`); wird in `App.xaml` registriert. |
| `KiAusfuehrungsZustand` | interner Enum | Zustandsklassifikation (`Pausiert`, `PromptInWartestellung`, `Laeuft`, `Wartet`, `Bereit`), gemeinsame Basis beider Status-Converter. |
| `KiAusfuehrungsStatusKlassifikation` | interne statische Klasse | Extrahierte Statuslogik aus `KiAusfuehrungsStatusConverter` (Mapping `Aufgabe`/`AktiveAufgabePanelItem` → `StatusDaten` → `KiAusfuehrungsZustand` + Restzeit). |
| `AufgabenListenAnsicht` | statische Klasse (`src/Softwareschmiede.App/Controls/`) | `Konfiguriere(ListCollectionView, AufgabenListenModus)` bzw. Deskriptor-Erzeugung je Modus; beherbergt die Gruppen-Konstanten `NichtAusgefuehrt` und `OhneRepository`. |

## Änderungen an bestehenden Klassen

### `IPlugin` (Interface, `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IPlugin.cs`)

- **Neue Eigenschaften:** `PluginSymbol` (`string`) — Unicode-Glyph für die Anzeige in der Aufgabenliste; **Default-Interface-Implementierung** (Fallback-Glyph), damit bereits kompilierte Plugin-DLLs binärkompatibel weiter laden.

### `GitPluginBase<TPlugin>` (abstrakte Basisklasse)

- **Neue Eigenschaften:** `virtual string PluginSymbol` — überschreibbare SCM-Glyph-Eigenschaft mit Fallback-Default.

### `CliKiPluginBase` (abstrakte Basisklasse)

- **Neue Eigenschaften:** `virtual string PluginSymbol` — überschreibbare KI-Glyph-Eigenschaft mit Fallback-Default.

### Plugin-Implementierungen (`plugins/`)

- `BitbucketPlugin`, `GitHubPlugin`, `LocalDirectoryPlugin` (SCM) sowie `ClaudeCliPlugin`, `CodexPlugin`, `DevinPlugin`, `GitHubCopilotPlugin`, `KiSimulatorPlugin` (KI): **Neue Eigenschaften:** `override string PluginSymbol` mit je einem konkreten, unterscheidbaren Unicode-Glyph (Konvention der App; exakte Glyphen bei der Umsetzung festlegen).

### `AppEinstellungService` (`src/Softwareschmiede/Application/Services/`)

- **Neue Konstanten:** `AktiveAufgabenListenModusKey` (`"aktiveaufgaben.listenmodus"`, Menü/Seitenleiste) und `AktiveAufgabenListenModusDashboardKey` (`"aktiveaufgaben.listenmodus.dashboard"`, Dashboard) — Konvention: alle Schlüssel-Konstanten liegen auf diesem Service.
- Keine neuen Methoden — `GetSettingAsync`/`SetSettingAsync` genügen (String-Werte).

### `AktiveAufgabePanelItem` (`src/Softwareschmiede.App/ViewModels/`)

- **Neue Eigenschaften:** `RepositoryName` (`string`) — Untertitel (`GitRepository?.RepositoryName`, Fallback `Projekt?.Name`); `ScmPluginSymbol` (`string?`); `KiPluginSymbol` (`string?`); `RepositoryGruppenName` (`string`); `RepositoryGruppeIstFallback` (`bool`); `KiGruppenName` (`string`); `KiGruppeIstFallback` (`bool`); `SortierZeitpunkt` (`DateTimeOffset` = `LetzterCliStartUtc ?? ErstellungsDatum`).
- **Entfernte Eigenschaften:** `ProjektName` — wird von `RepositoryName` ersetzt (einzige Verwendung ist das Kachel-Template; keine Tests referenzieren es).

### `MainWindowViewModel` (`src/Softwareschmiede.App/ViewModels/`)

- **Neue Eigenschaften:** `AufgabenListenModus` (`AufgabenListenModus`, setzbar, Default `Datum`) — Modus der Seitenleisten-Liste; der Setter persistiert bei echter Änderung über `AufgabenListenModusService.SpeichereMenueModusAsync` (fire-and-forget via `SafeFireAndForget`) und setzt das interne Flag `_aufgabenListenModusGeaendert`.
- **Neue Methoden:** `GespeichertenAufgabenListenModusLadenAsync` (`Task`, im Konstruktor per `SafeFireAndForget` gestartet, direkt awaitbar für Tests) — liest den gespeicherten Menü-Modus via `LadeMenueModusAsync` und setzt ihn über `_dispatcherInvoke` auf das Backing-Feld + `PropertyChanged`, ohne zurückzuschreiben; wird übersprungen, wenn `_aufgabenListenModusGeaendert` bereits gesetzt oder kein Service injiziert ist.
- **Geänderte Methoden:** `ResolvePluginName` → `ResolvePlugin` (Rückgabe `Name` + `Symbol` statt nur Name; unveränderte Prefix-Auflösung und `null` bei leerem Prefix; Symbol-Fallback-Glyph bei nicht auflösbarem Plugin oder leerem `PluginSymbol`); `MapAktiveAufgabePanelItem` — befüllt alle neuen Item-Eigenschaften (Gruppennamen/Fallback-Flags gemäß dem Ablauf "Plugin-Symbol-Auflösung", `SortierZeitpunkt = aufgabe.LetzterCliStartUtc ?? aufgabe.ErstellungsDatum`); Konstruktor — startet zusätzlich den Modus-Ladevorgang.
- **Neue Felder:** `_aufgabenListenModus` (`AufgabenListenModus`), `_aufgabenListenModusGeaendert` (`bool`), `_aufgabenListenModusService` (aus `MainWindowOptionaleDienste`).

### `MainWindowOptionaleDienste` (Record, `src/Softwareschmiede.App/ViewModels/`)

- **Neuer Record-Parameter:** `AufgabenListenModusService? AufgabenListenModusService = null` — optionale Persistenz-Nahtstelle; wird in der `App.xaml.cs`-Factory über `sp.GetService<AufgabenListenModusService>()` aufgelöst. Bestehende Aufrufer (Tests) bleiben quellkompatibel (Default `null` → reiner Laufzeitzustand).

### `DashboardViewModel` (`src/Softwareschmiede.App/ViewModels/`)

- **Neue Eigenschaften:** `AufgabenListenModus` (`AufgabenListenModus`, setzbar, Default `Datum`) — eigener Modus-Zustand für die Dashboard-Liste; Setter persistiert analog über `SpeichereDashboardModusAsync` (+ `_aufgabenListenModusGeaendert`-Flag).
- **Neue Methoden:** `GespeichertenAufgabenListenModusLadenAsync` (`Task`) — wie im `MainWindowViewModel`, aber für den Dashboard-Schlüssel; wird aus `Initialize` per `SafeFireAndForget` angestoßen.
- **Geänderter Konstruktor:** neuer optionaler Parameter `AufgabenListenModusService? aufgabenListenModusService = null` (durch DI automatisch gefüllt; bestehende Test-Konstruktionen bleiben quellkompatibel).

### `ActiveTasksListControl.xaml`

- `AufgabenKachelInhaltTemplate` → drei Zeilen: `Titel`, `RepositoryName`, horizontale Symbolzeile (SCM-Glyph `TextBlock` mit `AutomationId="AktiveAufgabeScmPlugin_{Titel}"` + `HelpText`/`ToolTip="SCM: {ScmPluginName}"` + `Visibility` via `NullOrEmptyToVisibilityConverter`; KI-Glyph analog mit bestehender `AutomationId="AktiveAufgabeKiPlugin_{Titel}"` + `"KI: {KiPluginName}"`; Status-Glyph `TextBlock` via `KiAusfuehrungsStatusSymbolConverter` mit `AutomationProperties.Name="AufgabeStatus:{Titel}"`, `HelpText` = `KiAusfuehrungsStatusConverter` (Volltext inkl. Pausiert-Countdown), `behaviors:StatusUebergangsAnimation.Status` an den Symbol-Converter gebunden; `OffeneTodos:{Titel}`-Button bleibt).
- `AufgabeKachel:{Titel}`-Marker-`TextBlock` bleibt unverändert (`Aktiv:{IsAktiv};Pausiert:{IstPausiert}` im HelpText — E2E-Anker für die Abblendung).
- Neues einziges Kachel-Template: `Button` (`AutomationProperties.Name="AufgabeNavigieren:{Titel}"`, `Cursor="Hand"`, `Command`/`CommandParameter` wie bisher) mit `ControlTemplate`, das den bisherigen `Border` inkl. `IsAktiv`/`IstPausiert`-Triggern enthält.
- Entfernt: `AufgabenKachelMitNavigationButtonTemplate`, `AufgabenKachelVollflaechigKlickbarTemplate` und der `ShowNavigationButton`-Trigger am `ItemsControl`.
- Neu: `ItemsControl.GroupStyle` mit `HeaderTemplate` (Gruppenname mit `AutomationProperties.Name="AufgabenGruppe:{0}"`).

### `ActiveTasksListControl.xaml.cs`

- **Entfernte Dependency Properties:** `ShowNavigationButton` (einzige Nutzung `DashboardView.xaml` entfällt).
- **Neue Dependency Properties:** `ListenModus` (`AufgabenListenModus`, Default `Datum`; PropertyChanged-Callback wendet `AufgabenListenAnsicht` an).
- **Geändertes Verhalten:** `ItemsSource`-Callback erzeugt eine instanz-eigene `ListCollectionView` (kein `CollectionViewSource.GetDefaultView` — die ItemsSource wird mit dem Dashboard geteilt) und setzt sie als `ItemsSource` des internen `ItemsControl`.

### `MainWindow.xaml`

- Die Sektions-Titelleiste "Aktive Aufgaben" (Z. 127–131) wird zu einer horizontalen Zeile mit dem `TextBlock` links und drei kleinen `RadioButton`s rechts (`AutomationProperties.Name="AufgabenModus:Datum"`/`":Repository"`/`":KI"`, `IsChecked`-Binding an `AufgabenListenModus` über `EnumToBoolConverter`, `Visibility` an `IsNavigationExpanded` gebunden, damit die Buttons in der eingeklappten 48-px-Leiste nicht gequetscht werden).
- `ActiveTasksListControl` erhält `ListenModus="{Binding AufgabenListenModus}"` und `AutomationProperties.AutomationId="AktiveAufgabenListeMenue"` (E2E-Scoping-Wurzel für alle Template-Anker der Seitenleisten-Instanz).

### `DashboardView.xaml`

- Die "Aktive Aufgaben"-Überschrift (Z. 62–66) erhält dieselbe Modus-RadioButton-Zeile (gebunden an `DashboardViewModel.AufgabenListenModus`), jedoch mit ansichtsspezifischen Ankern `AutomationProperties.Name="DashboardAufgabenModus:Datum"`/`":Repository"`/`":KI"` — die Buttons liegen außerhalb des `ActiveTasksListControl` und müssen daher eigenständig von den Seitenleisten-Ankern (`AufgabenModus:*`) unterscheidbar sein (siehe Scoping-Entscheidung).
- `ActiveTasksListControl`: `ShowNavigationButton="False"` entfällt; `ListenModus="{Binding AufgabenListenModus}"` und `AutomationProperties.AutomationId="AktiveAufgabenListeDashboard"` (E2E-Scoping-Wurzel der Dashboard-Instanz) hinzu.

### `App.xaml` / `App.xaml.cs`

- `App.xaml`: Registrierung `KiAusfuehrungsStatusSymbolConverter` als globale Resource.
- `App.xaml.cs`: `services.AddSingleton<AufgabenListenModusService>()` (neben `DarkModeService`, Z. ~338); die `MainWindowOptionaleDienste`-Factory (Z. 357–361) löst `AufgabenListenModusService` zusätzlich auf.

### `KiAusfuehrungsStatusConverter` (`src/Softwareschmiede.App/Converters/AppConverters.cs`)

- **Geänderte Methoden:** `Convert` delegiert die Statusklassifikation an `KiAusfuehrungsStatusKlassifikation`; Volltext-Ausgabe bleibt identisch (Bestandsvertrag der 18 vorhandenen Tests und der E2E-`HelpText`-Anker).

### `StatusUebergangsAnimation` (`src/Softwareschmiede.App/Behaviors/`)

- **Geänderte Methoden:** `OnStatusChanged` akzeptiert neben `Aufgabe` auch `AktiveAufgabePanelItem` als `DataContext` (Schlüssel `item.Id`) — die Animation greift im Kachel-Template erst dadurch tatsächlich.

### `MenuView` (`src/Softwareschmiede.Tests/E2E/Views/`)

- **Geänderte Methoden:** `GetActiveTaskKiPluginName` liest `HelpText` statt `Name` des Elements `AktiveAufgabeKiPlugin_{Titel}` (der Name ist künftig der Glyph); `NavigateToTask` bleibt unverändert (`AufgabeNavigieren:{Titel}` ist weiterhin ein Button — jetzt die gesamte Kachel).
- **Neue Methoden:** `SetzeAufgabenListenModus(string modus)` (klickt `AufgabenModus:{modus}` — fensterweit eindeutig, da die Dashboard-Buttons `DashboardAufgabenModus:*` heißen); `GetAufgabenGruppenNamen()` bzw. `WaitForAufgabenGruppe(string name)` (Gruppenheader `AufgabenGruppe:{name}`, gesucht **gescoped unterhalb des Elements `AktiveAufgabenListeMenue`**); `GetActiveTaskKiPluginSymbol(string titel)` und `GetActiveTaskScmPluginSymbol(string titel)` (lesen gescoped den UIA-`Name` = Glyph von `AktiveAufgabeKiPlugin_{Titel}`/`AktiveAufgabeScmPlugin_{Titel}`); `GetActiveTaskStatusGlyph(string titel)` (findet das Element gescoped per `ByName("AufgabeStatus:{Titel}")` und liest den sichtbaren Glyph über das **TextPattern** (`Patterns.Text`/`DocumentRange.GetText`) — `element.Name` ist hier nicht verwendbar, weil `AutomationProperties.Name` den gerenderten Text durch den Anker-String überschreibt); `KlickeOffeneTodosButton(string titel)` (klickt `OffeneTodos:{Titel}` gescoped — Testauslöser für den verschachtelten Button).

### `DashboardView` (`src/Softwareschmiede.Tests/E2E/Views/`)

- **Neue Methoden:** `SetzeAufgabenListenModus(string modus)` (klickt `DashboardAufgabenModus:{modus}`); `GetAufgabenGruppenNamen()` bzw. `WaitForAufgabenGruppe(string name)` (Gruppenheader `AufgabenGruppe:{name}`, gesucht gescoped unterhalb `AktiveAufgabenListeDashboard`).

## Datenbankmigrationen

Keine — die `AppEinstellung`-Schlüssel/Wert-Tabelle existiert bereits; die neuen Einstellungen entstehen als Datenzeilen zur Laufzeit über `AppEinstellungService.SetSettingAsync`.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| Gespeicherter Modus-Wert (`AppEinstellung.Wert`) | Muss ein definierter `AufgabenListenModus`-Name sein (`Enum.TryParse` + `Enum.IsDefined`) | Fehlender, leerer oder ungültiger Wert → Default `Datum` (kein Fehler, kein Crash) |

Ansonsten keine neuen Validierungen — die Modus-Buttons sind eine Enum-Auswahl mit Gültigkeitsgarantie durch `RadioButton`/`EnumToBoolConverter`.

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `aktiveaufgaben.listenmodus` (`AppEinstellungService.AktiveAufgabenListenModusKey`) | `AppEinstellung`-Zeile (Enum-Name) | `Datum` (bei fehlender/ungültiger Zeile) | Persistierter Anzeigemodus der Seitenleisten-Aufgabenliste |
| `aktiveaufgaben.listenmodus.dashboard` (`AppEinstellungService.AktiveAufgabenListenModusDashboardKey`) | `AppEinstellung`-Zeile (Enum-Name) | `Datum` (bei fehlender/ungültiger Zeile) | Persistierter Anzeigemodus der Dashboard-Aufgabenliste |
| `AufgabenListenModusService` | DI-Registrierung (`AddSingleton`) | — | Persistenz-Service, `App.xaml.cs` |

Keine `appsettings`-Änderungen; `PluginSymbol` ist deklarativ im Plugin-Code.

## Seiteneffekte und Risiken

- **Geteilte `AktiveAufgabenListe`:** Seitenleiste und Dashboard binden dieselbe `ObservableCollection`. Die Gruppierung muss über eine instanz-eigene `ListCollectionView` laufen — `CollectionViewSource.GetDefaultView` würde den Modus einer Ansicht in die andere leaken. Die Modus-Zustände sind bewusst unabhängig voneinander schaltbar **und** getrennt persistiert (eigene Schlüssel).
- **Dashboard-Optik:** Template-Änderungen wirken automatisch auch im Dashboard (geteilte Templates) — Kacheln werden dort ebenfalls dreizeilig und vollflächig klickbar. Die Modus-Buttons werden dort ebenfalls angeboten (vom Anwender bestätigt).
- **Doppelte UIA-Anker:** Da das Dashboard dieselben Templates nutzt, existieren `AufgabeNavigieren:{Titel}`/`AufgabeStatus:{Titel}`/`AktiveAufgabeKiPlugin_{Titel}`/`OffeneTodos:{Titel}` sowie die **neuen** Anker `AufgabenGruppe:{Name}` (geteiltes `HeaderTemplate`) und die Modus-Buttons künftig in beiden Ansichten, sobald das Dashboard sichtbar ist. `WpfTestBase.WaitForElement` filtert `IsOffscreen` nicht — ein fensterweites `FindFirstDescendant` wäre mehrdeutig und könnte das jeweils andere Ansichtselement treffen (kritischer Fall: Klick auf einen `AufgabenModus:*`-Button schaltet den falschen Ansichts-Modus). **Festgelegte Strategie:** Template-Anker werden in den E2E-Helfern gescoped unterhalb der ansichtsspezifischen `AutomationId`s `AktiveAufgabenListeMenue`/`AktiveAufgabenListeDashboard` gesucht; die außerhalb des Controls liegenden Modus-Buttons tragen ansichtsspezifische Namen (`AufgabenModus:*` vs. `DashboardAufgabenModus:*`). `ClickInForeground` fällt auf das Invoke-Pattern zurück, falls kein klickbarer Punkt existiert.
- **Verschachtelter Button:** Der To-do-Button sitzt innerhalb des Kachel-Buttons. WPF leitet den Click des inneren Buttons nicht an den äußeren weiter; `OffeneTodosButton_Click` (`e.Handled = true`) bleibt als Absicherung — Verhalten ist im Dashboard bereits bewiesen.
- **`StatusUebergangsAnimation` wird aktiv:** Nach der Erweiterung auf `AktiveAufgabePanelItem` feuert die Animation in der Kachel erstmals tatsächlich — beabsichtigt, aber ein sichtbarer Verhaltensunterschied.
- **Pausiert-Countdown:** Der Kurztext "Pausiert (noch …)" ist in der Symbolzeile nicht mehr sichtbar; Volltext bleibt per ToolTip/`HelpText` erreichbar (E2E-Vertrag unverändert).
- **Moq-Mocks:** `Mock<IKiPlugin>`/`Mock<IGitPlugin>` liefern für das nicht aufgesetzte `PluginSymbol` `null` statt des Interface-Defaults — die Symbol-Auflösung muss `null`/leer auf den Fallback-Glyph mappen.
- **Eingeklappte Sidebar:** Bei 48 px Breite wären die Modus-Buttons unbrauchbar — sie werden an `IsNavigationExpanded` gebunden.
- **Binärkompatibilität:** Fremde, ältere Plugin-DLLs laden dank Default-Interface-Member weiter; der Plan ändert kein bestehendes Member.
- **Wiederhergestellter Gruppenmodus beim Start:** Startet die App mit persistiertem Modus `Repository`/`Ki`, zeigen beide Listen sofort Gruppenheader — für Anwender beabsichtigt; für E2E-Szenarien relevant: Alle E2E-Läufe starten mit frischer Test-DB (`LaunchApp(ensureDatabaseDeleted: true)` → kein gespeicherter Modus → Default `Datum`), nur innerhalb eines Szenarios nach explizitem Moduswechsel bzw. nach `RestartAppPreservingDatabase` sind Header vorhanden. Der konsolidierte E2E-Test setzt am Ende auf `Datum` zurück, damit Folgeschritte unverändert laufen.
- **Race Laden vs. Benutzerwechsel:** Der asynchrone Modus-Ladevorgang beim Start könnte eine inzwischen erfolgte Benutzerumschaltung überschreiben — wird durch das `_aufgabenListenModusGeaendert`-Flag unterbunden.
- **DbContext-Isolation der Persistenz:** Speichern/Laden läuft ausschließlich über `AufgabenListenModusService` mit eigenem DI-Scope pro Operation — kein gemeinsamer `DbContext` mit dem 5-s-Aufgaben-Refresh (verhindert EF-Core-Nebenläufigkeitsfehler). Ein scheiternder Persistenz-Zugriff ist nicht kritisch: `SafeFireAndForget` loggt ihn, der Live-Modus bleibt wirksam.

## Umsetzungsreihenfolge

1. **Plugin-Vertrag: `IPlugin.PluginSymbol` + Basisklassen + Plugin-Overrides**
   - Voraussetzungen: Keine.
   - Beschreibung: `PluginSymbol` als Default-Member an `IPlugin`; `virtual` an `GitPluginBase<TPlugin>` und `CliKiPluginBase`; konkrete Glyphen in allen acht Repo-Plugins.
2. **`AufgabenListenModus`-Enum**
   - Voraussetzungen: Keine.
   - Beschreibung: Enum `Datum`/`Repository`/`Ki` in `src/Softwareschmiede.App/ViewModels/`.
3. **Modus-Persistenz: `AppEinstellungService`-Schlüssel + `AufgabenListenModusService` + DI**
   - Voraussetzungen: Schritt 2 (Enum); `AppEinstellungService`, `DarkModeService`-Muster und `IServiceScopeFactory` sind vorhanden.
   - Beschreibung: Zwei Schlüssel-Konstanten auf `AppEinstellungService`; `AufgabenListenModusService` (Scope-Factory, Lade-/Speicher-Methoden je Ansicht, Enum↔String inkl. `Datum`-Fallback) in `src/Softwareschmiede.App/Services/`; `AddSingleton`-Registrierung + `MainWindowOptionaleDienste`-Factory-Erweiterung in `App.xaml.cs`; Record-Parameter am `MainWindowOptionaleDienste`.
4. **Statusklassifikation extrahieren + `KiAusfuehrungsStatusSymbolConverter`**
   - Voraussetzungen: `KiAusfuehrungsStatusConverter` (vorhanden).
   - Beschreibung: `KiAusfuehrungsZustand` + `KiAusfuehrungsStatusKlassifikation` aus dem bestehenden Converter extrahieren; `KiAusfuehrungsStatusConverter` darauf umstellen; Symbol-Converter ergänzen; in `App.xaml` registrieren.
5. **`AktiveAufgabePanelItem` erweitern**
   - Voraussetzungen: Schritt 1 (Symbolwerte fließen vom Plugin-Vertrag).
   - Beschreibung: Neue Properties (`RepositoryName`, `*PluginSymbol`, `*GruppenName`, `*GruppeIstFallback`, `SortierZeitpunkt`); `ProjektName` entfernen.
6. **`AufgabenListenAnsicht`-Hilfsklasse**
   - Voraussetzungen: Schritte 2 und 5.
   - Beschreibung: Statische Konfiguration der `ListCollectionView` je Modus inkl. Gruppen-Konstanten.
7. **`MainWindowViewModel` erweitern**
   - Voraussetzungen: Schritte 1, 2, 3, 5.
   - Beschreibung: `AufgabenListenModus`-Property inkl. Persistier-Setter + `_aufgabenListenModusGeaendert`-Flag; `GespeichertenAufgabenListenModusLadenAsync` + Konstruktor-Anstoß; `ResolvePlugin` (Name + Symbol); `MapAktiveAufgabePanelItem` befüllt alle neuen Felder inkl. Gruppen-Fallbacks.
8. **`DashboardViewModel.AufgabenListenModus`**
   - Voraussetzungen: Schritte 2, 3.
   - Beschreibung: Eigene setzbare Modus-Property mit Persistenz über `SpeichereDashboardModusAsync`; optionaler Konstruktorparameter für den Service; Ladevorgang aus `Initialize` angestoßen.
9. **`ActiveTasksListControl` umbauen**
   - Voraussetzungen: Schritte 2, 4, 6.
   - Beschreibung: XAML — dreizeiliges Inhaltstemplate, Kachel als `Button` mit `ControlTemplate`, `GroupStyle`/`HeaderTemplate`, alte Templates entfernen; Code-Behind — `ShowNavigationButton` entfernen, `ListenModus`-DP + eigene `ListCollectionView` über `ItemsSource`.
10. **`StatusUebergangsAnimation` erweitern**
    - Voraussetzungen: Schritt 9 (erst das neue Template nutzt die Animation sinnvoll).
    - Beschreibung: `OnStatusChanged` akzeptiert `AktiveAufgabePanelItem`.
11. **`MainWindow.xaml` und `DashboardView.xaml` anpassen**
    - Voraussetzungen: Schritte 7, 8, 9.
    - Beschreibung: Modus-RadioButtons in beiden Titelleisten, `ListenModus`-Bindings, `ShowNavigationButton`-Attribut entfernen.
12. **Unit-/Integrationstests schreiben/anpassen**
    - Voraussetzungen: Schritte 1–10.
    - Beschreibung: Symbol-Converter-Tests, `AufgabenListenAnsicht`-Tests, `AufgabenListenModusService`-Persistenztests (Roundtrip über getrennte Scopes/Kontexte, Fallbacks), Mapping-/Modus-/Wiederherstellungs-Tests in `MainWindowViewModelTests`, `DashboardViewModelTests`, `PluginSymbolTests` (siehe Tests-Abschnitt).
13. **E2E-Anpassungen und neuer E2E-Test**
    - Voraussetzungen: Schritte 9–11.
    - Beschreibung: `MenuView` und `DashboardView` (E2E-Hilfsklassen) anpassen/erweitern (Scoping über `AktiveAufgabenListeMenue`/`AktiveAufgabenListeDashboard`, Glyph-Leser inkl. TextPattern-Pfad, `KlickeOffeneTodosButton`, `DashboardAufgabenModus:*`-Klick), Kommentar/Anker-Verifikation in `E2E_AufgabePausieren`, neuer konsolidierter E2E-Test in `E2E_AufgabenListeModi.cs` (inkl. To-do-Button-Ohne-Navigation-Nachweis, Dashboard-Modusfluss und Persistenz-/Neustart-Nachweis beider Schlüssel via `OpenTestDbContext` + `RestartAppPreservingDatabase`), Einbindung in `MainTest.cs`.
14. **Verifikation: vollständiger Build + beide Test-Lanes**
    - Voraussetzungen: Schritte 1–13.
    - Beschreibung: `dotnet build Softwareschmiede.slnx -c Debug`; `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"` sowie `--filter "Category=OsInterface"` — jeweils synchron/foreground, niemals im Hintergrund (siehe CLAUDE.md).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `Convert_ShouldReturnSymbolOnly_*` | `KiAusfuehrungsStatusSymbolConverterTests` (neu, `src/Softwareschmiede.Tests/App/Converters/`) | Glyph-Ausgabe je Zustand (`⏸` Pausiert ohne Countdown, `⏳` ScheduledPrompt, `▶` Läuft, `⏸` Wartet/WartetAufEingabe, `✓` Bereit), `null`-Eingabe → `string.Empty`, `ConvertBack` → `NotSupportedException` |
| `Konfiguriere_ShouldApplyGroupingAndSorting_*` | `AufgabenListenAnsichtTests` (neu, `src/Softwareschmiede.Tests/App/Controls/` o. ä.) | Je Modus die erzeugten `SortDescriptions`/`GroupDescriptions` als Daten; alphabetische Gruppenreihenfolge, Fallback-Gruppe zuletzt, gruppenintern `SortierZeitpunkt` absteigend |
| `SpeichernUndLaden_*` / `Lade_*_ReturnsDatum_WhenMissingOrInvalid` / `SchluesselUnabhaengig_*` | `AufgabenListenModusServiceTests` (neu, `src/Softwareschmiede.Tests/ServiceIntegration/` — Muster `DarkModeServiceIntegrationTests`) | Roundtrip Speichern→Laden über getrennte DI-Scopes/einen zweiten DbContext (Neustart-Semantik) je Ansichtsschlüssel; fehlender/ungültiger Wert → `Datum`; Menü- und Dashboard-Schlüssel beeinflussen sich nicht. Testinfrastruktur: `ServiceCollection` mit `AddDbContext<SoftwareschmiededDbContext>` (InMemory mit festem DB-Namen) + `AddScoped<AppEinstellungService>` → `BuildServiceProvider` → `IServiceScopeFactory` in den Service. |
| `AktiveAufgabenAktualisierenAsync_ShouldMapRepositoryName*` / `ShouldMapPluginSymbols*` / `ShouldMapKiGruppenName*` / `ShouldMapRepositoryGruppenName*` / `ShouldMapSortierZeitpunkt*` | `MainWindowViewModelTests` | `RepositoryName` inkl. Projektfallback, Symbol-Auflösung über `IPluginManager`-Mock inkl. `null`-/Fallback-Glyph-Toleranz, `KiGruppenName` = PluginName/roher Prefix/"Nicht ausgeführt", `RepositoryGruppenName`/"Ohne Repository", `SortierZeitpunkt` = `LetzterCliStartUtc ?? ErstellungsDatum` |
| `AufgabenListenModus_ShouldDefaultToDatum*` / `ShouldBeSettable*` / `ShouldRestorePersistedModus*` / `ShouldPersistModusOnChange*` | `MainWindowViewModelTests`, `DashboardViewModelTests` | Default `Datum`, Setzen triggert `PropertyChanged`; mit injiziertem `AufgabenListenModusService` (Test-Provider, geteilter InMemory-DB-Name): Seeding `AppEinstellung`="Repository" → `await GespeichertenAufgabenListenModusLadenAsync()` → Property `Repository`; Setter → persistierter Wert im selben Store (Polling-Assert mit Deadline, etabliertes Testmuster); Restore setzt das Benutzer-Flag nicht und schreibt nicht zurück |
| `PluginSymbol_*` | `PluginSymbolTests` (neu, `src/Softwareschmiede.Tests/`) | Alle acht Repo-Plugins liefern ein nicht-leeres, vom Interface-Default abweichendes Symbol; Interface-Default greift bei einem Minimal-`IPlugin` |
| `SetzeAufgabenListenModus`, `WaitForAufgabenGruppe`/`GetAufgabenGruppenNamen`, `GetActiveTaskKiPluginSymbol`, `GetActiveTaskScmPluginSymbol`, `GetActiveTaskStatusGlyph`, `KlickeOffeneTodosButton` | `MenuView` (E2E-Hilfsklasse) | Modus-Klick über `AufgabenModus:*`-Anker; Polling auf `AufgabenGruppe:{name}`-Header — gescoped unterhalb `AktiveAufgabenListeMenue`; Glyph-Lesen des SCM-/KI-Symbols (UIA-`Name`); Status-Glyph über das **TextPattern** des `AufgabeStatus:{Titel}`-Elements (der UIA-`Name` ist dort der Anker, nicht der Glyph); Klick auf `OffeneTodos:{Titel}` |
| `SetzeAufgabenListenModus`, `WaitForAufgabenGruppe`/`GetAufgabenGruppenNamen` | `DashboardView` (E2E-Hilfsklasse) | Modus-Klick über `DashboardAufgabenModus:*`-Anker; Polling auf `AufgabenGruppe:{name}`-Header — gescoped unterhalb `AktiveAufgabenListeDashboard` |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `KiAusfuehrungsStatusConverterTests` / `…_ZeitgesteuerterPrompt` | Keine Anpassung nötig — dienen als Regressionssicherung, dass die Extraktion der Statusklassifikation den Volltext-Vertrag nicht verändert. |
| `MainWindowViewModelTests` (Plugin-Mocks) | `Mock<IGitPlugin>`/`Mock<IKiPlugin>` liefern für `PluginSymbol` `null` — neue Mapping-Tests setzen das Member explizit; die Symbol-Auflösung muss `null` tolerieren, damit die bestehenden Tests ohne Setup weiterlaufen. |
| `MainWindowViewModelTests` / `MainWindowViewModelTests_UpdateStartup` / `…_UpdateSettingsReadFailure` / `…_ZeitgesteuerterPrompt` | `MainWindowOptionaleDienste` erhält einen optionalen Record-Parameter — bestehende Konstruktionen bleiben ohne Anpassung gültig; ohne injizierten `AufgabenListenModusService` ist das VM-Verhalten unverändert (reiner Laufzeitzustand). |
| `DashboardViewModelTests` | Neuer optionaler Konstruktorparameter — `CreateSut` bleibt kompilierfähig; neue Persistenztests übergeben den Service explizit. |
| `MenuView.GetActiveTaskKiPluginName` | Liest `HelpText` ("KI: {Name}") statt `Name` (künftig der Glyph); Aufrufer `E2E_PluginAuswahlUndWechsel` bleiben unverändert. |
| `E2E_AufgabePausieren` (Z. 57–59) | Kommentar verweist auf den "→"-Button/`ShowNavigationButton` — Kommentar anpassen; der Anker `AufgabeNavigieren:{Titel}` selbst bleibt funktional. |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Kompakte Kachel: Symbolzeile (SCM-/KI-/Status-Glyph sichtbar, To-do-Button erhalten), Titel + Repository-Untertitel | `E2E_AufgabenListeModi.cs` → `AufgabenListe_KompakteKachelSymboleUndModuswechsel_E2E` (neu, in `End2EndTest`) | Drei-zeilige Kachel mit Symbolen statt Textzeilen | Nur ein echter FlaUI-Lauf beweist, dass die gerenderten TextBlocks/Glyphen und UIA-Eigenschaften in der laufenden App stimmen. Lesepfade: SCM-/KI-Glyph als `Name` der `AktiveAufgabe*Plugin_{Titel}`-Elemente, Status-Glyph über das TextPattern des `AufgabeStatus:{Titel}`-Elements (nicht `element.Name` — das ist der Anker). |
| Pflicht | Kachelklick navigiert zur Aufgabendetailansicht (ohne separaten Pfeil-Button) | dto. | Gesamte Kachel klickbar | Der `Button`-ControlTemplate-Umbau muss in der echten App den `NavigateZuAufgabe`-Pfad auslösen. |
| Pflicht | To-do-Button im Kachel-Button: Klick auf `OffeneTodos:{Titel}` öffnet den "Offene Todos"-Dialog (`OpenTodosDialogView`, Desktop-Fenster) **ohne** die Kachel-Navigation auszulösen (`TaskDetailView.IsVisible` bleibt `false`); Dialog danach per `ForceClose` schließen | dto. | To-do-Button bleibt eigenständig klickbar (Anforderung) | Der Containment-Wechsel `MouseBinding`-Border → echter `Button` ist die Risikostelle für das Click-Routing; nur ein echter Klick in der laufenden App beweist, dass der verschachtelte Button den Dialog öffnet und die äußere Kachel-Navigation nicht auslöst — `OffeneTodosAnzeigenCommand_ShouldOpenDialogForSelectedTask` deckt nur die Command-Ebene ab. |
| Pflicht | Moduswechsel Seitenleiste: "Repository" zeigt Gruppenheader (inkl. "Ohne Repository"), "KI" zeigt Gruppenheader (inkl. "Nicht ausgeführt"), "Datum" zeigt wieder die flache Liste — alle Suchen gescoped unter `AktiveAufgabenListeMenue` | dto. | Umschaltbare Anzeigemodi inkl. Fallback-Gruppen | Gruppierung/`ListCollectionView`/`GroupStyle` sind nur in der laufenden WPF-Anwendung verifizierbar. |
| Pflicht | Moduswechsel Dashboard: `NavigateToDashboard` → `DashboardAufgabenModus:Repository` klicken → `AufgabenGruppe:*`-Header gescoped unter `AktiveAufgabenListeDashboard` sichtbar; danach `aktiveaufgaben.listenmodus.dashboard` per `OpenTestDbContext` = "Repository" und `aktiveaufgaben.listenmodus` unverändert (Schlüssel-Unabhängigkeit) | dto. | Anwender-Vorgabe "Modus-Buttons auch im Dashboard" inkl. eigenem Persistenzschlüssel | Eigenständiger Benutzerfluss mit eigener `ListCollectionView`-Instanz, eigener Bindung (`DashboardViewModel.AufgabenListenModus`) und eigenem `AppEinstellung`-Schlüssel — Unit-Tests auf VM/Service-Ebene ersetzen den Nachweis des UI-Datenflusses nicht. |
| Pflicht | Persistenz + Wiederherstellung beider Ansichten: nach den Moduswechseln stehen `aktiveaufgaben.listenmodus` (Menü) **und** `aktiveaufgaben.listenmodus.dashboard` (Dashboard) mit dem Enum-Namen in der Test-DB (Assertion via `OpenTestDbContext`); nach `RestartAppPreservingDatabase` erscheinen die Seitenleisten-Gruppenheader ohne erneuten Modusklick, und nach `NavigateToDashboard` — der `DashboardViewModel.Initialize`-Pfad, der den Dashboard-Modus erst dann lädt — auch die Dashboard-Gruppenheader ohne erneuten Modusklick; Abschluss mit Rücksetzen beider Modi auf "Datum" | dto. (am Ende desselben Testmethoden-Szenarios) | Persistenz über App-Neustarts (geforderte Funktion), je Ansicht | Der Schreibpfad RadioButton → `AppEinstellung` und die Lesepfade Start → Gruppenheader sind nur mit echter App + echter SQLite-DB nachweisbar; `RestartAppPreservingDatabase` ist die vorhandene Infrastruktur für Neustart-E2E. Der Dashboard-Restore-Pfad (`Initialize` beim ersten `NavigateToDashboard`) unterscheidet sich vom Seitenleisten-Pfad (Konstruktor) und braucht den eigenen Schritt. |

Ein einzelnes, konsolidiertes Testmethoden-Szenario deckt alle sechs Pflicht-Szenarien gegen weitgehend dieselbe App-Instanz ab (CLAUDE.md-Konsolidierungsregel — keine neue Testmethode, nur Erweiterung des geplanten Laufs): Aufgaben werden — wie in `E2E_AufgabePausieren` — über die UI angelegt und per Test-DB (`OpenTestDbContext`) auf `Gestartet`/`Aktiv` mit verschiedenen `KiPluginPrefix`-/Repository-Konstellationen gesetzt (ConPTY-frei, damit der Test in dieser Sandbox läuft). Reihenfolge im Szenario: Kachel-/Symbolprüfung und To-do-Button-Klick (Dialog-Ohne-Navigation-Nachweis) → Kachelklick-Navigation → Seitenleisten-Moduswechsel → Dashboard-Moduswechsel inkl. DB-Assertion beider Schlüssel → `RestartAppPreservingDatabase` → Wiederherstellungs-Nachweis beider Ansichten (Seitenleiste direkt, Dashboard nach `NavigateToDashboard`) → Rücksetzen beider Modi auf `Datum`. Der Neustart-Schritt nutzt `RestartAppPreservingDatabase` (existierender Helper, schließt den testeigenen Prozess regulär und startet mit derselben DB neu — kein Kill, keine Fremdprozesse). Aufzurufende Szenariomethode innerhalb des bestehenden App-Lifecycles von `General_RepositoryUndAufgaben_E2E` in `MainTest.cs` einhängen (kein neuer App-Start im Discovery-Sinn; die Relaunch innerhalb des Szenarios bleibt die einzige zusätzliche App-Instanz).

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `MenuView` (E2E-Hilfsklasse) | `GetActiveTaskKiPluginName` liest `HelpText`; neue Modus-/Gruppen-/Glyph-/To-do-Button-Helfer kommen hinzu (alle Suchen gescoped unter `AktiveAufgabenListeMenue`). `NavigateToTask` und `WaitForTaskStatus` bleiben durch die beibehaltenen Anker (`AufgabeNavigieren:{Titel}` als echter Button, `AufgabeStatus:{Titel}`-HelpText-Volltext) code-unverändert — Verifikation im E2E-Lauf. |
| `DashboardView` (E2E-Hilfsklasse) | Neue Modus-/Gruppen-Helfer für den Dashboard-Benutzerfluss (`DashboardAufgabenModus:*`, Gruppenheader gescoped unter `AktiveAufgabenListeDashboard`). |
| `E2E_AufgabePausieren` | Nur Kommentar-Update (Pfeil-Button/`ShowNavigationButton`-Verweis); Anker und `HelpText`-Prädikate bleiben gültig. |
| `E2E_TaskWechselUeberMenue`, `E2E_PluginAuswahlUndWechsel`, `E2E_ArbeitsstatusAktualisierung` | Keine Code-Anpassung — laufen über die beibehaltenen Anker bzw. die angepasste `MenuView`-Hilfsmethode weiter; als Regression in der OsInterface-Lane mitlaufen lassen. |

## Offene Punkte

Keine.

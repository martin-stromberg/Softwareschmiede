# Umsetzungsplan: Prereleases bei Updates

## Uebersicht

Die bestehende WPF-Updatefunktion erhaelt persistente Einstellungen fuer Update-Modus und Prereleases. Releaseauswahl, lokale und entfernte Versionsnormalisierung sowie manuelle Befehle und einmalige Startautomatik verwenden dieselben gespeicherten Regeln. Reale FlaUI-Starts pruefen die Wirkung bis zur kontrollierten Updater-Prozessgrenze.

Grundlage: [Anforderung](requirement.md), [Bestandsaufnahme](inventory.md) mit [Einstellungen](inventory/settings.md), [Updatepipeline](inventory/update-pipeline.md), [Startfluss](inventory/startup-flow.md), [Tests](inventory/tests.md) sowie P-01 bis P-04 und T-01 bis T-08 aus [plan-check.1.md](plan-check.1.md), laut aktuellem [plan-check.md](plan-check.md) bereits adressiert. Diese Nachplanung ergaenzt gezielt T-09 (AK 7). Der lokale `/plan`-Workflow wurde direkt ausgefuehrt, weil keine delegierbaren Unteragenten verfuegbar sind. Implementierung, Testausfuehrung und erneuter `/plan-check` stehen noch aus.

## Designentscheidungen

| Komponente / Bereich | Gewaehlter Ansatz | Begruendung |
|---|---|---|
| Persistenz | `UpdateMode` mit festen Werten `Aus = 0`, `NurPruefen = 1`, `BeiProgrammstartPruefenUndAusfuehren = 2`; zwei neue Eintraege in der bestehenden Key-Value-Tabelle | UI-Labels sind keine Steuerwerte; keine Schemaaenderung. |
| Defaults | `Nur Pruefen`, Prereleases `false`, auch bei fehlenden/ungueltigen gespeicherten Werten | Bestehende Pruefung bleibt ohne automatische Installation erhalten. |
| Reichweite von `Aus` (P-01) | Sperrt Start-/Hintergrundpruefung, manuellen Pruefbefehl und erneute Pruefung beim manuellen Installationsstart, einschliesslich direkter Command-Aufrufe | AK 2 nennt keine Ausnahme. |
| Andere Modi | `Nur Pruefen` erlaubt eine ausdrueckliche Installation. Der Startmodus installiert ausschliesslich beim einmaligen Programmstart automatisch; spaetere manuelle Pruefungen bieten lediglich an. | Automatik bleibt auf den geforderten Zeitpunkt begrenzt. |
| Optionsweitergabe | Unveraenderliche `UpdateSettings` und `UpdateCheckOptions`; Aufrufer lesen Einstellungen in kurzlebigem DI-Scope, der Singleton-Updateservice erhaelt nur Optionen | Keine scoped-to-singleton-Injektion oder parallel verwendeten DbContexts. |
| Aktualitaet | Gespeicherte Aenderungen invalidieren das Angebot und canceln einen noch nicht uebergebenen Updateablauf; ViewModel vergleicht dessen Einstellungsgeneration | Ein alter RC-Fund darf die neue Auswahl nicht umgehen (T-03). |
| Settings-Lesefehler (T-09) | Fehlgeschlagenes `GetUpdateSettingsAsync` beendet den aktuellen Versuch an jeder Lesegrenze mit sichtbarem Nicht-pruefbar-/Fehlerzustand; keine Ersatzfreigabe durch Defaults, Snapshot oder Cache | Defaults gelten nur fuer erfolgreich gelesene fehlende/ungueltige Werte. Ohne erfolgreichen Aktualitaetsnachweis darf kein Updater starten. |
| Releasequelle | Fuer beide Kanaele dieselbe paginierte Releases-Liste; hoechste zulaessige SemVer ueber alle Seiten | Filterung und Pagination sind einheitlich und unabhaengig von Listenreihenfolge/Datum pruefbar (T-06). |
| Version (P-03) | Bestehenden Comparer um SemVer-Value-Object erweitern; `Normalize` erhaelt Prerelease-Suffix und Build-Metadaten | `System.Version` allein bildet Prerelease-Rangfolgen nicht ab; beide Quellen muessen die Vergleichsinformation behalten. |
| Startzeitpunkt (P-02) | Einmaliger `MainWindow.ContentRendered`-Handler nach DB-Initialisierung und expliziter Zuweisung von `Application.MainWindow`; kein Updateaufruf im ViewModel-Konstruktor | Hauptfenster, Dispatcher und Dialog-Owner stehen auch bei sofortiger Antwort bereit. |
| Installationspfad | Gemeinsamer manueller/automatischer Ablauf mit einem nicht wartenden Gate ueber Pruefung, CLI-Sicherheit, Vorbereitung und Start | Verhindert Doppelinstallation und bewahrt Sicherheitsdialog, Fortschritt, Abbruch und Shutdown-Reihenfolge. |
| E2E-Grenzen (P-04) | Echter App-Prozess, echte Release-/Paket-/Skriptservices; kontrollierter HTTP-Transport und aufzeichnende Prozess-/Shutdown-Adapter | JSON-Auswahl, Download, Entpacken, Validierung und Skripterzeugung werden ausgefuehrt, ohne eine Installation zu veraendern. |

## Programmablaeufe

### Einstellungen laden, speichern und wirksam machen

1. `SettingsViewModel.LadenAsync` liest beide Werte mit `AppEinstellungService.GetUpdateSettingsAsync`. Die ComboBox zeigt exakt `Aus`, `Nur Pruefen`, `Bei Programmstart pruefen und ausfuehren`; die Checkbox zeigt `Prerelease-Versionen laden`.
2. `SpeichernAsync` validiert den Enum-Wert und schreibt beide Updatewerte mit `SetUpdateSettingsAsync` gemeinsam in einem DB-Speichervorgang. Direkt nach erfolgreichem Speichern dieses Wertepaars wird `UpdateSettingsSaved` mit dem gespeicherten Snapshot ausgeloest, auch wenn ein spaeterer anderer Einstellungsschritt fehlschlaegt. Die uebrigen Speicherablaeufe bleiben erhalten.
3. `MainWindowViewModel.NavigateToSettings` verbindet das Event einmal mit dem gecachten `SettingsViewModel`; `Dispose` entfernt den Handler. Ungespeicherte Aenderungen und `VerwerfenAsync` loesen kein Event aus.
4. Der Handler aktualisiert auf dem UI-Dispatcher den Modus, erhoeht bei geaenderten Updatewerten die Generation, leert `VerfuegbaresUpdate`/`UpdateVerfuegbar`, invalidiert das zugeordnete Pruefergebnis und cancelt einen laufenden Updateablauf. Beide Commands melden `CanExecuteChanged`. Speichern startet weder eine neue Pruefung noch eine automatische Installation.
5. Bei `Aus` bleibt der Pruefbutton sichtbar, aber deaktiviert; der Installationsbutton/das Angebot ist ausgeblendet. Bei anderen Modi ist Pruefen nach Initialisierung und ohne laufenden Vorgang aktiv; Installieren setzt zusaetzlich einen aktuellen Fund voraus. Vor dem Laden der Einstellungen sind beide Commands gesperrt.
6. Vor jeder Pruefung und jedem Installationsstart werden die gespeicherten Werte in einem frischen Scope gelesen. DB-Lesefehler ergeben fuer diesen Versuch einen sichtbaren Nicht-pruefbar-Zustand ohne Installation; sie aktivieren keine Automatik.

Beteiligte Komponenten: `AppEinstellungService`, `SettingsViewModel`, `SettingsView.xaml`, `MainWindowViewModel`, `MainWindow.xaml`.

### Einmaliger Start mit bereiten Dialogen

1. `App.StartupAsync` beendet DB-Migration und bestehende Startinitialisierung, erzeugt das Hauptfenster, weist es explizit `Application.MainWindow` zu und zeigt es an.
2. `MainWindow` registriert vor `Show` den `ContentRendered`-Handler. Dieser meldet sich beim ersten Aufruf ab und erwartet `MainWindowViewModel.InitializeUpdatesAfterWindowReadyAsync(ct)` auf dem Dispatcher; Exceptions werden behandelt/protokolliert.
3. Das ViewModel setzt sein einmaliges Startkennzeichen vor dem ersten `await`, liest die Einstellungen und verwendet das gemeinsame Update-Gate. Erneutes Rendern, Navigation, Moduswechsel oder Methodenaufruf setzen das Kennzeichen nicht zurueck. Es gibt keine wartende zweite Startinstallation.
4. `Aus`: kein Releaseabruf. `Nur Pruefen`: einmal pruefen und neues Update sichtbar anbieten, keine Sicherheitsabfrage, kein Download/Updater/Shutdown. Startmodus: einmal pruefen und bei neuer Version ohne Updateklick den gemeinsamen Installationspfad fortsetzen.
5. Auch ein synchron geliefertes Ergebnis muss Sicherheits-/Fortschrittsdialoge mit dem bereits sichtbaren Hauptfenster als Owner oeffnen koennen. Die Konstruktor-Hintergrundpruefung entfaellt. Fensterikon und Versionsanzeige duerfen den lokalen Provider weiterhin verwenden.
6. Kein Update, nicht pruefbarer Zustand, Sicherheitsablehnung, Abbruch und Fehler beenden den Startversuch ohne automatische Wiederholung. Die App bleibt bedienbar; manuelle Aktionen sind bei aktiviertem Modus wieder moeglich. Beim Schliessen wird ein laufender Vorgang gecancelt.

### Releaseauswahl und lokale/entfernte Versionskette

1. `UpdateService.CheckForUpdateAsync(UpdateCheckOptions, ct)` verwendet sein Pruef-Semaphore und liest `ApplicationVersionProvider.GetInstalledVersionAsync`. Fehlende/ungueltige lokale Version liefert `NichtPruefbar` ohne Releaseabruf.
2. `GitHubReleaseClient.GetLatestReleaseAsync(UpdateCheckOptions, ct)` ruft `/repos/{owner}/{repo}/releases?per_page=100` ab und folgt `Link` mit `rel=next` bis zum Ende. Folge-URLs muessen zur selben GitHub-API und zum selben Repository-Releases-Pfad gehoeren; wiederholte URLs/ungueltige Pagination sind Fehler. Ein gemeinsames Timeout begrenzt alle Seiten.
3. Seiten werden als JSON-Listen geparst. Drafts, ungueltige Tags und Eintraege ohne gueltiges `release.zip`-Asset werden uebersprungen. Ein einzelner unbrauchbarer Eintrag verhindert nicht andere Kandidaten. `draft` wird im DTO ergaenzt; Download-URLs muessen absolut und HTTP(S) sein.
4. Prerelease bedeutet GitHub-Flag `prerelease` ODER SemVer-Prerelease-Suffix. Bei `IncludePrereleases = false` werden beide ausgeschlossen; bei `true` nehmen Stable und RC gemeinsam am Vergleich teil. `UpdateInfo.IsPrerelease` fuehrt diese Klassifikation verbindlich mit, auch bei GitHub-markiertem Tag ohne Suffix.
5. Die hoechste zulaessige SemVer aller Seiten gewinnt, unabhaengig von Reihenfolge/Datum; bei gleicher Praezedenz bleibt die erste gueltige Fundstelle. HTTP-/JSON-/Timeoutfehler auf irgendeiner Seite ergeben keinen Teiltreffer, sondern `null` und damit `NichtPruefbar`; ebenso eine vollstaendige Liste ohne zulaessigen Kandidaten. Caller-Cancellation propagiert als `OperationCanceledException`.
6. `TryParse` liefert kuenftig `SemanticUpdateVersion` statt `System.Version`. `Normalize` entfernt Leerzeichen am Rand und fuehrendes `v`/`V`, erhaelt `-rc.1` und `+build.7`. Metadaten beeinflussen die Rangfolge nicht. Alle Aufrufer und Dokumentationsvertraege werden abgeglichen.
7. Der lokale Provider liest beispielsweise `v1.3.0-rc.1+build.7` als `1.3.0-rc.1+build.7`; der Releaseclient verwendet denselben Vertrag fuer `UpdateInfo.Version`. Nach installierter `rc.1` sind `rc.2` und Stable `1.3.0` neuer. Suffixe bleiben auch in Anzeigen und Paketpfaden erhalten.
8. Ein letztes Service-Pruefergebnis ist keine Installationsfreigabe. `CachedResult` ist derzeit nur ein letzter Wert: nach Verbrauchersuche entfernen, falls unbenutzt; andernfalls mit seinen Optionen speichern und bei Einstellungswechsel invalidieren. Keine Wiederverwendung zwischen unterschiedlichen Optionen.

### Manuelle Pruefung und gemeinsamer Installationspfad

1. Beide Commands und Startautomatik erwerben dasselbe nicht wartende ViewModel-Gate. Weitere Eintritte waehrend eines laufenden Vorgangs werden verworfen; der Methodenrumpf prueft Modus/Status auch bei umgangenem `CanExecute`.
2. Der Vorgang liest aktuelle DB-Werte; bei `Aus` endet er vor `CheckForUpdateAsync`, CLI-Sicherheit und Vorbereitung. Manuelle Pruefung bietet auch im Startmodus lediglich an.
3. Manueller Installationsstart prueft erneut mit aktuellen Optionen; ein altes Angebot wird nicht als Installationsargument benutzt. Die Automatik verwendet ihren unmittelbar zuvor ermittelten Fund nur bei unveraenderter Einstellungsgeneration. Der gemeinsame Installationskern erhaelt Fund und Snapshot.
4. Nach asynchronen Abschnitten werden Token und Generation geprueft; vor Vorbereitung und unmittelbar vor Updaterstart werden auch die gespeicherten Werte erneut gelesen/abgeglichen. Aenderungen beenden den Versuch ohne Installation. Verspaetete Antworten duerfen weder Angebote wiederherstellen noch Folgeschritte starten.
5. Nur ein aktueller neuer Fund erreicht `ICliUpdateSafetyService.CheckAsync` und gegebenenfalls den vorhandenen Bestaetigungsdialog. Ablehnung verhindert Fortschrittsdialog, Download und Start. Die Automatik umgeht diese Sicherheitsentscheidung nicht.
6. `WpfUpdateProgressDialogService.Show` zeigt den echten Fortschritt; `PrepareUpdateAsync` fuehrt Download, Entpacken, Validierung und Skripterzeugung aus. Abbrechen cancelt den verknuepften Token; der Dialog zeigt den Abbruchzustand und bleibt schliessbar.
7. Nach erfolgreicher Vorbereitung und letzter Aktualitaets-/Abbruchpruefung folgen `MarkUpdaterStarting` und `StartPreparedUpdateAsync`. Ausschliesslich `UpdateService` ruft nach erfolgreichem `StartScriptAsync` den Shutdown-Service auf; kein zweiter Shutdown im ViewModel.
8. Der tatsaechliche synchrone Prozessstart ist die Uebergabegrenze. Bis dahin verhindern gespeicherte Aenderungen den Start, danach wird der geordnete Shutdown abgeschlossen. Vorbereitungsfehler, Startfehler oder Abbruch erzeugen keinen Shutdown. Gate und Busy-Zustaende werden immer freigegeben; Fehler sind im Dialog bzw. `UpdateHinweis` sichtbar.

### Nicht lesbare Update-Einstellungen (T-09)

1. `GetUpdateSettingsAsync` propagiert DB-Leseexceptions. Der gemeinsame ViewModel-Ablauf behandelt sie fuer Startautomatik, manuellen Pruefbefehl und manuellen Installationsstart. Ein fehlgeschlagener Zugriff ist kein erfolgreich gelesener fehlender Schluessel; auch ein zuvor gespeicherter/geladener Snapshot darf den Fehler nicht verdecken.
2. Beim ersten Lesen des jeweiligen Versuchs: `UpdateHinweis` zeigt sichtbar und automatisierbar, dass die Update-Einstellungen nicht gelesen werden konnten und die Pruefung nicht moeglich ist. Ein altes Angebot wird ungueltig; null `CheckForUpdateAsync`-/Releaseabrufe, Sicherheitsdialoge, Vorbereitung, Assetabrufe, Updaterstarts oder Shutdown-Aufrufe.
3. Beim erneuten Lesen unmittelbar vor `PrepareUpdateAsync`: erste Settings-Abfrage und Releasepruefung waren erfolgreich. Der Lesefehler beendet den Versuch vor Vorbereitung/Assetabruf und verhindert Updaterstart/Shutdown. Beim letzten Lesen nach erfolgreichem `PrepareUpdateAsync`: Paket und Skript duerfen bereits vorliegen; `MarkUpdaterStarting`, `StartPreparedUpdateAsync`, Launcher und Shutdown duerfen nicht mehr aufgerufen werden. Kein erneuter Versuch mit bereits geladenen Werten.
4. In beiden spaeten Faellen wird das Angebot invalidiert und der Fehler im `UpdateHinweis` sowie im bereits offenen Fortschrittsdialog angezeigt; der Dialog bleibt schliessbar. Jeder Fehlerpfad gibt Gate und Busy im Abschluss frei und veroeffentlicht den Versuchabschluss erst danach. Die Startautomatik wiederholt sich nicht.
5. Nach Freigabe des Fixture-Fehlers kann der Benutzer die Einstellungen erneut erfolgreich laden/speichern und manuell pruefen. Auch wenn das erste Lesen beim Start fehlschlug und beide Updatecommands zunaechst gesperrt bleiben, erlaubt dieser Settings-UI-Weg die Wiederherstellung ohne App-Neustart. Erst erfolgreich gelesene/gespeicherte Werte geben die modusgemaessen Commands frei. Ein danach tatsaechlich durchlaufender manueller Pruefversuch beweist die Gate-Freigabe; er installiert auch im Startmodus nicht automatisch.

## Neue Klassen

Namen sind Zielnamen fuer die Implementierung.

| Klasse | Typ / Ort | Zweck |
|---|---|---|
| `UpdateMode` | Enum, `Application/Services/Updates/UpdateModels.cs` | Drei feste Speicherwerte. |
| `UpdateSettings` | Unveraenderliches Record, `UpdateModels.cs` | Gemeinsam geladener Modus und Prerelease-Wert. |
| `UpdateCheckOptions` | Unveraenderliches Record, `UpdateModels.cs` | Optionsvertrag fuer Service und Releaseclient. |
| `SemanticUpdateVersion` | Value Object neben `UpdateVersionComparer.cs` | Kernversion, Prerelease-Identifier, Metadaten und Praezedenz; keine neue NuGet-Abhaengigkeit. |
| `UpdateE2ETestConfiguration` | Testmodell, `Softwareschmiede.App/Services/Testing/` | Szenariodatei, Testwurzel, Antwort- und Prozesskonfiguration. |
| `UpdateFixtureHttpMessageHandler` | Testadapter, gleicher Ordner | JSON-Seiten/ZIP-Streams, Fehler und kontrollierte Freigaben im echten App-Prozess. |
| `RecordingUpdateProcessLauncher` | Testadapter fuer `IUpdateProcessLauncher`, gleicher Ordner | Startargumente protokollieren, steuerbarer Erfolg/Fehler, kein Prozessstart. |
| `RecordingApplicationShutdownService` | Testadapter fuer `IApplicationShutdownService`, gleicher Ordner | Shutdown-Aufruf protokollieren und Testfenster fuer Assertions offen lassen. |
| `FixtureCliUpdateSafetyService` | Testadapter fuer `ICliUpdateSafetyService`, gleicher Ordner | Kontrollierte riskante Aufgaben fuer echte Bestaetigungsdialoge ohne ConPTY. |
| `UpdateSettingsReadFailureInterceptor` | Testadapter auf Basis von EF Core `DbCommandInterceptor`, `Softwareschmiede.App/Services/Testing/` | Nur markierte Settings-Leseabfragen gezielt fehlschlagen lassen; deaktivierbar, mit Ereignissen und begrenzten Freigaben (T-09). |
| `UpdateE2EFixture` | Testhelfer, `Softwareschmiede.Tests/E2E/` | Isolierte Version/DB/JSON/ZIP, Freigaben und strukturiertes Aufrufprotokoll. |

## Aenderungen an bestehenden Klassen

### Einstellungen und UI

- `AppEinstellungService`: Konstanten `UpdateModeKey`, `IncludePrereleasesKey`; `GetUpdateSettingsAsync(ct)` liest beide Werte mit Defaults in einem DB-Lesevorgang, mit festem EF-Abfragetag `UpdateSettings.Read` fuer gezielte Testinterception. DB-Leseexceptions propagieren ohne Default-Rueckgabe (T-09). `SetUpdateSettingsAsync(UpdateSettings, ct)` validiert und schreibt beide gemeinsam mit einem `SaveChangesAsync`. Kein paralleler DbContext-Zugriff.
- `SettingsViewModel`: `SelectedUpdateMode`, `IncludePrereleases`, feste Auswahlliste; Laden, Speichern, Verwerfen erweitern; neues `UpdateSettingsSaved`-Event mit gespeichertem Snapshot.
- `SettingsView.xaml`: ComboBox/CheckBox im allgemeinen Tab mit Automation-Namen `Update-Modus` und `Prerelease-Versionen laden`; bestehende WPF-Styles, langer Auswahltext auch bei minimaler Fenstergroesse lesbar.

### Updatevertraege und Verbraucher

- `UpdateInterfaces.cs`: explizite Signaturen `CheckForUpdateAsync(UpdateCheckOptions options, CancellationToken ct = default)` und `GetLatestReleaseAsync(UpdateCheckOptions options, CancellationToken ct = default)`. Alle repo-internen Aufrufer/Mocks umstellen; keine ungenutzten Legacy-Adapter.
- `UpdateModels.cs`: neue Einstellungs-/Optionstypen und `UpdateInfo.IsPrerelease`; Konstruktorverwendungen anpassen.
- `UpdateService`: Optionen weiterreichen, ausgeschlossene Prereleases auch bei fehlerhaftem Client-Ergebnis nicht anbieten; Cache wie oben bereinigen; Semaphore und Skriptstart-vor-Shutdown erhalten.
- `UpdateVersionComparer`: `TryParse`, `Normalize`, `IsNewer` und Dokumentation auf SemVer-Vertrag umstellen. Numerische Identifier numerisch vergleichen, numerisch kleiner als nichtnumerisch, nichtnumerisch ordinal/case-sensitive; kuerzere identische Identifierfolge kleiner als deren Verlaengerung; Stable hoeher als eigene Prereleases.
- `ApplicationVersionProvider` unter `src/Softwareschmiede/Application/Services/Updates/`: explizit betroffener Normalisierungsverbraucher. API-Verwendung/Dokumentation soweit notwendig anpassen; Quelle bleibt `version.json`. Tests sind auch dann Pflicht, wenn kein Provider-Code geaendert werden muss.
- `GitHubReleaseClient`: Pagination, Draft-DTO-Feld, einheitlicher Filter/Maximumvergleich, vollstaendiges Fehler-/Cancellation-Verhalten implementieren.

### Hauptfenster, Dialoge und Registrierung

- `MainWindowViewModel`: Konstruktorpruefung entfernen; `InitializeUpdatesAfterWindowReadyAsync`, frisches Lesen, gemeinsamer Installationskern, Gate, Startkennzeichen, Generation/CTS und Einstellungs-Eventhandler ergaenzen. Pruef-/Startmethoden, `ApplyUpdateCheckResult`, Command-Praedikate und `Dispose` anpassen.
- `MainWindow.xaml.cs`: einmaliger `ContentRendered`-Handler mit beobachteter async-Ausfuehrung. `App.xaml.cs`: Hauptfenster vor `Show` zuweisen, Einstellungsscopes und explizite E2E-Registrierung vor Serviceaufloesung.
- `MainWindow.xaml`: bestehende Updatebuttons, deaktivierte/verborgene Zustaende, Angebotsversion im Tooltip und sichtbaren automatisierbaren `UpdateHinweis` fuer Nicht-pruefbar-/Fehlerzustaende anbinden.
- `WpfUpdateProgressDialogService`, `UpdateProgressDialog`: echten Owner sowie vorhandene Abbruch-/Fehler-/Schliessen-Zustaende wiederverwenden; nur fehlende Automation-Merkmale fuer Phase, Fortschritt und Aktionen ergaenzen.
- `UpdatePackageService`, `UpdateScriptService`: bestehenden produktiven Ablauf und explizite Basispfad-/Launcher-Grenzen verwenden; nur notwendige API-Folgeanpassungen. Beide Klassen sind explizite Regressionstestziele.

## Datenbankmigrationen

Keine. `AppEinstellungen` bleibt eine Key-Value-Tabelle; fehlende Schluessel werden beim Speichern angelegt. Ein Persistenztest verwendet echte SQLite-DB und neue Scopes/Prozesse.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---|---|---|
| Modus | Nur 0, 1, 2 schreibbar | Ungueltige Eingabe nicht speichern; unbekannter DB-Wert wird als `NurPruefen` gelesen. |
| Checkboxwert | Boolean, fehlend/ungueltig entspricht `false` | Kein implizites Aktivieren. |
| SemVer | Vollstaendiges `X.Y.Z` mit optionalen gueltigen Prerelease-/Metadaten-Identifiern; keine leeren Identifier oder fuehrenden Nullen in numerischen Prerelease-Identifiern | `TryParse = false`, `Normalize` wirft `FormatException`; lokale Quelle nicht pruefbar, entfernter Kandidat verworfen. |
| Release | Kein Draft, erlaubter Kanal, gueltiger Tag, passendes Asset mit absoluter HTTP(S)-URL | Kandidat ueberspringen. |
| Pagination | Unbesuchte Folge-URL im selben API-/Repository-Pfad, Gesamt-Timeout | Gesamtabfrage nicht pruefbar, kein Teiltreffer. |
| Laufendes Ergebnis | Generation/Snapshot aktuell, Modus erlaubt, Token nicht gecancelt | Ergebnis verwerfen, keine weiteren Installationsschritte. |
| E2E-Konfiguration | Test-DB plus explizite Testkonfiguration; Pfade innerhalb eindeutiger Testwurzel | Mit Diagnose abbrechen, niemals auf reale Quelle/Updater zurueckfallen. |

## Konfigurationsaenderungen

| Eintrag | Typ | Standardwert | Zweck |
|---|---|---|---|
| `updates.mode` (`UpdateModeKey`) | Integer als Setting-String | `1` | Persistenter Modus. |
| `updates.includePrereleases` (`IncludePrereleasesKey`) | Boolean als Setting-String | `false` | Persistente Kanalauswahl. |
| `SOFTWARESCHMIEDE_UPDATE_TEST_CONFIG` | Prozess-Umgebungsvariable, JSON-Dateipfad | Nicht gesetzt | Ausschliesslich E2E: kontrollierte Updateumgebung. |

`UpdateOptions` bleibt technische Deploymentkonfiguration mit bestehenden Produktionsdefaults. E2E setzt Werte und explizite Basispfad-Konstruktoren gezielt per DI; Benutzerwerte werden nicht in `appsettings` dupliziert.

## Testvoraussetzungen (P-04, T-09)

1. `UpdateE2EFixture` erstellt eine eindeutige Temp-Wurzel mit `installed/version.json` (normalerweise `1.2.0`), SQLite-DB, Szenario-JSON, Antworten und Protokoll. Gueltige ZIPs enthalten im Root die laut `UpdateOptions.ExecutableName` erwartete Testdatei sowie eine passende `version.json`; Stable/RC erhalten getrennte Download-URLs und unterscheidbare Inhalte.
2. `App.ConfigureServices` aktiviert dies nur mit `SOFTWARESCHMIEDE_TEST_DB_PATH` UND `SOFTWARESCHMIEDE_UPDATE_TEST_CONFIG`. Echter `ApplicationVersionProvider` liest `installed/`, echter Paketservice schreibt nur darunter. `UpdateService`, Releaseclient, Paketservice, Skripterzeugung, Settings-DB, ViewModels und WPF-Dialoge bleiben produktiv.
3. Dedizierten `HttpClient` mit `UpdateFixtureHttpMessageHandler` nur fuer Update-Release-/Paketservices registrieren. Exakte Request-URLs werden auf HTTP-Status, Header einschliesslich `Link`, JSON und ZIP abgebildet. Szenariodatei pro Request lesen; sofortige Antworten, freigabegesteuerte Antworten/Streams, Cancellation und absichtliche Fehler unterstuetzen. Unbekannte Requests scheitern ohne Netzwerkfallback. Kein vorab gemocktes `UpdateCheckResult`.
4. `RecordingUpdateProcessLauncher` protokolliert Skriptpfad, Arbeitsverzeichnis, Argumente/Elevation und liefert kontrolliert Erfolg/Fehler, ohne Prozess zu starten. Echtes `UpdateScriptService.StartScriptAsync` verarbeitet dieses Ergebnis. `RecordingApplicationShutdownService` protokolliert `ShutdownRequested` und laesst die Test-App offen; der Runner schliesst das eigene Fenster danach regulaer. Die reale Betriebssystembeendigung liegt ausserhalb dieses E2E-Nachweises.
5. Strukturiertes JSONL-Protokoll mit Szenario-/Prozess-ID und monotoner Ereignisfolge bereitstellen: `WindowReady`, `StartupUpdateCompleted`, Release-/Asset-URLs, Ergebnis/ausgewaehlte Version, Vorbereitungsphasen, `PreparationCompleted`, Startversuch/-erfolg, `ShutdownRequested`. Vorhandenes Logging mit Test-Sink bzw. eng begrenzte Adapter nutzen; Beobachtung darf Produktionsentscheidungen nicht ersetzen.
6. Nur fuer Sicherheitsdialog-Szenarien liefert `FixtureCliUpdateSafetyService` kontrollierte riskante Aufgaben. FlaUI bedient echte Ja-/Nein-Dialoge; die Sicherheitsbewertung selbst bleibt durch `CliUpdateSafetyServiceTests` abgesichert. Keine ConPTY-Abhaengigkeit fuer Update-E2Es.
7. `WpfTestBase` um prozessbezogene Launch-Konfiguration und `RestartAppPreservingDatabase` erweitern: nur eigenes Testfenster regulaer schliessen, Prozessende abwarten, UIA-Handles erneuern, `LaunchApp(ensureDatabaseDeleted: false)` mit derselben DB/Testwurzel. Keine Neuanlage/Ueberschreibung der gespeicherten Updatewerte beim Neustart. Testvariablen wiederherstellen; keine fremden Prozesse oder Build-`version.json` veraendern.
8. `Views/SettingsView.cs`: Modus-/Checkbox-Getter/Setter und Warten auf Speichern/Reload. `Views/Dialogs/UpdateProgressDialogView.cs`: Phase, Fortschritt, Abbrechen, Fehler, Schliessen. Hauptfenster-/Menuehelfer: Angebotsversion und Enabled/Visible-Zustaende. Sicherheitsdialog nach vorhandenem Win32-Dialogmuster bedienen.
9. Negative Assertions erst nach nachgewiesenem Abschlussmarker oder an kontrolliert blockiertem Schritt, mit begrenztem Warten auf UI/Protokoll. Ein kurzer Sleep oder ein beim Start leeres Log beweist keine ausbleibende Pruefung. Blockierte ZIP-Streams halten echten Fortschritt sichtbar, bis FlaUI geprueft/abgebrochen hat.
10. `E2E_UpdateSettings.cs` als Partial von `End2EndTest` anlegen und in `MainTest.cs` in `RunGeneralTests` konsolidieren (`E2E`, `OsInterface`, Collection `E2E`). Updategruppe nach Schliessen des bisherigen allgemeinen Testfensters mit eigener isolierter Fixture ausfuehren. Nur fuer Start-/Persistenz-/Einmaligkeitsnachweise neu starten; andere Szenarien verketten. Der ConPTY-Skip darf diese Gruppe nicht ueberspringen.
11. `UpdateSettingsReadFailureInterceptor` bereits in U-01 fuer SQLite-/ViewModel-Tests bereitstellen; U-06 verbindet ihn mit der E2E-Konfiguration und registriert ihn ausschliesslich unter beiden Testvariablen am echten DbContext. Er wirft eine kontrollierte DB-Leseexception nur fuer den festen Tag von `GetUpdateSettingsAsync`. Migration, allgemeine Startinitialisierung, andere Settings-Abfragen und Schreibzugriffe funktionieren weiter. Produktive Service-/ViewModel-Fehlerbehandlung bleibt aktiv; weder DB beschaedigen noch ein `NichtPruefbar`-Ergebnis mocken.
12. Die Szenariokonfiguration waehlt Versuch und Lesegrenze (`Initial`, `BeforePreparation`, `BeforeUpdaterStart`). Bei Neustart wird der Fehler erst nach erfolgreicher allgemeiner DB-Initialisierung (`DatabaseInitializationCompleted`) und vor dem ersten Updateversuch aktiviert; UI-Speichern im vorherigen Prozess bleibt erfolgreich. Bei manuellen Varianten aktiviert die Fixture ihn nach dem regulaeren Startabschluss und gegebenenfalls nach einem sichtbaren Updateangebot, vor dem jeweiligen UI-Klick. Nur markierte Reads des gewaehlten Versuchs zaehlen; vorherige Start-/Settings-Reads verbrauchen den Ausloeser nicht. Fruehere Reads und Releaseabruf gelingen bei spaeten Varianten; vor dem letzten Read muss `PreparationCompleted` beobachtet sein. Reihenfolge und exakter Fehlerzeitpunkt werden im Fixture-Smoke geprueft.
13. Vor dem ausgewaehlten Read meldet die Fixture `UpdateSettingsReadReached` mit Versuch, Lesegrenze und Read-Zaehler und wartet begrenzt/cancelbar auf die Fehlerfreigabe des Runners; beim initialen automatischen Fehler ist auch sofortiges Ausloesen nach dem DB-Marker moeglich. Der Runner prueft die erreichte Phase und gibt gezielt die Exception frei (`UpdateSettingsReadFailed`). Zusaetzlich Reads mit Erfolg, `UpdateAttemptCompleted` nach Gate-/Busy-Freigabe und bei Startversuchen `StartupUpdateCompleted` protokollieren. Der Fehler bleibt fuer betroffene Reads bis zur ausdruecklichen Deaktivierung aktiv, damit ein unerlaubter interner Retry keine Erfolgseinstellungen erhaelt; jeder Zusatzread wird sichtbar und laesst den Test scheitern. Nach Fehlerassertions deaktiviert der Runner den Ausloeser (`UpdateSettingsReadFailureDisabled`), gibt alle Wartestellen frei und prueft UI-Wiederherstellung/manuelle Folgepruefung. Fixture-Cleanup setzt Fehlersteuerung auch bei Testabbruch zurueck; jeder Versuch besitzt eigene Protokollgrenzen. Zeitablauf gilt nie als Freigabe oder bestandene Negativassertion.

## Seiteneffekte und Risiken

- **Manuelle Aktionen:** `Aus` sperrt kuenftig auch diese; UI-Praedikate und Methoden-Guards muessen uebereinstimmen und alte Angebote verschwinden.
- **Start:** Konstruktor startet keine Pruefung mehr; Tests muessen die Bereitschaftsmethode ausloesen. Der sofortige Start-E2E prueft die echte Eventverdrahtung und Dialog-Owner.
- **Versionsvertrag:** Stable-only-Erwartungen und `TryParse`-Typannahmen aendern sich; alle Normalisierungsverbraucher, Anzeige und Paketpfade abgleichen.
- **Pagination:** Mehr Requests koennen das gemeinsame Timeout erreichen; spaetere Fehler duerfen keinen hoechsten Teiltreffer vortaeuschen.
- **Nebenlaeufigkeit:** Cancellation allein verhindert keine spaeten Antworten. Generation, frisches DB-Lesen und gemeinsames Gate sind zusammen erforderlich.
- **Installationssicherheit:** Ablehnung, Abbruch und Skriptstartfehler muessen auch automatisch Shutdown verhindern. Testadapter vor Serviceaufloesung aktivieren und in der Testwurzel halten.
- **E2E-Laufzeit:** Reale Neustarts bleiben notwendig; Szenarien im vorhandenen Runner buendeln, keine dauerhafte Aufteilung in viele App-startende Methoden.

## Umsetzungsreihenfolge

| Schritt | Voraussetzungen | Konkrete Umsetzung / Abschlussnachweis |
|---|---|---|
| U-01 | Bestehende Updateklassen, Key-Value-Persistenz, EF Core und Testprojekte mit App-Referenz | Interface-/Normalisierungsverbraucher ermitteln; `UpdateMode`, `UpdateSettings`, `UpdateCheckOptions`, gemeinsame typisierte Persistenz einfuehren; Defaults/Validierung/SQLite-Roundtrip testen. Abfragetag und testseitig steuerbaren `UpdateSettingsReadFailureInterceptor` vor U-05 bereitstellen; propagierten Lesefehler ohne Defaults testen (T-09). |
| U-02 | U-01; vorhandener Comparer/Provider | `SemanticUpdateVersion`, suffixerhaltendes Parsing/Normalisieren; Provider-Verbrauch abgleichen; T-05 mit echten Versionsdateien und Stable-Regressionen. |
| U-03 | U-01/U-02; vorhandene HTTP-/JSON-/Semaphore-Basis | Interfaces/Mocks umstellen, Optionen/Cache, paginierte Releaseauswahl und einheitliche Filter; Client-/Service-Tests einschliesslich T-06. |
| U-04 | U-01; SettingsView und ViewModel-Navigation | Controls, Automation-Namen, Speichern-Event/Handler, Invalidierung/Generation und Command-Guards (P-01, T-03). |
| U-05 | U-02 bis U-04; Settings-Lesefehlerhelfer aus U-01; bestehende Sicherheits-/Fortschritts-/Paket-/Skript-/Shutdown-Services | Start nach `ContentRendered`, gemeinsames Gate/Installationsverfahren, Fehlerzustaende und Event-Cleanup; Sofortantwort-, Negativ- und Nebenlaeufigkeitstests (P-02, T-04, T-07). T-09 an allen drei Lesegrenzen automatisch/manuell mit sichtbarem Fehler, null unerlaubten Folgeschritten, Gate-/Busy-Freigabe und erfolgreicher Folgepruefung testen. |
| U-06 | U-03 bis U-05; Interceptor aus U-01; bestehende Test-DB-Umschaltung und Basispfad-/Launcher-Konstruktoren | Alle Testvoraussetzungen 1 bis 13 bereitstellen: Hostkonfiguration, HTTP/Streams, Protokoll, Launcher/Shutdown/Safety-Adapter, Fixture, Neustarts, FlaUI-Views, Runner. Settings-Lesefehler nach DB-Initialisierung und an spaeten Grenzen samt kontrollierter Freigabe/Deaktivierung integrieren. Fixture-Smoke prueft echte JSON-/ZIP-Pipeline, Testpfade und alle drei Fehlerzeitpunkte vor Szenarien (P-04, T-09). |
| U-07 | U-06 vollstaendig, einschliesslich bestandenem Settings-Lesefehler-Fixture-Smoke; Windows/.NET Desktop/FlaUI | E-01 bis E-07 mit echten Neustarts, UI- und Protokollassertions implementieren (T-01 bis T-04, T-07). T-09 als Pflichtvarianten in E-05/E-06 automatisch/manuell integrieren; Bedienbarkeit und tatsaechliche Folgepruefung nach Deaktivierung des Fehlers nachweisen, keine neue dauerhafte FlaUI-Testmethode. |
| U-08 | U-01 bis U-07; aktualisierte Mocks/Regressionen | Voller Build, regulaere und getrennte OS-Tests nach `CLAUDE.md`; T-08, T-09-Matrix und alle Pflicht-E2Es in `test-results.md` dokumentieren. Fehlende/nicht ausgefuehrte E2Es verhindern Abnahme. |

## Tests

### Neue Tests

Testnamen sind Zielnamen; Parameterfaelle duerfen ohne Abdeckungsverlust zusammengefasst werden.

| Test / Hilfsmethode | Testklasse / Ort | Erwartung / Befund |
|---|---|---|
| `UpdateSettings_DefaultsAndInvalidValues`, `UpdateSettings_SaveLoadAllValues`, `UpdateSettings_DiscardDoesNotPublish` | `SettingsViewModelTests` | 3 Modi x beide Checkboxzustaende, fehlende/unbekannte Werte; Event nur fuer erfolgreich gespeicherte Updatewerte, Verwerfen ohne Wirkung. |
| `UpdateSettings_PersistsAcrossScopes` | Tests fuer `AppEinstellungService` mit SQLite | Wertepaar nach Dispose/neuem Scope unveraendert; bei Speicherfehler kein gemischtes Paar (T-03). |
| `GetUpdateSettingsAsync_ReadFailureDoesNotReturnDefaults` | Tests fuer `AppEinstellungService` mit SQLite und Interceptor aus U-01 | DB erfolgreich initialisieren, gueltiges Wertepaar speichern und lesen; gezielten Read im frischen Scope fehlschlagen lassen: Exception statt Defaults/alter Werte. Fehler deaktivieren, Wertepaar unveraendert wieder lesen (T-09). |
| `UpdateSettingsReadFailure_InitialStopsBeforeReleaseRequest` | `MainWindowViewModelTests` mit echtem Settings-Service/SQLite und Interceptor | Parameter: Startautomatik, manuelles Pruefen, manueller Installationsstart mit zuvor sichtbarem Angebot. Erstes `GetUpdateSettingsAsync` des Versuchs scheitert: sichtbarer Nicht-pruefbar-Hinweis, Angebot ungueltig, null Check/Release/Sicherheitsdialog/Prepare/Asset/Start/Shutdown; kein Default-/Snapshot-Fallback oder interner Retry. Gate/Busy frei; nach Fehlerdeaktivierung, erfolgreichem Settings-Laden/Speichern und UI-konformer Command-Freigabe laeuft genau eine manuelle Folgepruefung ohne automatische Installation (T-09). |
| `UpdateSettingsReadFailure_BeforePreparationStopsInstall`, `UpdateSettingsReadFailure_BeforeUpdaterStartStopsInstall` | `MainWindowViewModelTests` mit echtem Settings-Service/SQLite und Interceptor | Je automatisch und manueller Installationsstart: erste Reads und Releasepruefung erfolgreich, unveraenderte gespeicherte Werte/Generation. Fehler vor Prepare: null Prepare/Asset/Start/Shutdown. Fehler beim letzten Read: erfolgreiche Vorbereitung nachweisen, dann null `MarkUpdaterStarting`/`StartPreparedUpdateAsync`/Launcher/Shutdown. Fehler sichtbar, Angebot ungueltig, offener Dialog schliessbar, Gate/Busy frei; keine Ersatzwerte/Retry/Startwiederholung. Nach Deaktivierung und Settings-Laden/Speichern genau eine erfolgreiche manuelle Folgepruefung nachweisen (T-09). |
| `Normalize_PreservesPrereleaseAndMetadata`, `Compare_PrereleasePrecedence`, `TryParse_RejectsInvalidIdentifiers` | `UpdateVersionComparerTests` | `v1.3.0-rc.1+build.7` normalisiert zu `1.3.0-rc.1+build.7`; `alpha < beta < rc.1 < rc.2 < rc.10 < stable`, numerisch/nichtnumerisch und Identifierlaengen; andere Metadaten nicht neuer (T-05). |
| `GetInstalledVersionAsync_PreservesPrerelease` | `ApplicationVersionProviderTests` | Echte temporaere `version.json` mit `1.3.0-rc.1` und Metadaten behaelt Suffix; vorhandene Fehlerfaelle erhalten (P-03, T-05). |
| `GetLatestReleaseAsync_PreservesPrereleaseVersion` | `GitHubReleaseClientTests` | Echtes JSON-Tag `v1.3.0-rc.1` ergibt `UpdateInfo.Version == 1.3.0-rc.1`, richtige Klassifikation/Asset (T-05). |
| `CheckForUpdateAsync_FromInstalledPrerelease` | `UpdateServiceTests` mit echtem Provider/Releaseclient und Fixture-HTTP | Lokale Datei `rc.1`: `rc.2` und `1.3.0` neuer; `rc.1`, `beta.2`, `1.2.9` nicht neuer; `rc.2` nur mit Checkbox, Stable auch ohne. Beide Normalisierer durchlaufen (T-05). |
| `GetLatestReleaseAsync_FiltersByOptions` | `GitHubReleaseClientTests` | Stable `1.2.1` statt `1.3.0-rc.1` bei `false`, RC bei `true`; GitHub-Flag/Suffix einzeln, Draft-/Tag-/Assetfilter pruefen (T-02). |
| `GetLatestReleaseAsync_SelectsHighestAcrossPages` | `GitHubReleaseClientTests` | Seite 1: Draft `9.0.0`, ungueltiger Tag, assetloses `8.0.0`, RC `1.3.0-rc.1`, Stable `1.2.1`; Seite 2 unsortiert: `1.4.0-rc.1`, `1.2.0`, `1.3.0`. `true` waehlt `1.4.0-rc.1`, `false` `1.3.0`; Abruf beider Seiten nachweisen (T-06). |
| `GetLatestReleaseAsync_LaterPageFailureDiscardsCandidate` | `GitHubReleaseClientTests`, `UpdateServiceTests` | Seite 1 gueltiger Fund, Seite 2 HTTP-/JSON-/Timeoutfehler: `null`/`NichtPruefbar`, kein Teiltreffer. Caller-Cancellation propagiert; Zyklen/unerlaubte Folge-URLs definiert ablehnen (T-06). |
| `CheckForUpdateAsync_PropagatesCurrentOptions` | `UpdateServiceTests` | `true -> false` darf kein gecachtes RC liefern; `IsPrerelease` bei `false` defensiv pruefen; fehlende lokale Version ohne Releaseabruf (T-03). |
| `Constructor_DoesNotCheckUpdates`, `WindowReady_StartsOnceWithImmediateResult` | `MainWindowViewModelTests` | Vor Bereitschaft kein Abruf; danach Modi korrekt, sofortiges Ergebnis; mehrere Aufrufe maximal ein automatischer Ablauf (P-02, T-01, T-07). |
| `Aus_BlocksBothCommandsAndDirectInvocation` | `MainWindowViewModelTests` | Commands gesperrt, Angebot geloescht; auch direkter Aufruf ohne Check/Prepare/Start; Wechsel aus aktivem Modus (P-01, T-03). |
| `SettingsChange_DiscardsDelayedResultAndStopsPreparation` | `MainWindowViewModelTests` | RC-Fund, Checkbox aus: Angebot weg, erneute Pruefung Stable. `Aus`/Kanalwechsel waehrend verzogerter Antwort/Vorbereitung verwirft auch Cancellation ignorierende Antworten, kein Start/Shutdown (T-03, T-07). |
| `Startup_NoUpdateOrNotCheckable_DoesNotInstall` | `MainWindowViewModelTests` | Kein neueres Release, unlesbare lokale Version, nicht pruefbare Quelle: null Sicherheitsdialog/Prepare/Start/Shutdown, Busy zurueckgesetzt (T-04). |
| `Startup_SafetyDeclinedOrCancelled_DoesNotContinue` | `MainWindowViewModelTests` | Nein: null Prepare/Start; Fortschrittsabbruch: Token gecancelt, null Start; immer null Shutdown, Dialog schliessbar, manuelle Bedienung wieder moeglich (T-04). |
| `Startup_PrepareOrStartFailure_DoesNotShutdown` | `MainWindowViewModelTests`, `UpdateServiceTests` | Prepare wirft: null Start; Skriptstart wirft/Launcher meldet Fehler: null Shutdown; sichtbarer Fehler/Gate frei (T-04). |
| `Startup_ConcurrentCommandsCannotDuplicateInstall` | `MainWindowViewModelTests` | `TaskCompletionSource` blockiert Check und danach Prepare; beide Commands/direkte Starts versuchen. Maximal ein Check/Prepare/Start der Automatik; kein Shutdown vor Prepare-/Starterfolg; erneuter Bereitschaftsaufruf auch nach Abschluss/Abbruch ohne Automatik (T-07). |
| `Fixture_UsesIsolatedRealUpdatePipeline`, `RestartAppPreservingDatabase` | Fixture-/Hosttests | JSON/echte ZIP-Pruefung, korrekte DI-Grenzen/Testpfade, DB-Erhalt und Start-/Shutdown-Protokoll; unbekannter Request scheitert ohne Netzwerkfallback (P-04). |
| `Fixture_SettingsReadFailureTargetsPhaseAfterDatabaseInitialization` | Fixture-/Hosttests | Alle drei Lesegrenzen/Versuche aus Testvoraussetzungen 11 bis 13: DB-Initialisierung und Speichern gelingen; nur ausgewaehlter `GetUpdateSettingsAsync`-Read scheitert. Spaete Reads erst nach erfolgreichen frueheren Reads/Releasepruefung, letzter erst nach `PreparationCompleted`; Fehlerfreigabe, wiederholte Fehler bis Deaktivierung, Wiederlesen danach, Cancellation/Timeout-Cleanup und Isolation zwischen Scopes/Versuchen pruefen (T-09). |

### Betroffene bestehende Tests (T-08)

| Test / Testklasse | Anpassung und beizubehaltende Regression |
|---|---|
| `UpdateVersionComparerTests.IsNewer_ShouldCompareSemVerValues` | Bisheriges `1.2.3 -> 1.2.4-beta.1 = false` wird im reinen Vergleich `true`; Kanalausschluss in Client-/Servicetests pruefen. Stable-Faelle erhalten. |
| `UpdateVersionComparerTests.TryParse_ShouldAcceptOnlyStableSemVerTags` | In SemVer-Akzeptanz umbenennen; `v1.2.3-beta.1` gueltig, Kurzversionen/ungueltige Tags weiter ungueltig. |
| `GitHubReleaseClientTests` und alle `IUpdateReleaseClient`-Mocks | `/latest`-Einzelobjekt auf paginierte Listen umstellen, Optionssignatur verifizieren; HTTP/JSON/Timeout/Assetfehler erhalten. |
| `ApplicationVersionProviderTests` | Stable-Normalisierung und fehlende/ungueltige `version.json` erhalten; Suffix/Metadaten ergaenzen. |
| `UpdateServiceTests` | Optionsargumente und `UpdateInfo`-Klassifikation anpassen; fehlgeschlagener Skriptstart ohne Shutdown bleibt Pflicht. |
| `MainWindowViewModelTests.Constructor_ShouldStartUpdateCheckAndExposeAvailableUpdate` | Durch Konstruktor-ohne-Pruefung- und Bereitschaftstest ersetzen; andere Command-Tests mit bekannten Einstellungen nach Bereitschaft initialisieren. |
| `UpdateStartenCommand_ShouldStop_WhenSafetyDialogIsDeclined` in `MainWindowViewModelTests` | Erhalten und fuer automatischen Pfad spiegeln. |
| `UpdateStartenCommand_ShouldCancelPrepareUpdate_WhenProgressDialogCancelIsExecuted` in `MainWindowViewModelTests` | Erhalten und automatisch spiegeln; kein Start/Shutdown nach Abbruch. |
| `UpdateStartenCommand_ShouldEnableProgressDialogCloseBeforeStartingPreparedUpdate` in `MainWindowViewModelTests` | Fortschritts-/Schliessen-Zustand vor Start auch automatisch pruefen. |
| `UpdatePackageServiceTests` | Gesamte Klasse ausfuehren: Download, Entpacken, Paketvalidierung, Pfadbegrenzung/Aufraeumen; RC-Paketpfad mit passender `version.json` ergaenzen. |
| `UpdateScriptServiceTests` | Gesamte Klasse ausfuehren: Skripterzeugung, Parameter, Launcher-Ergebnis und Fehler ohne echte Installation. |
| `CliUpdateSafetyServiceTests`, `UpdateProgressViewModelTests`, `UpdateProgressDialogTests` | Sicherheitsbewertung, Fortschritt, Abbruch und reale Dialogzustaende erhalten. |
| `SettingsViewModelTests`, `E2E_SettingsFeatureFlags`, `E2E_VersionAnzeige`, `E2E_ViewPattern`, `MainTest.cs` | Bestehende Settings/Navigation/Versionsanzeige erhalten; Testregistrierung nur in Update-Fixture, konsolidierter Runner bleibt kanonisch. |

### E2E-Tests (primaerer Funktionsnachweis)

Alle Szenarien sind Pflicht. Zielort: `src/Softwareschmiede.Tests/E2E/E2E_UpdateSettings.cs`, Partial `End2EndTest`, eingebunden in `RunGeneralTests`. Jede Phase hat eigene Protokollgrenzen; alte Requests/Shutdowns zaehlen nicht fuer die naechste Phase.

| ID / Szenario | Konkreter Ablauf und Assertions | AK / Befunde |
|---|---|---|
| E-01 `Settings_AllModesAndPrereleasesPersist` | Einstellungen oeffnen, alle drei exakten Labels und beide Checkboxzustaende setzen/speichern, weg-/zuruecknavigieren und Werte pruefen. Ungespeicherte Aenderung verwerfen. Mindestens ein echter Neustart mit derselben DB, beide Werte wieder anzeigen; minimale Fenstergroesse auf lesbaren Auswahltext pruefen. | AK 1, 5, 7; T-03 |
| E-02 `Startup_ModesDriveUpdatePipeline` | Lokal `1.2.0`, Stable `1.2.1`, gueltiges ZIP. Je Modus ueber UI speichern, eigenes Fenster regulaer schliessen, gleiche DB neu starten. `Aus`: nach `StartupUpdateCompleted` null Release/Asset/Start/Shutdown, Pruefen disabled, Installieren verborgen. `Nur Pruefen`: Abruf/sichtbares Angebot `1.2.1` im Tooltip, null Asset/Start/Shutdown. Startmodus: sofortige Releaseantwort, ohne Updateklick echter Fortschrittsdialog mit korrektem Owner; Download blockieren, UI pruefen, freigeben, entpacktes Paket/Skript und genau einen Launcher-Erfolg vor genau einem Shutdown-Aufruf nachweisen. | AK 2, 3, 4, 7; P-01, P-02; T-01 |
| E-03 `PrereleaseCheckbox_SelectsMatchingAsset` | Lokal `1.2.0`, Stable `1.2.1`, RC `1.3.0-rc.1`, verschiedene gueltige Assets. In `Nur Pruefen` Checkbox aus/ein per UI speichern, Pruefbutton klicken: exakt Stable/RC angeboten. Danach Installationsbutton klicken: echte Vorbereitung bis aufgezeichneter Uebergabe, Download-URL, entpackte `version.json` und Skriptziel gehoeren exakt zur Version. Beide Checkboxzustaende auch mit UI-gespeichertem Startmodus und je echtem Neustart ohne Updateklick pruefen. | AK 3, 4, 6, 7; T-02 |
| E-04 `SavedChangesInvalidatePreviousOffer` | RC anbieten, Checkbox aus/speichern: altes Angebot sofort weg, alter Installationsbutton nicht aktivierbar. Erneut pruefen, Stable anzeigen/installieren, nur Stable-Asset angefordert. Erneut RC anbieten, `Aus` speichern: Pruefen disabled, Angebot verborgen, keine neuen Requests/Starts; gleiche DB neu starten, Nullabruf bestaetigen. Weiteren laufenden Pruefrequest blockieren, waehrenddessen `Aus` speichern, Antwort freigeben: kein spaetes Angebot/Download. Direkten stale-Installationsaufruf ergaenzend im ViewModel-Test pruefen. | AK 2, 6, 7; P-01; T-03 |
| E-05 `Startup_NoUpdateOrUncheckableRemainsUsable` | Startmodus vorher ueber UI speichern. Je Neustart Fixture: gleiche/aeltere Version, fehlende/ungueltige lokale Datei, Release-HTTP-/JSON-Fehler. Startabschluss abwarten: kein Angebot/Fortschrittsdialog, null Asset/Launcher/Shutdown; bei ungueltiger lokaler Version auch null Releaseabruf. Nicht-pruefbar-Hinweis fuer Fehler kontrollieren und Einstellungen bedienen. | AK 4, 7; T-04 |
| E-06 `Startup_SafetyCancelAndErrorsRemainUsable` | Startmodus UI-speichern, fuer jeden automatischen Versuch neu starten. (a) Riskante CLI-Fixture: echten Dialog mit Nein beantworten, null Prepare/Asset/Start/Shutdown. (b) Ja bestaetigen, echten Downloadfortschritt abwarten, Abbrechen klicken, blockierten Stream beenden: Abbruchzustand, null Start/Shutdown. (c) Fehlerhaftes ZIP/Asset-HTTP-Fehler: sichtbarer Vorbereitungsfehler, null Start/Shutdown. (d) Gueltige Vorbereitung, Launcherfehler: ein Versuch, null erfolgreicher Start/Shutdown, sichtbarer Fehler. Nach jedem Fall Dialog schliessen, Einstellungen bedienen, keine automatische Wiederholung. | AK 4, 7; T-04, T-08 |
| E-07 `Startup_IsOnceAndCommandsStayBlocked` | Startmodus UI-speichern/neustarten, Releaseantwort und dann ZIP-Stream blockieren. Pruefen/Installieren disabled bzw. verborgen; versuchte UI-Aktivierung erzeugt keinen zweiten Ablauf, kein Shutdown vor Freigabe. Danach genau eine Vorbereitung/Uebergabe/Shutdown-Aufzeichnung. Im offen gehaltenen Testfenster navigieren/erneut rendern: unveraenderte Zaehler. Wiederholten direkten Bereitschaftsaufruf ergaenzend im ViewModel testen. | AK 4, 7; T-07 |

UI-Assertions pruefen echte Events, Bindings, Owner, Aktionen und Bedienbarkeit; Protokolle ergaenzen nicht sichtbare Negativbedingungen/Reihenfolgen. Reale Installation und Betriebssystembeendigung liegen hinter der kontrollierten E2E-Grenze und werden nicht als durch FlaUI ausgefuehrt behauptet. Unit-/Integrationstests ersetzen keines dieser Pflichtszenarien.

### Ausfuehrung und Abnahme

1. Erfolgreichen vollen `dotnet build Softwareschmiede.slnx` vor Tests durchfuehren; .NET-Desktop-Voraussetzungen pruefen. Kein `--no-build`.
2. Regulaere Spur: `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"`.
3. OS-/E2E-Spur separat: `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category=OsInterface"`. In jedem automatisierten Sandbox-Testaufruf `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` setzen; Update-E2Es in `RunGeneralTests` werden dadurch nicht uebersprungen.
4. Beide Testlaeufe im Vordergrund abwarten, keine parallelen Build-/Testprozesse und keine Hintergrund-Testlaeufe. Nach temporaer fokussierter E2E-Ausfuehrung den konsolidierten relevanten Runner ausfuehren, keinen temporaeren Runner behalten.
5. Bei FlaUI-Timeout zuerst Logs des gestarteten Testprozesses auf Startup-Exceptions pruefen. Bei Build-Locks keine Host-App beenden. Nicht ausfuehrbare Pflicht-E2Es als fehlender/fehlgeschlagener Nachweis dokumentieren, nicht durch Unit-Tests ersetzen.
6. `test-results.md` weist Build, beide Spuren, Ueberspringungen und E-01 bis E-07 samt Varianten/Protokollen aus. Erst erfolgreiche Pflicht-E2Es und gruene relevante Regressionen erfuellen die Umsetzungsabnahme.

## Nachverfolgung der Gegenpruefung

| Befund | Verbindliche Umsetzung | Konkreter Nachweis |
|---|---|---|
| P-01 | Alle Eintrittspunkte/Commands sperren, Fund invalidieren; U-04/U-05 | `Aus_BlocksBothCommandsAndDirectInvocation`, E-02/E-04 |
| P-02 | `ContentRendered` nach Konfiguration und Owner-Zuweisung, Konstruktor ohne Abruf; U-05 | Sofortantwort-/Einmaligkeitstest, E-02 mit echtem Start/Dialog-Owner |
| P-03 | Suffixerhaltung bei Provider und Releaseclient; U-02/U-03 | T-05-Kette mit Quelldateien/JSON und Folgeupdates |
| P-04 | HTTP/ZIP, lokale Version, DB-Neustart, Prozessprotokoll, Runner vor Szenarien; U-06 | Fixture-Smoke und E-01 bis E-07 |
| T-01 | Modusabhaengiger einmaliger Start; U-05/U-07 | E-02 alle Modi mit UI-Speichern/Neustart |
| T-02 | Filter/Optionen bis Asset; U-03/U-07 | E-03 beide Checkboxzustaende manuell/automatisch |
| T-03 | Persistenz, Generation/Invalidierung und `Aus`-Guard; U-01/U-04/U-05 | SQLite-/stale-result-Tests, E-01/E-04 |
| T-04 | Sicherheit/Abbruch/Fehler ohne Shutdown; U-05 | Negative Service-/ViewModel-Tests, E-05/E-06 |
| T-05 | Lokale/entfernte Normalisierung und SemVer; U-02/U-03 | Provider-/Client-/Normalisierungs-/Service-Kettentests |
| T-06 | Vollstaendige Pagination, keine Teiltreffer bei Fehler; U-03 | Mehrseitenfixture beider Kanaele und Folgeseitenfehler |
| T-07 | Nicht wartendes Gate, Generation, einmaliger Start; U-05 | Verzoegerte Check-/Prepare-Tests, direkte Aufrufe, E-07 |
| T-08 | Stable-only-/Interfacetests anpassen, Installationsregressionen erhalten; U-08 | Explizite Regressionstabelle und getrennte Testlaeufe |

## Offene Punkte

Keine.

# Datenmodell

## `InstalledVersionInfo` (record)
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` (Zeilen 8–12)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Version` | `string` | Semantische Version ohne führendes `v` (aus installierter `version.json`, normalisiert) |
| `TagName` | `string?` | Optionaler Release-Tag |
| `Commit` | `string?` | Optionaler Commit-SHA |
| `CreatedAtUtc` | `DateTimeOffset?` | Optionaler Erstellungszeitpunkt |

## `UpdateInfo` (record)
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` (Zeilen 21–27)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Version` | `string` | Semantische Release-Version ohne `v`, inkl. Prerelease-Suffix/Build-Metadaten |
| `TagName` | `string` | GitHub-Release-Tag |
| `AssetName` | `string` | Name des Release-Assets |
| `DownloadUrl` | `Uri` | Direkte Download-URL des Assets (wird von `UpdatePackageService` genutzt) |
| `PublishedAt` | `DateTimeOffset?` | Optionaler Veröffentlichungszeitpunkt |
| `IsPrerelease` | `bool` | Prerelease-Klassifikation (GitHub-Flag `prerelease` ODER SemVer-Suffix); trägt heute die Filter-/Defensivprüfungs-Semantik |

## `UpdateSettings` (record)
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` (Zeile 45)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Modus` | `UpdateMode` | Persistierter Update-Modus |
| `IncludePrereleases` | `bool` | Ob Prereleases bei der Update-Auswahl berücksichtigt werden (inklusiv, nicht exklusiv) |

Record-Gleichheit wird von `MainWindowUpdateFlow.ApplyGespeicherteUpdateSettings`/`PruefeSettingsAktualitaetAsync` zur Änderungserkennung genutzt. Wird erzeugt in `AppEinstellungService.GetUpdateSettingsAsync` und `SettingsViewModel.SpeichernAsync`; als `UpdateSettingsSaved`-Event-Argument weitergereicht.

## `UpdateCheckOptions` (record)
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` (Zeile 49)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `IncludePrereleases` | `bool` | Optionsvertrag einer Update-Prüfung für `IUpdateService` und `IUpdateReleaseClient` — einziges steuerndes Feld |

Erzeugt an drei Stellen in `MainWindowUpdateFlow` (Zeilen 173, 198, 220) aus `settings.IncludePrereleases`.

## `UpdateReleaseLookupResult` (record)
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` (Zeilen 56–79)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Erfolg` | `bool` | Ob die Release-Abfrage technisch erfolgreich war |
| `Release` | `UpdateInfo?` | Neuestes zulässiges Release oder `null` |
| `Fehlgeschlagen` | `static UpdateReleaseLookupResult` | Factory: fehlgeschlagene Abfrage |
| `KeinTreffer` | `static UpdateReleaseLookupResult` | Factory: erfolgreich ohne Kandidaten |
| `Gefunden(UpdateInfo)` | `static UpdateReleaseLookupResult` | Factory: erfolgreich mit Release |

## `UpdateCheckResult` (record)
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` (Zeilen 98–108)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Status` | `UpdateCheckStatus` | Fachlicher Prüfstatus |
| `Update` | `UpdateInfo?` | Optionales verfügbares Update |
| `Message` | `string?` | Optionale Diagnose-/Benutzerhinweismeldung |
| `KeinUpdate(string?)` | `static` | Factory |
| `UpdateVerfuegbar(UpdateInfo)` | `static` | Factory |
| `NichtPruefbar(string?)` | `static` | Factory (wird u. a. für die Defensivprüfung bei kanal-/prerelease-fremden Ergebnissen verwendet) |

## `UpdatePreparationProgress` (record)
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` (Zeile 127)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Phase` | `UpdatePreparationPhase` | Aktuelle Phase |
| `Percent` | `double?` | Optionaler Prozentwert 0–100 |
| `Message` | `string` | Anzeigetext für den Fortschrittsdialog |

## `UpdatePreparationResult` (record)
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` (Zeilen 135–140)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `ZipPath` | `string` | Pfad zur heruntergeladenen ZIP |
| `ExtractedDirectory` | `string` | Pfad zum entpackten Release-Verzeichnis |
| `ScriptPath` | `string` | Pfad zum erzeugten Update-Skript |
| `LogPath` | `string` | Pfad zur Update-Logdatei |
| `RequiresElevation` | `bool` | Ob der Skriptstart erhöhte Rechte anfordern soll |

## `CliUpdateSafetyResult` (record)
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` (Zeilen 145–149)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `RiskyTaskCount` | `int` | Anzahl riskanter aktiver CLI-Aufgaben |
| `RiskyTasks` | `IReadOnlyList<string>` | Kurzbeschreibungen der riskanten Aufgaben |
| `RequiresConfirmation` | `bool` (abgeleitet) | `RiskyTaskCount > 0` |

## `UpdateOptions` (Klasse)
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateOptions.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `RepositoryOwner` | `string` (init) | GitHub-Repository-Owner, Default `"martin-stromberg"` |
| `RepositoryName` | `string` (init) | GitHub-Repository-Name, Default `"Softwareschmiede"` |
| `AssetName` | `string` (init) | Erwartetes Release-Asset, Default `"release.zip"` |
| `UpdateDirectoryName` | `string` (init) | Update-Arbeitsverzeichnis relativ zum Programmverzeichnis, Default `"updates"` |
| `CheckTimeout` | `TimeSpan` (init) | Timeout für HTTP-Prüfung/Download, Default 15 s |
| `ExecutableName` | `string` (init) | Exe-Name im Release-Paket, Default `"Softwareschmiede.exe"` |

Wird in `App.xaml.cs` (Zeile 193) unverändert als `IOptions<UpdateOptions>` registriert; die Update-Quelle ist damit vollständig hartcodiert — kein konfigurierbarer Pfad/Endpunkt.

## `UpdateModusOption` (record) / `UpdateModusTexte` (statische Klasse)
Datei: `src/Softwareschmiede.App/ViewModels/UpdateModusOptionen.cs`

| Element | Typ | Beschreibung / Zweck |
|---------|-----|----------------------|
| `Modus` | `UpdateMode` | Steuerwert der Auswahloption |
| `Label` | `string` | Anzeige-Label in der ComboBox |
| `Alle` | `static IReadOnlyList<UpdateModusOption>` | Feste Auswahlliste (Aus / Nur prüfen / Bei Programmstart prüfen und ausführen) |
| `FuerModus(UpdateMode)` | `static UpdateModusOption` | Option zu gespeichertem Modus |
| `UpdateModusTexte.Aus/.NurPruefen/.BeiProgrammstartPruefenUndAusfuehren` | `const string` | Einzige Quelle der Labels (UI + Tests) |

## E2E-Szenariomodelle
Dateien: `src/Softwareschmiede.App/Services/Testing/UpdateE2ESzenario.cs`, `UpdateE2ETestConfiguration.cs`

| Klasse | Relevante Eigenschaften | Zweck |
|--------|------------------------|-------|
| `UpdateE2ESzenario` | `Antworten`, `Lesefehler`, `CliSicherheit`, `ProzessStart` | Pro Request/Read neu gelesene Steuerdatei; `FindeAntwort(url)` bildet exakte URLs auf Antworten ab |
| `UpdateE2EAntwort` | `Url`, `Status`, `Header`, `BodyDatei`, `Inhalt`, `ContentType`, `Gate`, `StreamGate`, `StreamGateBytes`, `Fehler` | Antwortdefinition inkl. Blockade-Gates und Stream-Gates |
| `UpdateE2ELesefehler` | `Aktiv`, `Versuch`, `Grenze`, `AufFreigabeWarten`, `WartezeitSekunden`; `GrenzOrdinal()`, `TrifftVersuch(art)` | Steuerung des kontrollierten Settings-Lesefehlers (Grenzen Initial/BeforePreparation/BeforeUpdaterStart) |
| `UpdateE2ECliSicherheit` | `Aktiv`, `RiskanteAufgaben` | Simulierte riskante CLI-Aufgaben |
| `UpdateE2EProzessStart` | `Ergebnis` | Gesteuertes Launcher-Ergebnis |
| `UpdateE2ETestConfiguration` | `Testwurzel`, `SzenarioDateiPfad`, `ProtokollDateiPfad`, `SzenarioId`, `GatesVerzeichnis`, `InstallationsVerzeichnis`, `EffektivesGatesVerzeichnis`; `LadeAusUmgebung(testDatenbankPfad)`, `Lade(pfad)`, `InstallationsVersionsDateiPfad()` | Statische Konfiguration der Update-E2E-Umgebung; Aktivierung nur mit beiden Umgebungsvariablen `SOFTWARESCHMIEDE_UPDATE_TEST_CONFIG` + `SOFTWARESCHMIEDE_TEST_DB_PATH` |
| `UpdateE2EProtokollEintrag` | `Seq`, `ProzessId`, `SzenarioId`, `Ereignis`, `Daten`, `Utc`, `Quelle` | JSONL-Protokolleintrag |

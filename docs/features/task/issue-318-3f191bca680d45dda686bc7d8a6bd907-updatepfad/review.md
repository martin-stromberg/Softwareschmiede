# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

Gegenstand: Review des Plans [plan.md](plan.md) gegen die uncommittete Implementierung im Arbeitsverzeichnis (Branch `task/issue-318-3f191bca680d45dda686bc7d8a6bd907-updatepfad`). Per Vorgabe wurden **keine Builds und keine Tests ausgeführt** — alle Aussagen beruhen auf Quellcode-Inspektion (`git status`/`git diff` plus Volltextlektüre der geänderten Dateien).

## Umgesetzte Planelemente

### Neue Klassen

- [x] `UpdateKanal` (Enum, `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs`, Zeilen 43–50) — `Produktiv = 0`, `ReleaseCandidate = 1`
- [x] `UpdatePfadKanalResolver` (statische Klasse, `src/Softwareschmiede/Application/Services/Updates/UpdatePfadKanalResolver.cs`) — Konstanten `ProduktivversionenPfad`/`VorabversionenPfad`; `ErmittleKanal` (`"Vorabversionen"` → `ReleaseCandidate`, alles andere → `Produktiv`), `NormalisierePfad` (unbekannt/leer → `Produktivversionen`), `IstGueltigerPfad` (nur die zwei bekannten Werte, OrdinalIgnoreCase + Trim), `ErmittleKanalFuerTagName` (`-rc`-Infix → `ReleaseCandidate`, fehlender/leerer TagName konservativ → `ReleaseCandidate`); keine DI-Registrierung
- [x] `UpdatePfadOption` / `UpdatePfadTexte` (Record + statische Klasse, `src/Softwareschmiede.App/ViewModels/UpdatePfadOptionen.cs`) — `Alle` mit den zwei Pfaden, `FuerPfad(string?)` mit Produktiv-Fallback, Kanal-Anzeigetexte `KanalProduktiv`/`KanalReleaseCandidate`

### Geänderte bestehende Klassen

- [x] `UpdateSettings` (`UpdateModels.cs:55–59`) — Signatur `(UpdateMode Modus, string UpdatePfad)`, `IncludePrereleases` entfernt, abgeleitete `Kanal`-Eigenschaft über `UpdatePfadKanalResolver.ErmittleKanal`
- [x] `UpdateCheckOptions` (`UpdateModels.cs:63–67`) — Signatur `(string UpdatePfad)`, abgeleitete `Kanal`-Eigenschaft
- [x] `UpdateCheckResult` (`UpdateModels.cs:122`) — `UpdateVerfuegbar(UpdateInfo update, string? message = null)`
- [x] `AppEinstellungService` (`src/Softwareschmiede/Application/Services/AppEinstellungService.cs`) — `UpdatePfadKey = "updates.pfad"` deklariert, `IncludePrereleasesKey` entfernt; `GetUpdateSettingsAsync` liest `updates.mode` + `updates.pfad` in einer `UpdateSettingsReadTag`-getaggten Abfrage mit `NormalisierePfad`-Fallback auf `Produktivversionen`; `SetUpdateSettingsAsync` validiert zusätzlich per `IstGueltigerPfad` (`ArgumentOutOfRangeException`, kein Write) und schreibt beide Schlüssel in einem Speichervorgang; `updates.includePrereleases` verfällt still (kein Leser/Schreiber mehr im Code)
- [x] `GitHubReleaseClient.TryCreateCandidate` (`src/Softwareschmiede/Infrastructure/Services/Updates/GitHubReleaseClient.cs:122`) — exklusiver Filter `isPrerelease != (options.Kanal == UpdateKanal.ReleaseCandidate)`; Request-URI (Zeile 31), Pagination und `IsAllowedFollowUpUri` unverändert
- [x] `UpdateService.CheckForUpdateAsync` (`src/Softwareschmiede/Application/Services/Updates/UpdateService.cs`) — Defensivprüfung beider Richtungen (`latest.IsPrerelease` vs. `options.Kanal` → `NichtPruefbar`); neue nicht-blockierende Konsistenzwarnung: `ErmittleKanalFuerTagName(installed.TagName)` vs. `options.Kanal`, Konflikt → `UpdateVerfuegbar(latest, konfliktHinweis)` + `_logger.LogWarning`, nur im Angebotsfall
- [x] `MainWindowUpdateFlow` (`src/Softwareschmiede.App/ViewModels/MainWindowUpdateFlow.cs`) — alle drei Erzeugungsstellen `new UpdateCheckOptions(settings.UpdatePfad)` (Zeilen 173, 198, 220); `ApplyUpdateCheckResult` setzt `UpdateHinweis = result.Message` auch im `UpdateVerfuegbar`-Zweig (Zeile 419); `ApplyGespeicherteUpdateSettings`/`PruefeSettingsAktualitaetAsync` unverändert
- [x] `SettingsViewModel` (`src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs`) — `IncludePrereleases` samt Feld entfernt; `UpdatePfadOptionen`, `SelectedUpdatePfad` (benachrichtigt `AbgeleiteterUpdateKanal`), schreibgeschütztes `AbgeleiteterUpdateKanal`; `LadenAsync` → `UpdatePfadOption.FuerPfad(updateSettings.UpdatePfad)`; `SpeichernAsync` → Guard `SelectedUpdatePfad is not { Pfad: var updatePfad }` → `FehlerMeldung`, `new UpdateSettings(updateModus, updatePfad)`; `UpdateSettingsSaved` unverändert
- [x] `SettingsView.xaml` (`src/Softwareschmiede.App/Views/SettingsView.xaml`) — ComboBox `AutomationProperties.Name="Update-Pfad"` (`ItemsSource` ↔ `UpdatePfadOptionen`, `SelectedItem` ↔ `SelectedUpdatePfad`, `Label`-ItemTemplate), schreibgeschützter TextBlock `AutomationProperties.Name="Update-Kanal"` ↔ `AbgeleiteterUpdateKanal`; CheckBox „Prerelease-Versionen laden" samt altem Hinweistext entfernt, neuer Hinweistext zur exklusiven Kanalsemantik
- [x] `ProtokollierenderUpdateService` (`src/Softwareschmiede.App/Services/Testing/ProtokollierenderUpdateService.cs:32`) — `UpdateCheckStarted` protokolliert `updatePfad` + `kanal` statt `includePrereleases`; `UpdateCheckCompleted` unverändert

### E2E-Infrastruktur

- [x] `UpdateE2EFixture` (`src/Softwareschmiede.Tests/E2E/UpdateE2EFixture.cs`) — `SetzeUpdateEinstellungenAsync`/`AktualisiereUpdateEinstellungenAsync` auf `(modus, updatePfad)` umgestellt; `ReleaseApiUrl`/`SetzeStandardAntworten` unverändert
- [x] FlaUI-Page-Object `SettingsView` (`src/Softwareschmiede.Tests/E2E/Views/SettingsView.cs`) — `GetUpdatePfad`, `SetUpdatePfad(label)` (ComboBox über `SelectComboBoxItemByClick` + `WaitForSelectedComboBoxItem`, Muster `SetUpdateMode`), `WaitForUpdatePfad`, `GetAbgeleitetenUpdateKanal` (`AutomationProperties.Name="Update-Kanal"`); `GetIncludePrereleases`/`SetIncludePrereleases` entfernt
- [x] Protokollauswertungen — `DatenText(e, "updatePfad")`/`DatenText(e, "kanal")` in `E2E_UpdateSettings.cs`/`E2E_UpdateFixture.cs`; kein `includePrereleases`-Verweis mehr unter `src/` (außer dem absichtlichen Altbestand-Schlüsselliteral im Persistenztest)
- [x] `MainTest.cs` — konsolidierter Runner ruft `Settings_AllModesAndUpdatePfadPersist` + `UpdatePfad_SelectsExclusiveChannelAsset`; `ScmE2EFixture.cs` auf neue `UpdateSettings`-Signatur umgestellt

### Tests

- [x] `UpdatePfadKanalResolverTests` (neu, `src/Softwareschmiede.Tests/Application/Services/Updates/UpdatePfadKanalResolverTests.cs`) — vier Theorien: `ErmittleKanal`, `NormalisierePfad`, `IstGueltigerPfad`, `ErmittleKanalFuerTagName` (inkl. OrdinalIgnoreCase-/Whitespace-Varianten und fehlendem TagName → RC)
- [x] `GitHubReleaseClientTests_Filters` — `FiltersByOptions` → `GetLatestReleaseAsync_FiltersExclusivelyByChannel` (Produktiv-Kanal: höchste Stabile `1.4.0` schlägt RC; RC-Kanal: `1.3.0-rc.1`, Stabile werden übersprungen); `FiltersGitHubPrereleaseFlag`, `FiltersSemVerPrereleaseSuffix`, `PreservesPrereleaseVersion`, `SkipsDraftInvalidTagAndUnusableAssetEntries` auf Pfad-Signatur/Kanalrichtung umgestellt
- [x] `GitHubReleaseClientTests` + `GitHubReleaseClientTests_Pagination` — Options-Signatur `UpdateCheckOptions(string)`; `Page1Url`/`Page2Url` und `IsAllowedFollowUpUri`-Erwartungen unverändert
- [x] `UpdateServiceTests` / `UpdateServiceTests_Options` / `UpdateServiceTests_PrereleaseChain` — `PropagatesCurrentOptions` auf Pfadwechsel, `RejectsChannelMismatch` (beide Kanäle → `NichtPruefbar`), neu `CheckForUpdateAsync_WarnsOnInstallationChannelConflict` (Konflikt → `UpdateVerfuegbar` + Warn-`Message`, passend → `Message` null), RC-Kette: stabile `1.3.0` kein Update im RC-Kanal, im Produktiv-Kanal mit Konfliktwarnung
- [x] `AppEinstellungServiceTests_UpdateSettings` — Pfad-Persistenz über Scopes, Defaults `NurPruefen`/`Produktivversionen`, `SetUpdateSettingsAsync_RejectsUnknownUpdatePfad` (`ArgumentOutOfRangeException`), Altbestand `updates.includePrereleases` wird ignoriert
- [x] `SettingsViewModelTests` — Theory-Matrix Modus × Pfad (6 Kombinationen), `AbgeleiteterUpdateKanal`-Asserts, `SelectedUpdatePfad = null` blockiert Speichern, Event-Snapshot, Verwerfen
- [x] `MainWindowViewModelTests_UpdateStartup`/`_UpdateSettingsReadFailure`/`MainWindowViewModelUpdateTestBase` — Signaturen auf Pfad umgestellt, `SpeichereUpdateEinstellungenUeberUiAsync` über `SelectedUpdatePfad`, Options-Verifies auf `UpdatePfad`, `UpdateHinweis`-Asserts vorhanden
- [x] E-01 `Settings_AllModesAndUpdatePfadPersist` — beide Pfade über echte UI, Kanal-Anzeige (`GetAbgeleitetenUpdateKanal`), Persistenz über Neustart, „Verwerfen", Protokollnachweis `updatePfad`/`kanal`
- [x] E-03 `UpdatePfad_SelectsExclusiveChannelAsset` — beide Pfade exklusiv (RC-Kanal wählt RC trotz höherer stabiler `v9.9.9`-Konkurrenz), Artefaktnachweis (Download-URL, `version.json`, Skriptziel), Protokollnachweis, verketteter Konfliktfall (produktive Installation + `Vorabversionen` → Angebot + `UpdateHinweis`, nicht blockierend), jeweils auch über Startmodus + echtem Neustart
- [x] E-02/E-04/E-05–E-07 und `E2E_UpdateFixture.cs` — alle `SpeichereUpdateEinstellungenUeberUiAsync`-Aufrufe auf Pfad-Labels, Protokollnachweise auf `updatePfad`/`kanal`; keine neuen Testmethoden

### Konfiguration / Release-Quelle

- [x] `updates.pfad` als neuer Key-Value-Eintrag, `updates.includePrereleases` verfällt still — umgesetzt wie geplant
- [x] `UpdateOptions` (`RepositoryOwner`/`RepositoryName`/`AssetName`), Request-URI und DI-Registrierung unverändert — verifiziert: der Pfad parametrisiert die Release-Quelle nicht

## Offene Aufgaben

Keine. Aufgabe 26 (Dokumentation `docs/help/programmupdate/*` und `docs/help/einstellungen/*`) ist im Plan (Umsetzungsreihenfolge, Schritt 11) ausdrücklich dem Dokumentationsschritt des Workflows zugeordnet und daher in der Tasks-Datei als `Folgt in Dokumentationsschritt` markiert — sie ist keine Implementierungslücke dieses Schritts.

## Hinweise

- Kein Build/Test ausgeführt (per Vorgabe); die Kompilierbarkeit ergibt sich aus der Inspektion: keine `IncludePrereleases`/`includePrereleases`-Verweise mehr unter `src/` außer dem beabsichtigten Altbestand-Schlüsselliteral `"updates.includePrereleases"` in `AppEinstellungServiceTests_UpdateSettings.cs:155`.
- Kosmetisch (keine Planlücke, ggf. im Dokumentationsschritt oder einem Folgecleanup mitnehmen): Einzelne XML-/Methodenkommentare in `E2E_UpdateSettings.cs` erwähnen noch die alte Checkbox — E-01-Kopf (Zeile 41 ff.: „Prerelease-Checkboxzustände"), E-04-Kommentare (Zeilen 383, 410, 434: „Checkbox aus/an") und der Hilfsmethoden-Kommentar von `SpeichereUpdateEinstellungenUeberUiAsync` (Zeile 1164). Ebenso der Assert-Text „aktuelle Checkbox-Einstellung" in `MainWindowViewModelTests_UpdateSettingsReadFailure.cs:31`.
- `MainTest.General_UpdateStartvarianten_E2E` ruft die beiden umbenannten Szenarien auf; die FlaUI-Methodenzahl ist unverändert (1:1-Umstellung, Projektregel eingehalten).

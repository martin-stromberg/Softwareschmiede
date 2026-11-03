# Umsetzungsplan: Updatepfad (konfigurierbarer Update-Pfad mit exklusivem Update-Kanal)

## Übersicht

Die Update-Funktion erhält einen in den Einstellungen auswählbaren **Update-Pfad** mit genau zwei festen Werten (`Produktivversionen`/`Vorabversionen`), der als reiner **Kanalselektor** dient: Produktiv-Kanal (ausschließlich stabile Versionen) oder RC-Kanal (ausschließlich Prereleases) — niemals gemischt. Das boolesche `IncludePrereleases` (inklusive Semantik) wird vollständig durch den Update-Pfad mit abgeleitetem Kanal ersetzt. Die Update-Quelle (GitHub-Endpunkt, `UpdateOptions.RepositoryOwner`/`RepositoryName`, Request-URI) bleibt unverändert. Betroffen sind das Options-/Einstellungsmodell (`UpdateSettings`, `UpdateCheckOptions`), die Persistenz (`AppEinstellungService`), die Kandidatenfilterung (`GitHubReleaseClient`), die Defensivprüfung plus neue nicht-blockierende Konsistenzwarnung (`UpdateService`, `UpdateCheckResult`, `MainWindowUpdateFlow`), die Settings-UI (`SettingsViewModel`/`SettingsView.xaml`) sowie die zugehörige Unit- und E2E-Testinfrastruktur.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Kanalmodell | Neuer Enum `UpdateKanal` (`Produktiv`, `ReleaseCandidate`) in `UpdateModels.cs`; `IncludePrereleases` wird in `UpdateSettings` und `UpdateCheckOptions` **vollständig entfernt** | Die Anforderung verlangt strikte Exklusivität — ein verbleibendes inklusives `bool` wäre redundant und semantisch widersprüchlich. Record-Gleichheit von `UpdateSettings` bleibt für die Änderungserkennung in `MainWindowUpdateFlow` nutzbar. |
| Pfadwerte | Zwei feste Konstanten `UpdatePfadKanalResolver.ProduktivversionenPfad = "Produktivversionen"` und `UpdatePfadKanalResolver.VorabversionenPfad = "Vorabversionen"` | Vom Anwender festgelegt: genau zwei benannte Pfadwerte `Produktivversionen`/`Vorabversionen`, keine Freitexteingabe. Die Bezeichner stehen zugleich für den Produktiv- bzw. RC-Kanal (`UpdateKanal.Produktiv`/`ReleaseCandidate`). |
| Kanal-Ableitung | Statischer `UpdatePfadKanalResolver` im Application-Layer mit `ErmittleKanal(string? updatePfad)`, `NormalisierePfad(string? updatePfad)`, `IstGueltigerPfad(string? updatePfad)` und `ErmittleKanalFuerTagName(string? tagName)` — keine DI-Registrierung | Pure Functions ohne Abhängigkeiten; dieselben Regeln stehen `AppEinstellungService`, `UpdateService`, `SettingsViewModel` (Kanal-Anzeige) und Tests identisch zur Verfügung. Der Kanal ergibt sich allein aus dem gewählten Pfadwert (keine Marker-/Segment-Analyse); die Release-Klassifikation bleibt beim bestehenden GitHub-`prerelease`-Flag/`SemanticUpdateVersion.IsPrerelease`. |
| `UpdateCheckOptions` | Trägt `UpdatePfad` (`string`, normalisiert) als einziges positionsbasiertes Feld plus abgeleitete Eigenschaft `Kanal` (über `UpdatePfadKanalResolver`) | Single Source of Truth: Der Kanal kann niemals vom Pfad abweichen. Der Pfad wird bis zum `IUpdateReleaseClient` durchgereicht, damit er ins E2E-Protokoll geschrieben werden kann — er adressiert aber **nicht** die Release-Quelle (Request-URI bleibt fest). |
| `UpdateSettings` | `(UpdateMode Modus, string UpdatePfad)` plus abgeleitete Eigenschaft `Kanal` | Gleiche Ableitung wie bei `UpdateCheckOptions`; Pfadänderungen werden durch die bestehende Record-Ungleichheit automatisch als Einstellungsänderung erkannt (Angebot invalidieren, laufenden Ablauf canceln) — der Generations-/Aktualitätsmechanismus bleibt unverändert. |
| Release-Quelle | **Unverändert:** `UpdateOptions.RepositoryOwner`/`RepositoryName`/`AssetName`, die Request-URI in `GitHubReleaseClient` (Zeile 31) und `IsAllowedFollowUpUri` bleiben exakt wie heute | Der Anwender hat verbindlich festgelegt: Der Update-Pfad ist ausschließlich Kanalselektor und parametrisiert **nicht** den GitHub-Endpunkt. Damit entfallen URI-Aufbau, Host-Absicherung und Fremdhost-Validierung aus diesem Plan. |
| Persistenz-Verhalten bei unbekanntem Pfadwert | **Lesen defensiv, Schreiben ablehnend:** `GetUpdateSettingsAsync` normalisiert fehlende/leere/unbekannte gespeicherte Werte auf `Produktivversionen` (gleiches Muster wie die bestehende Default-Rückfallebene für `updates.mode`); `SetUpdateSettingsAsync` lehnt Werte außerhalb der zwei bekannten Pfadwerte per `ArgumentOutOfRangeException` ab (analog `Enum.IsDefined` für `UpdateMode`) | Die UI kann ungültige Werte nicht erzeugen (feste ComboBox); der Schreib-Guard sichert den Service-Vertrag gegen programmatische Aufrufer. Beim Lesen verhindert der Produktiv-Fallback, dass ein korrupt gespeicherter Wert die Prüfung lähmt — konsistent mit der „sanften" Altbestand-Regel. |
| Konsistenzprüfung Installation ↔ Kanal | **Nur warnen, nicht blockieren:** `UpdateService.CheckForUpdateAsync` ermittelt den Installationskanal aus `InstalledVersionInfo.TagName` (`-rc`-Infix, OrdinalIgnoreCase — Konvention von `DatenbankPfadResolver.ReleaseCandidateMarker`). Bei Konflikt zum gewählten Kanal wird das Update trotzdem angeboten; der Konflikthinweis reist als `Message` auf dem `UpdateVerfuegbar`-Ergebnis mit und erscheint über den bestehenden `UpdateHinweis`-Mechanismus im MainWindow | Anwenderentscheid „Nur warnen" — kein `NichtPruefbar`. `UpdateCheckResult.Message` und die `UpdateHinweis`-Anzeige (MainWindow.xaml, `AutomationId="UpdateHinweis"`) sind vorhandene, nicht-blockierende Mechanismen und werden im Versuchsprotokoll (`BeendeVersuch`) mitgeschrieben, was die E2E-Verifikation erlaubt. Strikte Trennung: Die kanalfremde Defensivprüfung bleibt interne Vertragsprüfung (`NichtPruefbar`), die Installationswarnung ist Anwenderhinweis. |
| Prerelease-Klassifikation | Unverändert: `isPrerelease = release.Prerelease \|\| SemanticUpdateVersion.IsPrerelease`, weitergegeben über `UpdateInfo.IsPrerelease` | Bewährte Klassifikation (GitHub-Pre-release-Marker bzw. `-rc.n`-Suffix); nur die Richtung der Filterung ändert sich (exklusiv statt ausschließend). |
| Settings-UI | ComboBox „Update-Pfad" mit zwei festen Optionen nach dem Muster `UpdateModusOption`/`UpdateModusTexte` (neue `UpdatePfadOption`/`UpdatePfadTexte`), plus schreibgeschützte Anzeige des resultierenden Kanals | Anwenderentscheid: feste Auswahl statt Freitext, kein `IDialogService`-Durchsuchen-Dialog. Das Optionsrecord-Muster ist im selben View bereits für den Update-Modus etabliert. |

## Programmabläufe

### Update-Einstellungen laden und anzeigen

1. `SettingsViewModel.LadenAsync` ruft `AppEinstellungService.GetUpdateSettingsAsync` auf; die mit `UpdateSettingsReadTag` getaggte Abfrage liest `updates.mode` und `updates.pfad` in einem Read.
2. Fehlende oder ungültige gespeicherte Werte fallen auf `UpdateMode.NurPruefen` bzw. `Produktivversionen` zurück (Pfadwert über `UpdatePfadKanalResolver.NormalisierePfad` normalisiert); der Schlüssel `updates.includePrereleases` wird nicht mehr gelesen.
3. Das ViewModel setzt `SelectedUpdateMode` und `SelectedUpdatePfad` (über `UpdatePfadOption.FuerPfad`); die schreibgeschützte Anzeige `AbgeleiteterUpdateKanal` folgt der Auswahl live.

Beteiligte Klassen/Komponenten: `SettingsViewModel`, `AppEinstellungService`, `UpdatePfadKanalResolver`, `UpdatePfadOption`, `UpdateSettings`

### Update-Einstellungen speichern

1. `SettingsViewModel.SpeichernAsync` prüft `SelectedUpdatePfad is { Pfad: var updatePfad }` (analog den bestehenden Modus-/Konto-Guards: `null` → `FehlerMeldung`, kein Write). Die ComboBox kann keine anderen Werte erzeugen; eine zusätzliche Format-Validierung ist nicht nötig.
2. `SetUpdateSettingsAsync(new UpdateSettings(updateModus, updatePfad))` validiert `UpdateMode` per `Enum.IsDefined` **und** den Pfad per `UpdatePfadKanalResolver.IstGueltigerPfad` (jeweils `ArgumentOutOfRangeException`), schreibt dann `updates.mode` und `updates.pfad` in einem Speichervorgang (Update-or-Insert).
3. `UpdateSettingsSaved` wird mit dem neuen `UpdateSettings`-Snapshot ausgelöst.

Beteiligte Klassen/Komponenten: `SettingsViewModel`, `AppEinstellungService`, `UpdatePfadKanalResolver`, `UpdateSettings`, `MainWindowViewModel.OnUpdateSettingsSaved`

### Update-Prüfung mit exklusivem Kanal und Konsistenzwarnung

1. `MainWindowUpdateFlow` erzeugt an allen drei Stellen `new UpdateCheckOptions(settings.UpdatePfad)`; `options.Kanal` ergibt sich aus dem Pfad.
2. `UpdateService.CheckForUpdateAsync`: installierte Version via `IApplicationVersionProvider` lesen → `IUpdateReleaseClient.GetLatestReleaseAsync(options)` → Defensivprüfung (interne Vertragsprüfung): `latest.IsPrerelease` muss zum Kanal passen (`ReleaseCandidate` ⇔ `IsPrerelease`), sonst `NichtPruefbar` → `UpdateVersionComparer.IsNewer`-Vergleich.
3. Bei `UpdateVerfuegbar` prüft `UpdateService` zusätzlich die **Konsistenzwarnung**: Installationskanal aus `installed.TagName` (`-rc`-Infix, OrdinalIgnoreCase, via `UpdatePfadKanalResolver.ErmittleKanalFuerTagName`; fehlender `TagName` → konservativ `ReleaseCandidate`, analog `DatenbankPfadResolver`) gegen `options.Kanal`. Konflikt (z. B. RC-Installation mit `Produktivversionen`-Pfad oder produktive Installation mit `Vorabversionen`-Pfad) → Ergebnis bleibt `UpdateVerfuegbar`, erhält aber einen Konflikt-`Message`-Text; zusätzlich `_logger.LogWarning`.
4. `GitHubReleaseClient.GetLatestReleaseAsync` und `TryCreateCandidate`: Request-URI, Pagination und `IsAllowedFollowUpUri` unverändert; `TryCreateCandidate` filtert exklusiv — `Kanal == Produktiv` überspringt alle `isPrerelease`-Kandidaten, `Kanal == ReleaseCandidate` überspringt alle Nicht-Prerelease-Kandidaten. Drafts, ungültige Tags und unpassende Assets bleiben unverändert ausgeschlossen.
5. `MainWindowUpdateFlow.ApplyUpdateCheckResult` setzt `UpdateHinweis` auch im `UpdateVerfuegbar`-Fall auf `result.Message` (bisher hart `null`), sodass die Konfliktwarnung sichtbar wird, ohne das Angebot zu blockieren.

Beteiligte Klassen/Komponenten: `MainWindowUpdateFlow`, `UpdateCheckOptions`, `UpdateService`, `IApplicationVersionProvider`, `GitHubReleaseClient`, `UpdatePfadKanalResolver`, `UpdateCheckResult`, `SemanticUpdateVersion`, `UpdateVersionComparer`

### Einstellungsänderung zur Laufzeit

1. `UpdateSettingsSaved` → `MainWindowViewModel.OnUpdateSettingsSaved` → `MainWindowUpdateFlow.ApplyGespeicherteUpdateSettings`.
2. Record-Ungleichheit (Modus **oder** Pfad geändert) → Generation erhöhen, laufenden Ablauf canceln, Angebot entfernen — unveränderter Mechanismus; Pfadänderungen werden automatisch abgedeckt, weil der Pfad positionsbasierter Record-Bestandteil ist.
3. `PruefeSettingsAktualitaetAsync` liest gespeicherte Werte an den beiden Nachweisgrenzen erneut und bricht bei Ungleichheit ab — ebenfalls automatisch pfadsensitiv.

Beteiligte Klassen/Komponenten: `SettingsViewModel`, `MainWindowViewModel`, `MainWindowUpdateFlow`, `AppEinstellungService`

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `UpdateKanal` | Enum (`Produktiv = 0`, `ReleaseCandidate = 1`) in `UpdateModels.cs` | Exklusiver Update-Kanal, abgeleitet aus dem Update-Pfad |
| `UpdatePfadKanalResolver` | Statische Klasse in `src/Softwareschmiede/Application/Services/Updates/` | Konstanten `ProduktivversionenPfad`/`VorabversionenPfad`; `ErmittleKanal` (Pfad `"Vorabversionen"` → `ReleaseCandidate`, alles andere → `Produktiv`), `NormalisierePfad` (unbekannt/leer → `Produktivversionen`), `IstGueltigerPfad` (nur die zwei bekannten Werte), `ErmittleKanalFuerTagName` (`-rc`-Infix → `ReleaseCandidate`) |
| `UpdatePfadOption` / `UpdatePfadTexte` | Record + statische Klasse in `src/Softwareschmiede.App/ViewModels/` (neue Datei analog `UpdateModusOptionen.cs`) | Feste Auswahlliste der zwei Update-Pfade (`Pfad`-Steuerwert + `Label`); `Alle`, `FuerPfad(string?)` (unbekannt/leer → Produktiv-Option) |

## Änderungen an bestehenden Klassen

### `UpdateSettings` (record, `UpdateModels.cs`)

- **Geänderte Signatur:** `(UpdateMode Modus, bool IncludePrereleases)` → `(UpdateMode Modus, string UpdatePfad)` — `IncludePrereleases` entfällt.
- **Neue Eigenschaften:** `Kanal` (`UpdateKanal`, nicht positionsbasiert) — abgeleitet über `UpdatePfadKanalResolver.ErmittleKanal(UpdatePfad)`; dient Anzeige und Lesbarkeit.

### `UpdateCheckOptions` (record, `UpdateModels.cs`)

- **Geänderte Signatur:** `(bool IncludePrereleases)` → `(string UpdatePfad)` — einziges positionsbasiertes Feld.
- **Neue Eigenschaften:** `Kanal` (`UpdateKanal`, abgeleitet über `UpdatePfadKanalResolver`) — treibt Filterung und Defensivprüfung.

### `UpdateCheckResult` (record, `UpdateModels.cs`)

- **Geänderte Factory:** `UpdateVerfuegbar(UpdateInfo update)` → `UpdateVerfuegbar(UpdateInfo update, string? message = null)` — trägt die optionale, nicht-blockierende Konsistenzwarnung in `Message` (vorhandenes Feld, bisher ungenutzt bei diesem Status).

### `AppEinstellungService` (Klasse)

- **Neue Konstante:** `UpdatePfadKey = "updates.pfad"` analog `UpdateModeKey`.
- **Entfernte Konstante:** `IncludePrereleasesKey` — der Schlüssel `updates.includePrereleases` wird nicht mehr gelesen/geschrieben (stiller Verfall des Altbestands, keine Migration); Tests, die den Altbestand simulieren, verwenden das Schlüsselliteral direkt.
- **Geänderte Methoden:** `GetUpdateSettingsAsync` — `keys`-Array der getaggten Abfrage auf `updates.mode` + `updates.pfad` umstellen; Pfadwert über `UpdatePfadKanalResolver.NormalisierePfad` normalisieren (fehlend/leer/unbekannt → `Produktivversionen`).
- **Geänderte Methoden:** `SetUpdateSettingsAsync` — zusätzliche Pfad-Validierung per `UpdatePfadKanalResolver.IstGueltigerPfad` (`ArgumentOutOfRangeException`, kein Write); schreibt `updates.mode` + `updates.pfad` in einem Speichervorgang.
- **Unverändert:** `UpdateSettingsReadTag`, Update-or-Insert-Mechanik, `Enum.IsDefined`-Validierung für `UpdateMode`.

### `GitHubReleaseClient` (Klasse)

- **Geänderte Methoden:** `TryCreateCandidate` — Filter von „Prereleases bei `!IncludePrereleases` ausschließen" auf exklusiven Kanal: `Produktiv` überspringt `isPrerelease`-Kandidaten, `ReleaseCandidate` überspringt Nicht-Prerelease-Kandidaten.
- **Unverändert:** `GetLatestReleaseAsync`-Request-URI (`api.github.com/repos/{RepositoryOwner}/{RepositoryName}/releases`), Pagination, `IsAllowedFollowUpUri`, `UpdateOptions`-Bindung — der Pfad parametrisiert die Quelle nicht.

### `UpdateService` (Klasse)

- **Geänderte Methoden:** `CheckForUpdateAsync` — Defensivprüfung (Zeilen 51–54) auf Kanalsemantik: `latest.IsPrerelease` widerspricht `options.Kanal` (Prerelease im Produktiv-Kanal **oder** Stabiles im RC-Kanal) → `NichtPruefbar` (interne Vertragsverletzung, bleibt).
- **Neue Prüfung:** Konsistenzwarnung Installation ↔ Kanal: `UpdatePfadKanalResolver.ErmittleKanalFuerTagName(installed.TagName)` gegen `options.Kanal`; Konflikt → `UpdateVerfuegbar(latest, konfliktHinweis)` mit Warn-`Message` (nicht blockierend) plus `_logger.LogWarning`. Wirkt nur dort, wo ein Update angeboten wird — `KeinUpdate`/`NichtPruefbar` bleiben warnfrei.

### `MainWindowUpdateFlow` (Klasse)

- **Geänderte Methoden:** `InitializeAfterWindowReadyAsync` (Zeile 173), `PruefenAsync` (Zeile 198), `StartenAsync` (Zeile 220) — `new UpdateCheckOptions(settings.UpdatePfad)` statt `new UpdateCheckOptions(settings.IncludePrereleases)`.
- **Geänderte Methoden:** `ApplyUpdateCheckResult` — `UpdateHinweis = result.Message` statt hart `null` im `UpdateVerfuegbar`-Zweig (Zeile 419), damit die Konsistenzwarnung angezeigt wird, ohne das Angebot zu verändern.
- **Unverändert:** `ApplyGespeicherteUpdateSettings`, `PruefeSettingsAktualitaetAsync`, Generations-/Aktualitätsmechanismus (profitiert automatisch von Record-Gleichheit).

### `SettingsViewModel` (Klasse)

- **Entfernte Eigenschaften:** `IncludePrereleases` (`bool`) samt Feld `_includePrereleases`.
- **Neue Eigenschaften:** `UpdatePfadOptionen` (`IReadOnlyList<UpdatePfadOption>`), `SelectedUpdatePfad` (`UpdatePfadOption?`), `AbgeleiteterUpdateKanal` (schreibgeschützter Anzeigetext des aus der Auswahl resultierenden Kanals über `UpdatePfadTexte`; Benachrichtigung bei `SelectedUpdatePfad`-Änderung).
- **Geänderte Methoden:** `LadenAsync` — `SelectedUpdatePfad = UpdatePfadOption.FuerPfad(updateSettings.UpdatePfad)`; `SpeichernAsync` — Guard `SelectedUpdatePfad is { Pfad: var updatePfad }` (Fehlermeldung bei `null`), `new UpdateSettings(updateModus, updatePfad)`; `VerwerfenAsync` unverändert (lädt neu).
- **Unverändert:** `UpdateSettingsSaved`-Event (Argumenttyp `UpdateSettings` bleibt), Commands.

### `SettingsView` (XAML)

- **Neu:** ComboBox „Update-Pfad" (`AutomationProperties.Name="Update-Pfad"`, `ItemsSource` ↔ `UpdatePfadOptionen`, `SelectedItem` ↔ `SelectedUpdatePfad`, `Label`-ItemTemplate wie beim Update-Modus) und schreibgeschützte Kanal-Anzeige (TextBlock, `AutomationProperties.Name="Update-Kanal"`, `Text` ↔ `AbgeleiteterUpdateKanal`).
- **Entfernt:** CheckBox „Prerelease-Versionen laden" (Zeilen 453–460) samt Hinweistext; Hinweistext zur exklusiven Kanalsemantik neu formulieren.
- **Kein** Durchsuchen-Dialog und keine Freitext-Eingabe.

### `ProtokollierenderUpdateService` (E2E-Dekorator)

- **Geänderte Methoden:** `CheckForUpdateAsync` — protokolliert `UpdateCheckStarted` mit `updatePfad` und `kanal` statt `includePrereleases` (Zeile 32); `UpdateCheckCompleted` unverändert.
- Betroffenes Ereignisschema: `UpdateE2EEreignisse`/Protokoll-Datenfeld `includePrereleases` wird durch `updatePfad` + `kanal` ersetzt — alle Protokollauswertungen in Fixture/Tests mitziehen.

## Datenbankmigrationen

Keine. Die Update-Einstellungen liegen als Key-Value-Paare in der `AppEinstellungen`-Tabelle; der neue Schlüssel `updates.pfad` wird einfach hinzugefügt. Der Altbestand `updates.includePrereleases` verfällt still (wird nicht mehr gelesen/geschrieben, keine Migration).

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `UpdatePfad` (Settings-UI) | Auswahl aus genau zwei festen Optionen (`Produktivversionen`/`Vorabversionen`) über `UpdatePfadOption.Alle`; `SelectedUpdatePfad == null` → Speichern blockiert mit `FehlerMeldung` (analog `SelectedUpdateMode`-Guard) | Kein Freitext möglich; `null`-Auswahl → Fehlermeldung, kein Write |
| `UpdatePfad` (`SetUpdateSettingsAsync`, Service-Guard) | Nur `UpdatePfadKanalResolver.IstGueltigerPfad` (Wert ∈ {`Produktivversionen`, `Vorabversionen`}, OrdinalIgnoreCase, Trim) | `ArgumentOutOfRangeException`, kein Write |
| `UpdatePfad` (`GetUpdateSettingsAsync`, Lesepfad) | Fehlende, leere oder unbekannte gespeicherte Werte → `NormalisierePfad` → `Produktivversionen` (Produktiv-Default, „sanft") | Kein Fehler — defensiver Fallback wie bei `updates.mode` |
| `UpdateMode` | Unverändert: `Enum.IsDefined` in `SetUpdateSettingsAsync` | `ArgumentOutOfRangeException`, kein Write |

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `updates.pfad` (`AppEinstellungService.UpdatePfadKey`) | `string` (Key-Value-Tabelle) | `Produktivversionen` (bei fehlendem/leerem/unbekanntem Wert) | Gewählter Update-Pfad; reiner Kanalselektor |
| `updates.includePrereleases` | — | — | Entfällt: verfällt still, wird nicht mehr gelesen/geschrieben (Altbestand bleibt ungenutzt in der DB) |

`UpdateOptions` und die DI-Registrierung in `App.xaml.cs` bleiben vollständig unverändert (`RepositoryOwner`/`RepositoryName`-Defaults, statischer Resolver — keine neuen Services).

## Seiteneffekte und Risiken

- **Kompilierbruch an allen Erzeugungsstellen:** `new UpdateSettings(modus, bool)` und `new UpdateCheckOptions(bool)` existieren in Produktivcode (`SettingsViewModel`, `MainWindowUpdateFlow`), Testdoubles (`UpdateE2EFixture.SetzeUpdateEinstellungenAsync`, `MainWindowViewModelUpdateTestBase`) und zahlreichen Unit-Tests — alle müssen in einem Rutsch umgestellt werden.
- **E2E-Protokollvertrag:** Das `UpdateCheckStarted`-Datenfeld `includePrereleases` wird durch `updatePfad`/`kanal` ersetzt; Protokollauswertungen (`DatenBool`-Hilfen, `UpdateE2EProtokoll`-Assertions) müssen mitziehen.
- **Geänderte Update-Kette:** Im RC-Kanal ist eine stabile `X.Y.Z` kein zulässiges Update mehr, obwohl semantisch neuer als `X.Y.Z-rc.n` — dokumentiertes Verhalten (`business-rules.md`) und Tests (`UpdateServiceTests_PrereleaseChain`) ändern sich fachlich.
- **Bestandsinstallationen:** Fehlender `updates.pfad`-Wert bedeutet `Produktivversionen` (bisheriges Verhalten mit `IncludePrereleases = false`). RC-Bestandsinstallationen mit Default-Pfad erhalten weiterhin produktive Update-Angebote — jetzt allerdings **mit sichtbarer Konfliktwarnung** statt still, was das im Issue beschriebene Mischrisiko transparent macht ohne zu blockieren.
- **Konsistenzwarnung nur im Angebotsfall:** Die Warnung erscheint, wenn ein Update verfügbar ist; bei `KeinUpdate`/`NichtPruefbar` wird nicht gewarnt — bewusste Entscheidung, da nur das Angebot das Risiko birgt.
- **Dokumentation:** `docs/help/programmupdate/*` (Business Rules „Prerelease-Regeln", Update-Einstellungen) und `docs/help/einstellungen/*` müssen im Dokumentationsschritt an die Pfad-/Kanalsemantik angepasst werden.

## Umsetzungsreihenfolge

1. **`UpdateKanal`-Enum und `UpdatePfadKanalResolver` anlegen**
   - Voraussetzungen: Keine.
   - Beschreibung: Enum mit `Produktiv`/`ReleaseCandidate` in `UpdateModels.cs`; statischer Resolver mit Pfad-Konstanten, `ErmittleKanal`, `NormalisierePfad`, `IstGueltigerPfad`, `ErmittleKanalFuerTagName`.

2. **`UpdateSettings`, `UpdateCheckOptions` und `UpdateCheckResult` umstellen**
   - Voraussetzungen: Schritt 1 (Resolver für die abgeleitete `Kanal`-Eigenschaft).
   - Beschreibung: `IncludePrereleases` entfernen, `UpdatePfad` als positionsbasiertes Feld einführen, abgeleitete `Kanal`-Eigenschaft ergänzen; `UpdateVerfuegbar`-Factory um optionalen `message`-Parameter erweitern. Ab hier kompiliert der Bestand nicht mehr — die Folgeschritte schließen die Bruchstellen.

3. **`AppEinstellungService` um neuen Schlüssel erweitern**
   - Voraussetzungen: Schritt 2 (neues `UpdateSettings`-Modell).
   - Beschreibung: `UpdatePfadKey` deklarieren, `IncludePrereleasesKey` entfernen; getaggte Leseabfrage (`NormalisierePfad`-Fallback) und gemeinsamer Write (`IstGueltigerPfad`-Guard) auf `updates.mode` + `updates.pfad` umstellen.

4. **`GitHubReleaseClient.TryCreateCandidate` auf exklusiven Kanal umstellen**
   - Voraussetzungen: Schritt 2.
   - Beschreibung: Filter exklusiv je `options.Kanal`; Request-URI, Pagination und `IsAllowedFollowUpUri` unverändert lassen.

5. **`UpdateService.CheckForUpdateAsync` anpassen**
   - Voraussetzungen: Schritt 2.
   - Beschreibung: Defensivprüfung auf Kanalsemantik umstellen (beide Richtungen → `NichtPruefbar`); nicht-blockierende Konsistenzwarnung Installation↔Kanal als `Message` auf `UpdateVerfuegbar` ergänzen.

6. **`MainWindowUpdateFlow`-Aufrufstellen und Hinweisanzeige umstellen**
   - Voraussetzungen: Schritt 2.
   - Beschreibung: Drei `new UpdateCheckOptions(...)`-Erzeugungen auf `settings.UpdatePfad` umstellen; `ApplyUpdateCheckResult` zeigt `result.Message` auch bei `UpdateVerfuegbar`.

7. **`UpdatePfadOption`/`UpdatePfadTexte` anlegen, `SettingsViewModel` und `SettingsView.xaml` erweitern**
   - Voraussetzungen: Schritt 1 (Kanal-Anzeige), Schritt 2 (neues `UpdateSettings`), Schritt 3 (Persistenz).
   - Beschreibung: Optionsrecord + Texte analog `UpdateModusOptionen`; `SelectedUpdatePfad` + schreibgeschützte `AbgeleiteterUpdateKanal`-Anzeige; `IncludePrereleases` entfernen; Laden/Speichern anpassen; XAML: ComboBox + Kanal-Anzeige ergänzen, Checkbox entfernen.

8. **E2E-Testdoubles umstellen**
   - Voraussetzungen: Schritte 2 und 4.
   - Beschreibung: `ProtokollierenderUpdateService` (`updatePfad`/`kanal` statt `includePrereleases`), `UpdateE2EEreignisse`-Datenfelder; `UpdateE2ETestConfiguration`/`UpdateE2ESzenario` bleiben unverändert (die Release-URL ist weiterhin fix).

9. **Unit-Tests anpassen und neue schreiben**
   - Voraussetzungen: Schritte 1–7.
   - Beschreibung: Alle bool-basierten Erzeugungen umstellen; neue Resolver-, Filter- (beide Kanäle exklusiv), Persistenz-, Warnungs-, ViewModel- und Hinweis-Tests gemäß Testtabelle.

10. **E2E-Fixture, Page-Object und E2E-Tests umstellen**
    - Voraussetzungen: Schritte 7 und 8.
    - Beschreibung: `UpdateE2EFixture`-Hilfen auf Pfadparameter, `SettingsView`-Page-Object auf ComboBox/Kanal-Anzeige, bestehende Szenarien konsolidiert auf die Pfad-/Kanalsemantik umstellen (keine zusätzlichen FlaUI-Testmethoden — Projektregel).

11. **Dokumentation anpassen**
    - Voraussetzungen: Abgeschlossene Implementierung.
    - Beschreibung: `docs/help/programmupdate/*` und `docs/help/einstellungen/*` im Dokumentationsschritt des Workflows aktualisieren.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `ErmittleKanal_…` (Theory) | `UpdatePfadKanalResolverTests` (neu) | `"Vorabversionen"` (inkl. OrdinalIgnoreCase-Varianten) → `ReleaseCandidate`; `"Produktivversionen"`, `null`, leer, unbekannte Werte → `Produktiv` |
| `NormalisierePfad`/`IstGueltigerPfad` (Theory) | `UpdatePfadKanalResolverTests` | Normalisierung unbekannter/leerer Werte auf `Produktivversionen`; `IstGueltigerPfad` akzeptiert nur die zwei bekannten Werte |
| `ErmittleKanalFuerTagName_…` (Theory) | `UpdatePfadKanalResolverTests` | `v1.2.3-rc.1`/`V1.2.3-RC.1` → `ReleaseCandidate`; `v1.2.3` → `Produktiv`; `null`/leer → `ReleaseCandidate` (konservativ wie `DatenbankPfadResolver`) |
| `GetLatestReleaseAsync_FiltersExclusivelyByChannel` (Theory) | `GitHubReleaseClientTests_Filters` | Produktiv-Kanal: nur stabile Kandidaten (RCs werden übersprungen, auch wenn höher); RC-Kanal: nur `isPrerelease`-Kandidaten (Stabile werden übersprungen, auch wenn höher) — Gegenstück zum heutigen `FiltersByOptions` |
| `CheckForUpdateAsync_RejectsChannelMismatch` (Theory) | `UpdateServiceTests_Options` | Defensivprüfung beider Richtungen: `IsPrerelease`-Ergebnis im Produktiv-Kanal **und** stabiles Ergebnis im RC-Kanal → `NichtPruefbar` |
| `CheckForUpdateAsync_WarnsOnInstallationChannelConflict` (Theory) | `UpdateServiceTests` (oder neue Teilklasse) | RC-Installation (`tagName` mit `-rc`) + `Produktivversionen`-Pfad bzw. produktive Installation + `Vorabversionen`-Pfad → Ergebnis bleibt `UpdateVerfuegbar` mit gesetztem Konflikt-`Message`-Text (kein `NichtPruefbar`, Releaseabruf erfolgt normal); passender Kanal → `Message` leer |
| `GetUpdateSettingsAsync_…Pfad` | `AppEinstellungServiceTests_UpdateSettings` | Pfad persistiert/lädt über Scopes; fehlender/unbekannter Schlüssel → `Produktivversionen`; `SetUpdateSettingsAsync` lehnt unbekannten Pfad per `ArgumentOutOfRangeException` ab; gespeicherter `updates.includePrereleases`-Altbestand wird ignoriert |
| `UpdateSettings_Pfad…` | `SettingsViewModelTests` | Speichern/Laden/Verwerfen des Pfads; `UpdateSettingsSaved` mit Pfad-Snapshot; `AbgeleiteterUpdateKanal` folgt der Auswahl; `SelectedUpdatePfad = null` blockiert Speichern |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `GitHubReleaseClientTests_Filters` (`FiltersByOptions`, `FiltersGitHubPrereleaseFlag`, `FiltersSemVerPrereleaseSuffix`, `PreservesPrereleaseVersion`, `SkipsDraftInvalidTagAndUnusableAssetEntries`) | `UpdateCheckOptions(bool)` → `UpdateCheckOptions(string)`; inklusive Filtersemantik → exklusiver Kanal (Assert-Richtung für RC-Kanal umkehren) |
| `GitHubReleaseClientTests` (`ShouldReturnRelease_WhenAssetExists`, `ShouldReturnRelease_WhenReleaseIsNotUsable`, `…ResponseIsNotAReleaseList`, `…RequestTimesOut`) | Options-Signatur; Request-URI-Erwartungen unverändert (Endpunkt wird nicht parametrisiert) |
| `GitHubReleaseClientTests_Pagination` (alle Methoden) | Options-Signatur; `IsAllowedFollowUpUri` und `Page1Url`/`Page2Url` bleiben unverändert |
| `UpdateServiceTests` / `UpdateServiceTests_Options` | Options-Signatur; `PropagatesCurrentOptions` auf Pfadwechsel (`Produktivversionen` ↔ `Vorabversionen`) umstellen; Defensivprüfungs-Assert auf beide Kanäle erweitern |
| `UpdateServiceTests_PrereleaseChain` | Fachlich geändert: Bei installiertem `1.3.0-rc.1` im RC-Kanal gilt `1.3.0` stabil **nicht** mehr als Update; nur `1.3.0-rc.2`+ ist zulässig |
| `AppEinstellungServiceTests_UpdateSettings` (alle Methoden) | `UpdateSettings`-Signatur; Defaults jetzt `NurPruefen`/`Produktivversionen`; Altbestand-Schlüssel ignorieren; neuer `ArgumentOutOfRangeException`-Fall für unbekannten Pfad |
| `SettingsViewModelTests` (Update-Methoden ~Zeilen 495–612) | Checkbox → Pfad-ComboBox; Theory-Matrix Modus × Pfad (`Produktivversionen`/`Vorabversionen`); Event-Snapshot |
| `MainWindowViewModelTests_UpdateStartup`, `MainWindowViewModelTests_UpdateSettingsReadFailure`, `MainWindowViewModelUpdateTestBase` (`SpeichereUpdateEinstellungenUeberUiAsync`, `SetUpdateSettingsAsync`) | `UpdateSettings`-/`UpdateCheckOptions`-Signaturen; UI-Speicherweg führt über die Pfad-ComboBox statt der Checkbox |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Update-Pfad über echte UI auswählen (`Produktivversionen` + `Vorabversionen`), speichern, über echten Neustart persistiert sehen; resultierender Kanal wird angezeigt; „Verwerfen" verwirft die Auswahl | `E2E_UpdateSettings.cs` — `Settings_AllModesAndPrereleasesPersist` umbenennen/umstellen zu `Settings_AllModesAndUpdatePfadPersist` (E-01, Methodenzahl bleibt) | Update-Pfad in Einstellungen pflegbar; Persistenz; Kanal-Anzeige |
| Exklusive Kanalauswahl: `Vorabversionen`-Pfad installiert ausschließlich RC-Asset, `Produktivversionen`-Pfad ausschließlich Stable-Asset — inkl. Nachweis, dass im RC-Kanal eine höhere stabile Version **nicht** gewählt wird (Installationsartefakte: Download-URL, entpackte `version.json`, Skriptziel); Protokollnachweis `updatePfad`/`kanal`; **verkettet am selben App-Prozess:** Konfliktfall produktive Installation + `Vorabversionen`-Pfad → Update wird angeboten **und** `UpdateHinweis` zeigt die Konfliktwarnung (nicht blockierend, `BeendeVersuch`-Hinweis im Protokoll) | `E2E_UpdateSettings.cs` — `PrereleaseCheckbox_SelectsMatchingAsset` umstellen zu `UpdatePfad_SelectsExclusiveChannelAsset` (E-03, Methodenzahl bleibt) | Strikte Exklusivität beider Kanäle + nicht-blockierende Konsistenzwarnung Ende-zu-Ende |

Beide Szenarien ersetzen bestehende Methoden 1:1 statt neue zu addieren (Projektregel: FlaUI-Methodenzahl minimieren, Abdeckung nicht reduzieren). Der Warnpfad wird in E-03 an derselben App-Instanz verkettet — die Fixture-Installation ist produktiv (`InstallierteVersion="1.2.0"`), sodass der `Vorabversionen`-Pfad dort direkt den Konfliktfall erzeugt.

Bestehende E2E-Tests, die angepasst werden müssen:

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `E2E_UpdateSettings.cs` (E-01, E-03, sowie alle `SpeichereUpdateEinstellungenUeberUiAsync`-Aufrufe in E-02, E-04, E-07) | Checkbox-Bedienung → Pfad-ComboBox; `includePrereleases`-Protokollnachweis → `updatePfad`/`kanal` |
| `E2E_UpdateFixture.cs` (`Fixture_UsesIsolatedRealUpdatePipeline`) | RC-Auswahl über `includePrereleases: true` → `Vorabversionen`-Pfad; Protokoll-Datenfelder |
| `UpdateE2EFixture` (`SetzeUpdateEinstellungenAsync`, `AktualisiereUpdateEinstellungenAsync`) | Signatur `(modus, includePrereleases)` → `(modus, pfad)`; `ReleaseApiUrl`/`SetzeStandardAntworten` unverändert (Endpunkt fix) |
| Page-Object `SettingsView` (`GetIncludePrereleases`/`SetIncludePrereleases`) | Neue Elemente: `GetUpdatePfad`/`SetUpdatePfad(label)` (ComboBox-Bedienung analog `SetUpdateMode`), `GetAbgeleitetenUpdateKanal` (Kanal-Anzeige `AutomationProperties.Name="Update-Kanal"`); Checkbox-Elemente entfernen |
| `ProtokollierenderUpdateService`, `UpdateE2EEreignisse`, `UpdateE2EProtokoll`-Auswertungen | Datenfeld `includePrereleases` → `updatePfad`/`kanal` |

## Offene Punkte

Keine.

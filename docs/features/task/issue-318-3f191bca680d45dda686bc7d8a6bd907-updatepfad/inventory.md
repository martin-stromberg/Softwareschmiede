# Bestandsaufnahme: Updatepfad (Issue #318)

Bestandsaufnahme des bestehenden Update-Mechanismus, der Update-Einstellungspersistenz, der Settings-UI sowie der zugehörigen Unit- und E2E-Tests — bezogen auf die Anforderung „konfigurierbarer Update-Pfad mit exklusivem Update-Kanal (produktiv vs. RC)" aus `requirement.md`.

## Zusammenfassung

- **Vollständig vorhandener Update-Stack:** `IUpdateService`/`UpdateService` (Orchestrierung), `IUpdateReleaseClient`/`GitHubReleaseClient` (Release-Abfrage inkl. Pagination und Prerelease-Filter), `IUpdatePackageService`, `IUpdateScriptService`, `IApplicationVersionProvider` (liest `version.json`). Die Prerelease-Steuerung läuft heute ausschließlich über das boolesche `IncludePrereleases` auf `UpdateCheckOptions` und `UpdateSettings` — inklusive Semantik (stabile + RC konkurrieren im SemVer-Vergleich), nicht exklusiv.
- **Kein Update-Pfad, kein Kanal vorhanden:** Es existiert weder ein Einstellungsschlüssel `updates.pfad` o. ä. noch ein Kanal-Enum oder eine Pfad→Kanal-Ableitung. Die Update-Quelle ist fest verdrahtet: `UpdateOptions` trägt `RepositoryOwner`/`RepositoryName`/`AssetName` als `init`-Defaults, die Request-URI `https://api.github.com/repos/...` steht hartcodiert in `GitHubReleaseClient` (Zeile 31).
- **Persistenz-Infrastruktur vorhanden und erweiterbar:** `AppEinstellungService` speichert Update-Einstellungen als Key-Value-Paare (`updates.mode`, `updates.includePrereleases`) in einem gemeinsamen, mit `UpdateSettingsReadTag` getaggten Read bzw. einem gemeinsamen Write — neue Schlüssel benötigen keine Migration.
- **Settings-UI und Laufzeit-Aktualität vorhanden:** `SettingsViewModel` bindet Modus (ComboBox) und `IncludePrereleases` (Checkbox) und publiziert `UpdateSettingsSaved`; `MainWindowUpdateFlow` konsumiert den Snapshot, invalidiert bei Record-Ungleichheit Angebot und laufende Versuche (Generations-/Aktualitätsmechanismus) und erzeugt an drei Stellen `new UpdateCheckOptions(settings.IncludePrereleases)`.
- **Installationstrennung bereits umgesetzt:** `DatenbankPfadResolver` unterscheidet RC- (`-rc` im `tagName` → Programmverzeichnis-DB) und produktive Versionen (`%LocalAppData%`-DB) — der fachliche Grund für die geforderte Kanaltrennung.
- **Breite, eng gekoppelte Testabdeckung:** Unit-Tests für Filtersemantik, Optionsweitergabe, Prerelease-Kette, Settings-Persistenz und ViewModel-Verhalten sowie eine umfangreiche E2E-Fixture (`UpdateE2EFixture`, `UpdateE2ETestKontext`, `UpdateFixtureHttpMessageHandler`, `UpdateSettingsReadFailureInterceptor`, `ProtokollierenderUpdateService`), die `includePrereleases` im Protokoll mitschreibt und die Checkbox über das Page-Object `SettingsView` bedient. Alle diese Stellen referenzieren das boolesche Modell.

## Details

- [Datenmodell](inventory/models.md) — Records/Optionsklassen in `UpdateModels.cs`, `UpdateOptions`, `UpdateModusOption`, E2E-Szenariomodelle
- [Logik](inventory/logic.md) — `UpdateService`, `GitHubReleaseClient`, `AppEinstellungService`, `SettingsViewModel`, `MainWindowUpdateFlow`, `DatenbankPfadResolver`, `ApplicationVersionProvider`, `UpdateVersionComparer`, `SemanticUpdateVersion`, DI-Registrierung
- [Enums](inventory/enums.md) — `UpdateMode`, `UpdateCheckStatus`, `UpdatePreparationPhase`
- [Interfaces](inventory/interfaces.md) — Update-Contracts in `UpdateInterfaces.cs` und App-seitige Test-/Dialog-Contracts
- [Tests](inventory/tests.md) — Unit-Tests, MainWindowViewModel-Tests, E2E-Tests, Page-Object und Testdoubles

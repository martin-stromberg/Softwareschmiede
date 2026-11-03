# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### E2E_UpdateFixture.cs (End2EndTest)

- **Namenskonventionen und Einheitlichkeit** — Veralteter Kommentar in Zeile 89: `// RC-Auswahl bei aktivierten Prereleases über echten, DB-erhaltenden Neustart.` beschreibt noch die entfernte `IncludePrereleases`-Option; der nachfolgende Code setzt inzwischen `UpdatePfadKanalResolver.VorabversionenPfad`. Rest der Iteration-1-Behebung (gleiche Befundklasse wie die dort gemeldeten „Checkbox"-Kommentare).

  Empfehlung: Kommentar auf die neue Steuerung umformulieren, z. B. „RC-Auswahl über den Vorabversionen-Pfad über echten, DB-erhaltenden Neustart."

### UpdateModels.cs (UpdateSettings)

- **Toter Code** — Die abgeleitete Eigenschaft `UpdateSettings.Kanal` (Zeile 58) wird im gesamten Branch nirgends aufgerufen: `MainWindowUpdateFlow` übergeben `settings.UpdatePfad` an `new UpdateCheckOptions(...)`, und alle Kanal-Logik nutzt `UpdateCheckOptions.Kanal` bzw. `UpdatePfadKanalResolver.ErmittleKanal` direkt.

  Empfehlung: Die Eigenschaft entfernen (oder an einer bestehenden Aufrufstelle tatsächlich verwenden), damit die öffentliche Oberfläche des Records nur genutzte Member enthält.

### AppEinstellungService.cs (AppEinstellungService)

- **Namenskonventionen und Einheitlichkeit** — `SetUpdateSettingsAsync` persistiert in Zeile 431 `settings.UpdatePfad.Trim()` (Roheingabe), obwohl die Validierung über `UpdatePfadKanalResolver.IstGueltigerPfad` Groß-/Kleinschreibung und Leerzeichen toleriert und `GetUpdateSettingsAsync` beim Lesen über `NormalisierePfad` immer die kanonische Form liefert. Damit kann derselbe logische Pfad mit unterschiedlicher Schreibweise in der Datenbank landen, und ein Schreib-Lese-Roundtrip liefert nicht den geschriebenen Wert (z. B. `SetUpdateSettingsAsync` mit `" vorabversionen "` speichert `"vorabversionen"`, `GetUpdateSettingsAsync` liefert `"Vorabversionen"`).

  Empfehlung: Beim Schreiben die kanonische Form persistieren: `(UpdatePfadKey, UpdatePfadKanalResolver.NormalisierePfad(settings.UpdatePfad))`.

### UpdatePfadKanalResolver.cs (UpdatePfadKanalResolver)

- **Doppelter Code** — Die RC-Klassifikation von Release-Tags existiert nun zweimal: `ReleaseCandidateMarker = "-rc"` plus `tagName.Contains(marker, StringComparison.OrdinalIgnoreCase)` in `UpdatePfadKanalResolver` (Zeilen 17, 64) und identisch in `DatenbankPfadResolver` (`src/Softwareschmiede/Infrastructure/Data/DatenbankPfadResolver.cs`, Zeilen 16, 75). Der XML-Kommentar verweist selbst auf die Parallele („gleiche Regel wie DatenbankPfadResolver"). Bei einer Änderung der Tag-Konvention müssten zwei Stellen synchron geändert werden.

  Empfehlung: Die Regel an einer Stelle zentralisieren — `DatenbankPfadResolver` liegt in Infrastructure und darf `Softwareschmiede.Application.Services.Updates` referenzieren; seine private `IstProduktiveVersion`-Prüfung kann `UpdatePfadKanalResolver.ErmittleKanalFuerTagName(tagName) == UpdateKanal.Produktiv` verwenden, statt die Markerkonstante und den Vergleich zu duplizieren.

## Geprüfte Dateien

Liste aller geprüften Dateien:
- `src/Softwareschmiede.App/Services/Testing/ProtokollierenderUpdateService.cs`
- `src/Softwareschmiede.App/ViewModels/MainWindowUpdateFlow.cs`
- `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/UpdatePfadOptionen.cs`
- `src/Softwareschmiede.App/Views/SettingsView.xaml`
- `src/Softwareschmiede/Application/Services/AppEinstellungService.cs`
- `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs`
- `src/Softwareschmiede/Application/Services/Updates/UpdatePfadKanalResolver.cs`
- `src/Softwareschmiede/Application/Services/Updates/UpdateService.cs`
- `src/Softwareschmiede/Infrastructure/Services/Updates/GitHubReleaseClient.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests_UpdateSettingsReadFailure.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests_UpdateStartup.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelUpdateTestBase.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/SettingsViewModelTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/AppEinstellungServiceTests_UpdateSettings.cs`
- `src/Softwareschmiede.Tests/Application/Services/Updates/UpdatePfadKanalResolverTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/Updates/UpdateServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/Updates/UpdateServiceTests_Options.cs`
- `src/Softwareschmiede.Tests/Application/Services/Updates/UpdateServiceTests_PrereleaseChain.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_UpdateFixture.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_UpdateSettings.cs`
- `src/Softwareschmiede.Tests/E2E/MainTest.cs`
- `src/Softwareschmiede.Tests/E2E/ScmE2EFixture.cs`
- `src/Softwareschmiede.Tests/E2E/UpdateE2EFixture.cs`
- `src/Softwareschmiede.Tests/E2E/Views/SettingsView.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Services/GitHubReleaseClientTests.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Services/GitHubReleaseClientTests_Filters.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Services/GitHubReleaseClientTests_Pagination.cs`

# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### E2E_UpdateSettings.cs (End2EndTest)

- **Namenskonventionen und Einheitlichkeit** — Veraltete Kommentare/Dokumentation: Mehrere Kommentare und XML-Docs beschreiben noch die entfernte Prerelease-CheckBox statt der neuen „Update-Pfad"-ComboBox: Klassen-Doc Zeilen 16–17 („Prerelease-Auswahl", „Asset-Auswahl je Checkboxzustand"), Methoden-Doc E-01 Zeile 41 („beide Prerelease-Checkboxzustände"), Methoden-Doc E-04 Zeile 383 („Checkbox aus"), Inline-Kommentare Zeile 410 („Checkbox aus + speichern") und Zeile 434 („Checkbox wieder an") sowie die Doc von `SpeichereUpdateEinstellungenUeberUiAsync` Zeile 1164 („Update-Modus und Prerelease-Checkbox").

  Empfehlung: Alle Stellen auf die neue Steuerung umformulieren (z. B. „Update-Pfad", „Vorabversionen-/Produktivversionen-Auswahl", „beide Update-Pfade").

### MainWindowViewModelTests_UpdateSettingsReadFailure.cs / MainWindowViewModelTests_UpdateStartup.cs (End2EndTest-nahe ViewModel-Tests)

- **Namenskonventionen und Einheitlichkeit** — Veraltete „Checkbox"-Begriffe in Kommentaren/Assertions, obwohl es die CheckBox nicht mehr gibt: `MainWindowViewModelTests_UpdateSettingsReadFailure.cs` Zeile 124 („die Folgeprüfung muss die aktuelle Checkbox-Einstellung übergeben"), `MainWindowViewModelTests_UpdateStartup.cs` Zeile 160 („übergibt beim Folgeaufruf die aktuelle Checkbox-Einstellung") und Zeile 198 („Folgeprüfung verwendet die aktuelle Einstellung (Checkbox jetzt aus)").

  Empfehlung: „Checkbox-Einstellung" durch „Update-Pfad-Einstellung" bzw. „Pfad-Auswahl" ersetzen.

### UpdateE2EFixture.cs (UpdateE2EFixture)

- **Doppelter Code** — `SetzeUpdateEinstellungenAsync` (Zeilen 238–248) und `AktualisiereUpdateEinstellungenAsync` (Zeilen 256–265) sind nahezu identisch; einziger Unterschied ist der `db.Database.MigrateAsync()`-Aufruf. DbContextOptions-Aufbau, DbContext-Erzeugung, AppEinstellungService-Instanziierung und `SetUpdateSettingsAsync`-Aufruf sind doppelt.

  Empfehlung: Gemeinsame private Methode auslagern, z. B. `SchreibeUpdateEinstellungenAsync(UpdateMode modus, string updatePfad, bool migriere)`, die von beiden öffentlichen Methoden aufgerufen wird.

### UpdatePfadOptionen.cs (UpdatePfadTexte)

- **Doppelter Code** — `UpdatePfadTexte.Produktivversionen` und `UpdatePfadTexte.Vorabversionen` duplizieren die Literale von `UpdatePfadKanalResolver.ProduktivversionenPfad` bzw. `VorabversionenPfad` (jeweils identischer String). Damit existieren zwei Quellen für denselben persistierten Wert: `UpdatePfadOption.Alle` nutzt den Resolver für `Pfad` und `UpdatePfadTexte` für `Label`; der E2E-Test `PruefeUpdateUeberUiAsync` (E2E_UpdateSettings.cs, Zeile 1199) vergleicht das protokollierte `updatePfad` sogar gegen die Label-Konstante — das funktioniert nur, solange beide Literale zufällig gleich bleiben.

  Empfehlung: Die Label-Konstanten auf die Resolver-Konstanten referenzieren (`public const string Produktivversionen = UpdatePfadKanalResolver.ProduktivversionenPfad;`) oder `UpdatePfadOption.Alle` für `Label` dieselbe Konstante wie für `Pfad` verwenden, sodass nur noch eine Quelle existiert.

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

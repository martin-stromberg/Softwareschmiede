# Tests

## Testklassen

### `KiAusfuehrungsServiceTests_WorkingDirectory`

Datei: `src/Softwareschmiede.Tests/Application/Services/KiAusfuehrungsServiceTests_WorkingDirectory.cs`

Testet die Arbeitsverzeichnisauflösung in `KiAusfuehrungsService.StartCliAsync` und verwandte `WorkingDirectoryResolver`-Funktionalität.

| Test | Beschreibung |
|------|-------------|
| `ResolveEffectiveWorkingDirectory_ShouldCombinePaths` | Prüft, dass Repository-Root und relativer Arbeitsverzeichnis-Pfad korrekt kombiniert werden. |
| `ResolveEffectiveWorkingDirectory_ShouldRejectPathTraversal` | Prüft Path-Traversal-Schutz: Pfade wie `../../../etc` werden korrekt abgelehnt. |
| `ResolveEffectiveWorkingDirectory_ShouldAcceptDotAsRoot` | Prüft, dass `.` (aktuelles Verzeichnis) als Repository-Root interpretiert wird. |
| `ResolveEffectiveWorkingDirectory_ShouldRejectSiblingDirectoryWithSharedPrefix` | Prüft, dass Sibling-Verzeichnisse mit gemeinsamem Präfix korrekt abgelehnt werden (z.B. Pfad `task-1` wird nicht als `task-12` interpretiert). |
| `ValidateWorkingDirectory_ShouldThrowWhenNotExists` | Prüft, dass `DirectoryNotFoundException` geworfen wird, wenn das aufgelöste Verzeichnis nicht existiert. |
| `ValidateWorkingDirectory_ShouldSucceedWhenExists` | Prüft, dass Validierung erfolgreich ist, wenn das Verzeichnis existiert. |
| `StartCliAsync_ShouldUseEffectiveWorkingDirectory` | **Kritisch:** Prüft, dass `KiAusfuehrungsService.StartCliAsync` das konfigurierte effektive Arbeitsverzeichnis (basierend auf `RepositoryStartKonfiguration.WorkingDirectoryRelativePath`) dem Plugin übergibt, nicht nur `LokalerKlonPfad`. |
| `StartCliAsync_ShouldUseRepoRootWhenConfigNull` | Prüft, dass `StartCliAsync` den Repository-Root nutzt, wenn keine `startConfig` (oder `startConfig.WorkingDirectoryRelativePath == null`) angegeben ist. |

---

### `ArbeitsverzeichnisOeffnenServiceTests`

Datei: `src/Softwareschmiede.Tests/Application/Services/ArbeitsverzeichnisOeffnenServiceTests.cs`

Testet `ArbeitsverzeichnisOeffnenService.Oeffne`.

| Test | Beschreibung |
|------|-------------|
| `Oeffne_StartetPlattformbefehlMitVerzeichnis` | Prüft, dass `explorer.exe` mit gequottem Verzeichnis aufgerufen wird (Windows-spezifisch). |
| `Oeffne_AufNichtWindows_WirftPlatformNotSupportedException` | Prüft Fehlerbehandlung auf Nicht-Windows-Systemen. |
| `Oeffne_MitLeeremVerzeichnis_WirftArgumentException` | Prüft Validierung: leere oder Whitespace-Verzeichnisse werfen `ArgumentException`. |
| `Oeffne_WennProzessStarterWirft_ReichtAusnahmeUnveraendertWeiter` | Prüft Exception-Propagation: wenn `IProzessStarter` wirft, wird die Exception weitergegeben. |

**Hinweis:** Diese Tests validieren NUR die Funktionalität von `ArbeitsverzeichnisOeffnenService.Oeffne` selbst (Prozessstart, Validierung). Sie prüfen NICHT die Arbeitsverzeichnisauflösung auf Basis von `RepositoryStartKonfiguration` — das ist Aufgabe des Callers (`TaskDetailViewModel`).

---

### `IdeOeffnenServiceTests`

Datei: `src/Softwareschmiede.Tests/Application/Services/IdeOeffnenServiceTests.cs`

Testet `IdeOeffnenService.FindeSolutions`, `OeffneSolution` und `OeffneVisualStudioCode`.

| Test | Beschreibung |
|------|-------------|
| `FindeSolutions_LiefertAlleSlnAlphabetischSortiert` | Prüft, dass alle `*.sln` und `*.slnx` Dateien gefunden und alphabetisch sortiert zurückgegeben werden. |
| `FindeSolutions_OhneSln_LiefertLeereListe` | Prüft leere Liste wenn keine Solutions vorhanden sind. |
| `FindeSolutions_NichtExistierendesVerzeichnis_LiefertLeereListe` | Prüft graceful handling wenn das Verzeichnis nicht existiert (gibt leere Liste zurück, wirft nicht). |
| `FindeSolutions_LeererPfad_LiefertLeereListe` | Prüft Handling bei `null` oder leerem Pfad-Parameter. |
| `OeffneSolution_StartetShellExecuteFuerSln` | Prüft, dass Solution-Datei via Shell-Execute mit dem Standardhandler geöffnet wird. |
| `OeffneSolution_MitLeeremPfad_WirftArgumentException` | Prüft Validierung: leerer Pfad wirft `ArgumentException`. |
| `OeffneSolution_WennProzessStarterWirft_ReichtAusnahmeUnveraendertWeiter` | Prüft Exception-Propagation. |
| `OeffneVisualStudioCode_StartetAufgeloestenBefehl` | Prüft, dass der von `IVisualStudioCodeLocator` aufgelöste VSCode-Befehl mit dem Arbeitsverzeichnis als Argument gestartet wird. |
| `OeffneVisualStudioCode_MitFehlendemArbeitsverzeichnis_Wirft` | Prüft, dass `DirectoryNotFoundException` geworfen wird bei fehlendem Verzeichnis. |
| `OeffneVisualStudioCode_WennVsCodeNichtVerfuegbar_Wirft` | Prüft, dass `InvalidOperationException` geworfen wird wenn VSCode nicht verfügbar ist. |

**Hinweis:** Analog zu `ArbeitsverzeichnisOeffnenServiceTests` — diese Tests validieren die Service-Logik selbst, nicht die Arbeitsverzeichnisauflösung.

---

## Hilfsmethoden

### `WorkingDirectoryResolverTestHelper` (falls vorhanden)

*Keine dedizierte Hilfsklasse dokumentiert; `WorkingDirectoryResolver` ist statisch und wird direkt aufgerufen.*

### `IdeOeffnenServiceTestHelper` (falls vorhanden)

*Keine dedizierte Hilfsklasse dokumentiert; Mocks werden direkt in den Tests erstellt.*

---

## Fehlende Tests (für Anforderung relevant)

Basierend auf der requirement.md gibt es folgende Testlücken:

1. **E2E-Tests für Ribbon-Aktionen:** Die Anforderung erwähnt, dass E2E-Tests für die Ribbon-Aktionen (Öffnen von Arbeitsverzeichnis / VSCode mit konfiguriertem Arbeitsverzeichnis) zu erweitern/schreiben sind:
   - `E2E_WorkingDirectory.AufgabeStarten_MitKonfiguriertemArbeitsverzeichnis_CliStartetErfolgreich_E2E` — existiert bereits (CLI mit konfiguriertem Arbeitsverzeichnis)
   - **FEHLT:** E2E-Tests, die verifizieren, dass die Ribbon-Aktionen (`OeffneArbeitsverzeichnis`, `OeffneIdeAsync`/`OeffneVisualStudioCodeFallback`) das konfigurierte Arbeitsverzeichnis tatsächlich nutzen

2. **Unit-Tests für TaskDetailViewModel-Auflösung:** Es gibt keine Unit-Tests, die prüfen, dass `TaskDetailViewModel.OeffneArbeitsverzeichnis()` und `TaskDetailViewModel.OeffneVisualStudioCodeFallback()` `WorkingDirectoryResolver` nutzen und das aufgelöste Verzeichnis an die Services übergeben.

3. **Async-Handling:** Die requirement.md stellt Frage 4: `OeffneArbeitsverzeichnis()` ist synchron, aber `WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync()` ist async. Es gibt keine Tests, die diese Inkonsistenz adressieren.

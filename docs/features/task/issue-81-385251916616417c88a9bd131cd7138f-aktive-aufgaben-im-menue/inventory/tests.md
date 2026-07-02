# Bestandsaufnahme: Tests

## Testklassen

### Unit Tests

#### `AufgabeServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/AufgabeServiceTests.cs`

| Testmethode | Beschreibung |
|-------------|-------------|
| `CreateAsync_ShouldCreateAufgabeWithStatusOffen_WhenCalledWithValidData` | Testet Erstellen einer Aufgabe mit Status `Neu` |
| `CreateFromIssueAsync_ShouldCreateAufgabeWithIssueReferenz_WhenCalledWithValidIssue` | Testet Erstellen einer Aufgabe aus einem Issue mit IssueReferenz |
| `GetByProjektAsync_ShouldReturnAufgabenForProjekt_WhenAufgabenExist` | Testet Abrufen aller Aufgaben für ein Projekt |
| `StartenAsync_ShouldSetStatusGestartetAndBranchName_WhenAufgabeExists` | Testet Status-Änderung auf `Gestartet` und Setzen von Branch + Klonpfad |
| `GetLatestDiffResultIdForFileAsync_ShouldReturnNewestMatchingDiff_WhenPathUsesDifferentSeparators` | Testet dateispezifische Diff-Suche mit unterschiedlichen Pfadnotationen |
| `GetLatestDiffResultIdForFileAsync_ShouldReturnNull_WhenNoDiffForFileExists` | Testet dass null zurückkommt, wenn keine Diff vorhanden |
| `StatusSetzenAsync_ShouldSetStatusGestartet_WhenAufgabeExists` | Testet Status-Setzen ohne Transitions-Validierung |

**Setup:**
- Verwendet `TestDbContextFactory.Create()` für In-Memory-Datenbank
- Mock für `ILogger<AufgabeService>`
- Seed-Daten: Ein Test-Projekt mit bekannter ID

**Hinweise:**
- Tests für die fehlende Methode `GetAktiveAufgabenAsync()` sind nicht vorhanden
- Tests für Heartbeat-Logik (`UpdateHeartbeatAsync`, `GetHeartbeatAgeMinutesAsync`) sind nicht abgebildet

### Integration Tests

#### `AufgabeServiceTests` (Integration)
Datei: `src/Softwareschmiede.IntegrationTests/Services/AufgabeServiceTests.cs`

Zusätzliche Integration-Tests für den AufgabeService (Datenbank-Persistierung).

### UI Tests / ViewModel Tests

#### `ProjectDetailViewModelTests`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/ProjectDetailViewModelTests.cs`

Tests für ProjectDetailViewModel (enthält Aufgabenlisten-Handling).

**Hinweise:**
- Keine spezialisierten Tests für `MainWindowViewModel` oder `DashboardViewModel` vorhanden
- Keine Tests für Navigation zu Aufgabendetails
- Tests für die neuen Properties (`AktiveAufgaben`, `IsDashboardVisible`) fehlen

## Hilfsmethoden

### Testdatenfactory

#### `TestDbContextFactory`
Datei: `src/Softwareschmiede.Tests/Helpers/TestDbContextFactory.cs`

- `Create()` : `SoftwareschmiededDbContext` - Erstellt eine In-Memory-SQLite-Datenbank für Tests

### Domain Factories (falls vorhanden)

Suche nach anderen Builder/Factory-Klassen für Testdaten im `Helpers`-Verzeichnis:
```
src/Softwareschmiede.Tests/Helpers/
```

**Bestehende Hilfsmethoden in `AufgabeServiceTests`:**
- `CreateDiffResult()` - Erstellt Test-DiffResult-Instanzen

---

## Empfehlungen für neue Tests

Für die Anforderung "Aktive Aufgaben im Menü" sollten folgende Tests erstellt werden:

### Service-Level
1. Test für `GetAktiveAufgabenAsync()` (neue Methode)
   - Mit verschiedenen Status (Gestartet, Wartend, Neu, Beendet, Archiviert)
   - Mit Sortierung (LastHeartbeatUtc, ErstellungsDatum)
   - Mit optionalem Limit

### ViewModel-Level
1. Test für `MainWindowViewModel.AktiveAufgabenAktualisierenAsync()`
2. Test für `MainWindowViewModel.IsDashboardVisible` (computed Property)
3. Test für `MainWindowViewModel.NavigateZuAufgabeCommand` mit Guid-Parameter
4. Test für `DashboardViewModel.AktiveAufgaben` ObservableCollection-Binding

### UI/XAML-Level
1. Converter-Test für `KiAusfuehrungsStatusConverter`
2. Integration-Test für Seitenleisten-Menü mit aktiven Aufgaben
3. Test für Sichtbarkeit der "Aktive Aufgaben"-Sektion (abhängig von `IsDashboardVisible`)

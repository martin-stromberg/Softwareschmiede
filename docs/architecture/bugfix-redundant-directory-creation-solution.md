# Architektur-Design: Bug Fix – Redundante Verzeichniserstellung

**Datum:** 2025-05-09  
**Status:** Entwurf  
**Änderungstyp:** Bugfix / Refactoring  

---

## 1. Ursache-Analyse

### Problem-Symptom
- Eingabe "Test" erstellt Verzeichnisse `T`, `Te`, `Tes`, `Test`
- Tritt während Inline-Validierung auf

### Root Cause
Die Methode `ValidatePathForSave()` in `ArbeitsverzeichnisSettingsService` vermischt zwei Verantwortlichkeiten:

```csharp
public static void ValidatePathForSave(string path)
{
    // Validierung (Zeile 73-97) ✓ Richtig
    // ...
    
    // SIDE EFFECT: Verzeichnis-Erstellung (Zeile 99-106) ✗ Falsch!
    Directory.CreateDirectory(fullPath);
}
```

**Das Problem:** Diese Methode wird aufgerufen aus:
1. `ArbeitsverzeichnisInputChanged()` – bei **jedem Tastendruck** für Inline-Validierung
2. `ArbeitsverzeichnisSpeichernAsync()` – nur beim bewussten Speichern
3. `SaveArbeitsverzeichnisAsync()` – interne Speicherlogik

**Resultat:** 4 aufeinanderfolgende Tastendrücke = 4 `Directory.CreateDirectory()`-Aufrufe

---

## 2. Lösungsansatz: Separation of Concerns

Teile die Funktionalität in zwei klare Methoden auf:

### Neue Methodenstruktur

```
ArbeitsverzeichnisSettingsService
│
├── ValidatePathForConfiguration(string path) [STATELESS]
│   ├── Null/Whitespace prüfen
│   ├── Path.IsPathRooted() prüfen
│   ├── Ungültige Zeichen prüfen
│   ├── Path.GetFullPath() Validierung
│   └── ❌ KEINE Directory-Erstellung
│
└── EnsureDirectoryExistsAsync(string path) [STATEFUL, nur im Persistence-Layer]
    ├── Nur von SaveArbeitsverzeichnisAsync() aufgerufen
    ├── Erstellt Verzeichnis wenn nötig
    └── Fehlerbehandlung für I/O-Fehler
```

---

## 3. Detailliertes Design

### 3.1 Neue Validierungsmethode

**Ort:** `ArbeitsverzeichnisSettingsService.cs`

```csharp
/// <summary>
/// Validiert einen Pfad für die Speicherung in den Einstellungen.
/// Diese Methode hat KEINE Nebeneffekte und erstellt keine Verzeichnisse.
/// </summary>
/// <param name="path">Der zu validierende Pfad</param>
/// <exception cref="ArgumentException">Bei ungültigem Pfad</exception>
public static void ValidatePathForConfiguration(string path)
{
    if (string.IsNullOrWhiteSpace(path))
    {
        return;
    }

    if (!Path.IsPathRooted(path))
    {
        throw new ArgumentException("Pfad muss absolut sein.");
    }

    var invalidChars = Path.GetInvalidPathChars();
    if (path.IndexOfAny(invalidChars) >= 0)
    {
        throw new ArgumentException("Pfad enthält ungültige Zeichen.");
    }

    string fullPath;
    try
    {
        fullPath = Path.GetFullPath(path);
    }
    catch (Exception ex)
    {
        throw new ArgumentException("Pfad ist ungültig.", ex);
    }

    // ❌ NICHT: Directory.CreateDirectory(fullPath);
}
```

### 3.2 Verzeichnis-Erstellung in Speichermethode

**Ort:** `ArbeitsverzeichnisSettingsService.cs` – Methode `SaveArbeitsverzeichnisAsync()`

**Bestehender Code (Zeile 37-67):**
```csharp
public async Task SaveArbeitsverzeichnisAsync(string? arbeitsverzeichnis, CancellationToken ct = default)
{
    var trimmed = arbeitsverzeichnis?.Trim();
    var normalized = string.IsNullOrWhiteSpace(trimmed)
        ? null
        : NormalizePath(trimmed);

    if (normalized is not null)
    {
        ValidatePathForSave(trimmed!);  // ← HIER wird aktuell das Verzeichnis erstellt
    }
    // ... weitere Logik
}
```

**Neue Implementierung:**
```csharp
public async Task SaveArbeitsverzeichnisAsync(string? arbeitsverzeichnis, CancellationToken ct = default)
{
    var trimmed = arbeitsverzeichnis?.Trim();
    var normalized = string.IsNullOrWhiteSpace(trimmed)
        ? null
        : NormalizePath(trimmed);

    if (normalized is not null)
    {
        // 1. Validierung (keine Nebeneffekte)
        ValidatePathForConfiguration(trimmed!);
        
        // 2. Verzeichnis erstellen (Nebeneffekt nur hier)
        try
        {
            Directory.CreateDirectory(normalized);
        }
        catch (Exception ex)
        {
            throw new ArgumentException("Verzeichnis kann nicht erstellt oder erreicht werden.", ex);
        }
    }

    // ... weitere Persistierungs-Logik ...
}
```

### 3.3 Updates in UI-Layer

**Ort:** `Components/Pages/Einstellungen.razor.cs`

**Zeile 221 (in `ArbeitsverzeichnisInputChanged`):**
```csharp
// ALT:
ArbeitsverzeichnisSettingsService.ValidatePathForSave(_arbeitsverzeichnisInput);

// NEU:
ArbeitsverzeichnisSettingsService.ValidatePathForConfiguration(_arbeitsverzeichnisInput);
```

**Zeile 174 (in `ArbeitsverzeichnisSpeichernAsync`):**
```csharp
// ALT:
ArbeitsverzeichnisSettingsService.ValidatePathForSave(_arbeitsverzeichnisInput);

// NEU:
// Hier ist Validierung nicht nötig, da SaveArbeitsverzeichnisAsync()
// bereits validiert und Verzeichnis erstellt
```

---

## 4. Änderungspaket (Change Package)

### Dateien zum Ändern
1. **`src/Softwareschmiede/Application/Services/ArbeitsverzeichnisSettingsService.cs`**
   - Methode `ValidatePathForSave()` → Umbenennen zu `ValidatePathForConfiguration()`
   - Zeile 99-106: `Directory.CreateDirectory()` entfernen
   - Methode `SaveArbeitsverzeichnisAsync()`: Directory-Erstellung hinzufügen

2. **`src/Softwareschmiede/Components/Pages/Einstellungen.razor.cs`**
   - Zeile 174 & 221: `ValidatePathForSave` → `ValidatePathForConfiguration`

### Tests zum Aktualisieren
1. **`src/Softwareschmiede.Tests/Application/Services/ArbeitsverzeichnisSettingsServiceTests.cs`**
   - Test für `ValidatePathForConfiguration()` (keine Directory-Erstellung)
   - Test für `SaveArbeitsverzeichnisAsync()` (mit Directory-Erstellung)

2. **`src/Softwareschmiede.Tests/Components/Pages/EinstellungenBaseArbeitsverzeichnisTests.cs`**
   - Sicherstellen, dass Inline-Validierung keine Verzeichnisse erstellt

---

## 5. Testabdeckung

### Neue Test-Szenarien

#### Test 1: ValidatePathForConfiguration darf KEINE Verzeichnisse erstellen
```csharp
[Fact]
public void ValidatePathForConfiguration_ShouldNotCreateDirectory_WhenValidating()
{
    // Arrange
    var testPath = Path.Combine(Path.GetTempPath(), $"no-create-{Guid.NewGuid():N}");
    
    // Act
    ArbeitsverzeichnisSettingsService.ValidatePathForConfiguration(testPath);
    
    // Assert
    Directory.Exists(testPath).Should().BeFalse("Verzeichnis darf nicht durch Validierung erstellt werden");
}
```

#### Test 2: SaveArbeitsverzeichnisAsync erstellt Verzeichnis
```csharp
[Fact]
public async Task SaveArbeitsverzeichnisAsync_ShouldCreateDirectory_WhenPathIsValid()
{
    // Arrange
    await using var db = CreateDb();
    var service = new ArbeitsverzeichnisSettingsService(db, NullLogger<ArbeitsverzeichnisSettingsService>.Instance);
    var testPath = Path.Combine(Path.GetTempPath(), $"should-create-{Guid.NewGuid():N}");
    
    // Act
    await service.SaveArbeitsverzeichnisAsync(testPath);
    
    // Assert
    Directory.Exists(testPath).Should().BeTrue("Verzeichnis muss beim Speichern erstellt werden");
}
```

#### Test 3: Inline-Validierung erstellt keine Verzeichnisse
```csharp
[Fact]
public void ArbeitsverzeichnisInputChanged_ShouldNotCreateDirectory_WhenValidatingInput()
{
    // Arrange
    var component = CreateSut();
    var testPath = Path.Combine(Path.GetTempPath(), $"inline-{Guid.NewGuid():N}");
    
    // Act
    component.InvokeArbeitsverzeichnisInputChanged(testPath);
    
    // Assert
    Directory.Exists(testPath).Should().BeFalse("Inline-Validierung darf keine Verzeichnisse erstellen");
}
```

---

## 6. Rollout-Plan

### Phase 1: Code-Änderung
- Refaktorierung der beiden Service-Methoden
- Update der UI-Layer-Aufrufe

### Phase 2: Test-Validierung
- Alle Unit Tests grün
- Integrationstests für SaveArbeitsverzeichnisAsync
- Manuelle UI-Tests (Eingabe von "Test" sollte nur 1 Ordner erstellen)

### Phase 3: Verifikation
- Cleanup: Keine Tests schlagen fehl
- Performance: Keine Regression
- Dokumentation aktualisiert

---

## 7. Rückfallplan

Falls kritische Regression auftritt:
1. Revert zu Alt-Implementierung
2. Analyse und Root Cause Review
3. Neue Iteration mit angepasstem Design

---

## 8. Nicht-funktionale Anforderungen

| Kriterium | Anforderung | Notizen |
|-----------|------------|---------|
| Sicherheit | Pfadvalidierung bleibt streng | Keine Lockerung der Validierungsregeln |
| Performance | Keine Regression | Directory-Erstellung bleibt O(1) |
| Wartbarkeit | Klare Separation of Concerns | Validierung vs. Persistierung getrennt |
| Rückwärtskompatibilität | API bleibt gleich | `ValidatePathForConfiguration` ist nur privat |

---

## 9. Schnittstellen-Übersicht

```
Public API bleibt identisch:
├── SaveArbeitsverzeichnisAsync(string?, CancellationToken)
├── GetArbeitsverzeichnisAsync(CancellationToken)
└── NormalizePath(string) → string

Private/Internal Änderungen:
├── ValidatePathForSave() → ValidatePathForConfiguration()
│   └── Entfernt: Directory.CreateDirectory()
```

---

## Gültigkeitsprüfung

✓ Löst das Kernproblem (4 Verzeichnisse → 1 Verzeichnis)  
✓ Beachtet Single Responsibility Principle  
✓ Keine API-Breaking Changes  
✓ Testbar  
✓ Wartbar  

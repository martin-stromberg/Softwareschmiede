# Anforderungs-Analyse: Bug Fix – Redundante Verzeichniserstellung

**Datum:** 2025-05-09  
**Status:** In Analyse  
**Priorität:** Mittel  

---

## 1. Problembeschreibung

Bei der Eingabe eines Arbeitsverzeichnisses (z. B. "Test") werden unnötige Zwischenverzeichnisse erstellt:
- Eingabe: `Test`
- Tatsächliche Ausgabe: Es werden 4 Ordner erstellt: `T`, `Te`, `Tes`, `Test`
- Erwartete Ausgabe: Nur ein Ordner `Test` sollte erstellt werden

Dieses Verhalten tritt bei der **Inline-Validierung** während der Benutzereingabe auf, nicht nur beim finalen Speichern.

---

## 2. Fehlerlokalisation

### Datei
- **`src/Softwareschmiede/Application/Services/ArbeitsverzeichnisSettingsService.cs`**

### Betroffene Methoden
1. **`ValidatePathForSave(string path)`** (Zeile 71–107)
   - **Codezeile mit Problem:** 101 – `Directory.CreateDirectory(fullPath);`
   - Diese Zeile erstellt das Verzeichnis als **Nebeneffekt** der Validierung.

2. **Aufgerufen von:** `ArbeitsverzeichnisInputChanged(string value)` (Zeile 209–227 in `Components/Pages/Einstellungen.razor.cs`)
   - Diese Methode wird bei **jedem Keystroke** aufgerufen
   - Sie ruft `ValidatePathForSave()` auf Zeile 221 auf

### Aufrufkette (Call Stack)
```
User tippt "Test" mit 4 Schritten:
  [T] → ArbeitsverzeichnisInputChanged("T")    → ValidatePathForSave("T")    → Directory.CreateDirectory("T")
  [Te] → ArbeitsverzeichnisInputChanged("Te")  → ValidatePathForSave("Te")   → Directory.CreateDirectory("Te")
  [Tes] → ArbeitsverzeichnisInputChanged("Tes") → ValidatePathForSave("Tes")  → Directory.CreateDirectory("Tes")
  [Test] → ArbeitsverzeichnisInputChanged("Test") → ValidatePathForSave("Test") → Directory.CreateDirectory("Test")
```

---

## 3. Root Cause

**Architekturales Designproblem:** Die Methode `ValidatePathForSave()` hat einen **doppelten Zweck**:
1. **Validation:** Prüfung des Pfads auf Gültigkeit
2. **Side Effect:** Tatsächliche Erstellung des Verzeichnisses

### Warum ist das falsch?
- **Validierung sollte keine Nebeneffekte haben** (Single Responsibility Principle verletzt)
- **Inline-Validierung während der Eingabe** darf nicht dauerhaft Verzeichnisse erstellen
- **Benutzererwartung:** Der Benutzer erwartet, dass beim Tippen nur validiert wird, nicht modifiziert wird
- **Idempotenz-Problem:** Bei jeder Validierung wird ein Verzeichnis erstellt

### Gegenwärtige Nutzung

```csharp
// Zeile 221 in Einstellungen.razor.cs: Validation während der Eingabe
ArbeitsverzeichnisSettingsService.ValidatePathForSave(_arbeitsverzeichnisInput);

// Zeile 174 in Einstellungen.razor.cs: Validation vor dem Speichern (absichtlich)
ArbeitsverzeichnisSettingsService.ValidatePathForSave(_arbeitsverzeichnisInput);

// Zeile 46 in ArbeitsverzeichnisSettingsService.cs: Validation während des Speicherns
ValidatePathForSave(trimmed!);
```

---

## 4. Akzeptanzkriterien

### AC1: Keine Verzeichniserstellung bei Inline-Validierung
- Wenn der Benutzer text eingibt, dürfen **keine Verzeichnisse erstellt werden**
- Nur Syntaxvalidierung und Pfadprüfung sollten stattfinden

### AC2: Verzeichnis wird nur beim Speichern erstellt
- Das Verzeichnis wird erst bei `SaveArbeitsverzeichnisAsync()` oder bei explizitem Speichern erstellt
- Maximal einmalig pro Speicheroperation

### AC3: Validierung wird nicht unterbrochen
- Die Validierungsfunktion muss weiterhin Fehler zurückweisen
- Pfad-Syntax muss korrekt sein (absolut, keine ungültigen Zeichen)

### AC4: Keine Duplikate von Validierungslogik
- Validierungslogik sollte nicht dupliziert werden
- Separation of Concerns: Validierung vs. Erstellung

---

## 5. Geschäftliche Auswirkungen

**Problem-Schweregrad:** Mittel
- Benutzer können Systemdateien versehentlich mit Fragmenten füllen (z. B. `C:\T`, `C:\Te`, `C:\Tes`)
- Kann zu verwirrender Verzeichnisstruktur führen
- Sicherheits-/Hygiene-Problem: Unerwünschte Verzeichniserstellung ohne explizite Bestätigung

---

## 6. Abhängigkeiten & Constraints

- Test-Abdeckung muss vorhanden sein
- Änderungen dürfen nicht bestehende Tests brechen
- UI-Verhalten (Error-Hinweise während Eingabe) sollte beibehalten werden
- Datenbank-Persistierung bleibt unverändert

---

## 7. Nächste Schritte

1. **Architektur-Design** (planning-architecture-blueprint): Design neue Lösung
2. **Code-Änderung**: Implementiere Separation of Concerns
3. **Test-Update**: Ergänze/korrigiere Teststten
4. **Integration**: Merge und Verification

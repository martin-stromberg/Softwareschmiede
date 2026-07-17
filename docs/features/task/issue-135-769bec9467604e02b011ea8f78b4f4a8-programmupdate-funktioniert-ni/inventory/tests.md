# Tests

## Testklassen

### `CliUpdateSafetyServiceTests`

**Datei:** `src/Softwareschmiede.Tests/Application/Services/Updates/CliUpdateSafetyServiceTests.cs`

Testklasse für `CliUpdateSafetyService`.

#### Testmethoden

##### `CheckAsync_ShouldTreatRunningAndNullStatusAsRisky()` (Zeile 35–49)

**Was wird getestet?**
- Dass sowohl `LaufStatus.Laeuft` als auch `LaufStatus == null` als riskant klassifiziert werden
- Dass `LaufStatus.WartetAufEingabe` **nicht** als riskant klassifiziert wird
- Dass die Anzahl der riskanten Aufgaben korrekt gezählt wird (erwartet: 2)

**Arragement (Zeile 38–40):**
- Erstellt 3 aktive Aufgaben mit verschiedenen `LaufStatus`-Werten:
  - `"Laeuft"` mit `LaufStatus.Laeuft` (blockierend nach aktuellem, fehlerhafte Logik)
  - `"Null"` mit `LaufStatus == null` (blockierend nach aktuellem, fehlerhaftem Filter)
  - `"Wartet"` mit `LaufStatus.WartetAufEingabe` (nicht blockierend)

**Act (Zeile 43):**
- Ruft `sut.CheckAsync()` auf

**Assert (Zeile 45–48):**
- `result.RequiresConfirmation.Should().BeTrue()` — Bestätigung erforderlich
- `result.RiskyTaskCount.Should().Be(2)` — **FEHLER: Nach der Anforderung sollte nur 1 sein** (nur `Laeuft`)
- Überprüft, dass die riskanten Task-Namen in der Liste enthalten sind

#### Hilfsmethoden

##### `CreateActiveTaskAsync(string title, AufgabeLaufStatus? laufStatus)` (Zeile 51–60)

**Zweck:** Hilfsmethode zum Erstellen von Test-Aufgaben mit definiertem `LaufStatus`.

**Ablauf:**
1. Erstellt eine neue Aufgabe via `_aufgabeService.CreateAsync()`
2. Startet sie via `StartenAsync()` (setzt Status auf `Gestartet`, Branch und Klonpfad)
3. Setzt einen aktiven Lauf via `AktivenLaufSetzenAsync()` (setzt `AktiveRunId`)
4. Laden die Aufgabe neu aus der DB mit Tracking
5. Setzt den `LaufStatus` manuell auf den gewünschten Wert
6. Speichert alle Änderungen
7. Gibt die Aufgabe zurück

Diese Methode umgeht die normale Service-Logik, um alle 3 Zustände (`Laeuft`, `WartetAufEingabe`, `null`) zu testen.

---

## Kontext: Datenbankinitialisierung

**Konstruktor `CliUpdateSafetyServiceTests()` (Zeile 18–29):**
- Erstellt eine Test-Datenbankinstanz via `TestDbContextFactory.Create()`
- Initialisiert `AufgabeService` mit Test-DB und `NullLogger`
- Erstellt ein Test-Projekt mit fester ID (`33333333-3333-3333-3333-333333333333`)
- Speichert das Projekt
- Diese Basis wird für alle Tests wiederverwendet

**Dispose (Zeile 31–32):**
- Gibt die Test-DB-Ressourcen frei

---

## Auswirkungen der Anforderungskorrektur

Nach Korrektur der Filterlogik in `CliUpdateSafetyService` von:
```csharp
.Where(a => a.AktiveRunId is not null && a.LaufStatus != AufgabeLaufStatus.WartetAufEingabe)
```

zu:
```csharp
.Where(a => a.AktiveRunId is not null && a.LaufStatus == AufgabeLaufStatus.Laeuft)
```

**wird die Test-Assertion fehlschlagen:**
- Erwartet: `result.RiskyTaskCount.Should().Be(2)`
- Tatsächlich: `result.RiskyTaskCount == 1` (nur die Aufgabe mit `Laeuft`)

Die Test-Methode muss dann angepasst werden, um die korrigierte Logik abzubilden. Möglicherweise sollte sie auch in `CheckAsync_ShouldTreatOnlyRunningStatusAsRisky()` oder ähnlich umbenannt werden, um die neue Semantik zu reflektieren.

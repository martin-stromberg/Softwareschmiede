# Bestandsaufnahme: CLI-Sicherheitsprüfung bei Programmupdate (Issue #135)

Diese Bestandsaufnahme analysiert die Komponenten der Sicherheitsprüfung beim Programmupdate, die derzeit Aufgaben mit `LaufStatus == null` (noch nicht initialisiert) fälschlicherweise als blockierend bewertet.

## Zusammenfassung

| Aspekt | Status | Anmerkung |
|--------|--------|-----------|
| **Logik-Service** | ✓ Vorhanden | `CliUpdateSafetyService` mit `CheckAsync()`-Methode |
| **Datenmodell** | ✓ Vorhanden | `Aufgabe`-Entity mit `LaufStatus?`-Property |
| **Enum-Definitionen** | ✓ Vorhanden | `AufgabeLaufStatus` mit Werten `Laeuft`, `WartetAufEingabe` |
| **Interface** | ✓ Vorhanden | `ICliUpdateSafetyService` mit `CheckAsync()`-Verzeichnung |
| **Modelle/Records** | ✓ Vorhanden | `CliUpdateSafetyResult` für Rückgabewerte |
| **Tests** | ✓ Vorhanden | `CliUpdateSafetyServiceTests`, aber Test-Assertion ist fehlerhaft |
| **Abhängigkeiten** | ✓ Vorhanden | `AufgabeService` für Datenbeschaffung |

### Kritischer Fehler

**Datei:** `src/Softwareschmiede/Application/Services/Updates/CliUpdateSafetyService.cs` (Zeile 23–26)

Der Filter in `CheckAsync()` nutzt `!=`-Vergleich statt Gleichheit:
```csharp
// FALSCH:
.Where(a => a.AktiveRunId is not null && a.LaufStatus != AufgabeLaufStatus.WartetAufEingabe)
```

Dies wertet `null != WartetAufEingabe` als `true`, weshalb Aufgaben mit `LaufStatus == null` fälschlicherweise als blockierend eingestuft werden.

**Fehlerhafte Test-Assertion:** Die Test-Methode `CheckAsync_ShouldTreatRunningAndNullStatusAsRisky()` in `CliUpdateSafetyServiceTests.cs` (Zeile 36–49) erwartet, dass `null`-Status als riskant gezählt werden (Zeile 46: `.RiskyTaskCount.Should().Be(2)`). Dies wird nach der Korrektur fehlschlagen, da nur `Laeuft` riskant sein sollte.

## Details

- [Logik-Services](inventory/logic.md)
- [Datenmodell](inventory/models.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)

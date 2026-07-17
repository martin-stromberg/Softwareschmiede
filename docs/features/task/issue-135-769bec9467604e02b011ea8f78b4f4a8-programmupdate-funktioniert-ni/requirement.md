# Anforderung

## Fachliche Zusammenfassung

Die Sicherheitsprüfung beim Programmupdate (`CliUpdateSafetyService.CheckAsync()`) filtert Aufgaben mit dem Kriterium `LaufStatus != WartetAufEingabe` heraus, um zu bestimmen, welche Aufgaben als blockierend gelten. Da `LaufStatus` nullable ist, werden Aufgaben mit `LaufStatus == null` (noch nicht initialisiert / "Bereit") fälschlicherweise als blockierend bewertet, obwohl kein CLI-Prozess tatsächlich läuft. Erwartetes Verhalten: Nur Aufgaben mit `LaufStatus == AufgabeLaufStatus.Laeuft` sollen das Update blockieren. Aufgaben mit `LaufStatus == null` oder `LaufStatus == AufgabeLaufStatus.WartetAufEingabe` sind nicht blockierend.

## Betroffene Klassen und Komponenten

### Logik-Services
- `Softwareschmiede.Application.Services.Updates.CliUpdateSafetyService` — Sicherheitsprüfung vor Programmupdate
  - Methode: `CheckAsync(CancellationToken ct = default)` (Zeile 20–34)
  - Fehlerhafter Filter in Zeile 23–26

### Datenmodell-Enums
- `Softwareschmiede.Domain.Enums.AufgabeLaufStatus` — Laufzeit-Substatus einer aktiven CLI-Ausführung
  - `Laeuft` — CLI läuft und verarbeitet aktiv
  - `WartetAufEingabe` — CLI läuft, wartet aber auf Benutzereingabe
  - (implizit: `null` — kein aktiver Lauf oder noch nicht initialisiert, entspricht "Bereit")

- `Softwareschmiede.Domain.Entities.Aufgabe.LaufStatus` (Zeile 63) — nullable `AufgabeLaufStatus?`

### Tests (zu überprüfen/zu erweitern)
- `Softwareschmiede.Tests.Application.Services.Updates.CliUpdateSafetyServiceTests` (falls vorhanden)
  - Test-Szenarios für `null`, `Laeuft`, `WartetAufEingabe`

## Implementierungsansatz

**Fehler im Filter:**
Die aktuelle Filterlogik in `CliUpdateSafetyService.CheckAsync()` (Zeile 23–26):
```csharp
var riskyTasks = activeTasks
    .Where(a => a.AktiveRunId is not null && a.LaufStatus != AufgabeLaufStatus.WartetAufEingabe)
    .Select(a => $"{a.Titel} ({a.Id})")
    .ToList();
```

Problematisch: Der Vergleich `a.LaufStatus != AufgabeLaufStatus.WartetAufEingabe` wertet `null != WartetAufEingabe` als `true`, weshalb Aufgaben mit `LaufStatus == null` (noch nicht initialisiert) einbezogen werden.

**Korrekte Filterlogik:**
Nur Aufgaben mit tatsächlich laufendem Prozess (`LaufStatus == AufgabeLaufStatus.Laeuft`) als blockierend einstufen:
```csharp
var riskyTasks = activeTasks
    .Where(a => a.AktiveRunId is not null && a.LaufStatus == AufgabeLaufStatus.Laeuft)
    .Select(a => $"{a.Titel} ({a.Id})")
    .ToList();
```

**Begründung:** Der `LaufStatus` hat drei semantische Zustände:
- `null`: Kein aktiver Lauf oder noch nicht initialisiert ("Bereit") → nicht blockierend
- `AufgabeLaufStatus.Laeuft`: CLI läuft aktiv → blockierend
- `AufgabeLaufStatus.WartetAufEingabe`: CLI läuft, wartet auf Eingabe → nicht blockierend (da bereits geplant)

## Konfiguration

Keine Konfiguration erforderlich. Die Änderung ist eine reine Logik-Korrektur.

## Offene Fragen

- Ist `LaufStatus == null` mit `AktiveRunId != null` ein erwarteter Zustand, oder sollte `LaufStatus` auch auf einen Standardwert initialisiert werden?
- Gibt es andere Stellen im Code, die das `LaufStatus`-Kriterium ähnlich falsch verwenden?
- Sollten bestehende Tests für `CliUpdateSafetyService` angepasst oder erweitert werden, um diese Szenarien abzudecken?

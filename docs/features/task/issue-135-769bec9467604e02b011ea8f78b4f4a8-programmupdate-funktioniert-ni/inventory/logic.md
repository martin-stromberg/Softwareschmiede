# Logik-Services

## `CliUpdateSafetyService`

**Datei:** `src/Softwareschmiede/Application/Services/Updates/CliUpdateSafetyService.cs`

Implementiert `ICliUpdateSafetyService`. Prüft aktive CLI-Aufgaben vor dem Start eines Programmupdates.

| Methode | Sichtbarkeit | Kurzbeschreibung | Zeile |
|---------|-------------|------------------|-------|
| `CliUpdateSafetyService(AufgabeService aufgabeService, ILogger<CliUpdateSafetyService> logger)` | public | Konstruktor mit Dependency Injection | 13 |
| `CheckAsync(CancellationToken ct = default)` | public async | Filtert aktive Aufgaben und bestimmt riskante Laufstatus | 20–34 |

### Abhängigkeiten

- **`AufgabeService`** — Beschaff aktive Aufgaben via `GetAktiveAufgabenAsync()`
- **`ILogger<CliUpdateSafetyService>`** — Protokolliert gefundene riskante Aufgaben

### Ablauf `CheckAsync()`

1. Ruft `_aufgabeService.GetAktiveAufgabenAsync(ct)` auf (alle aktiven Aufgaben mit Status `Gestartet` oder `Wartend`, max. 20)
2. Filtert nach `AktiveRunId is not null` (nur Aufgaben mit laufender Ausführung)
3. **FEHLERHAFTE FILTERLOGIK (Zeile 24):** Zusätzliches Kriterium `a.LaufStatus != AufgabeLaufStatus.WartetAufEingabe`
   - Wertet `null != WartetAufEingabe` als `true`
   - Führt dazu, dass Aufgaben mit `LaufStatus == null` als riskant eingestuft werden
4. Wählt Titel + ID der riskanten Aufgaben
5. Protokolliert Anzahl der riskanten Aufgaben
6. Gibt `CliUpdateSafetyResult` zurück

### Publizierte Events

Keine (ist ein Service ohne Event-basierte Kommunikation).

### Abonnierte Events

Keine direkten Abonnements (nutzt nur `AufgabeService` für Lesezugriff).

---

## `AufgabeService` (Verwandte Methoden)

**Datei:** `src/Softwareschmiede/Application/Services/AufgabeService.cs`

Methoden, die für die Bestandsaufnahme relevant sind:

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetAktiveAufgabenAsync(CancellationToken ct)` | public async | Gibt alle aktiven Aufgaben (Status `Gestartet` oder `Wartend`) zurück, sortiert nach letztem CLI-Start (max. 20) |
| `StartenAsync(Guid id, string branchName, string lokalerKlonPfad, CancellationToken ct)` | public async | Startet eine Aufgabe: Status → `Gestartet`, Branch und Arbeitsverzeichnis setzen |
| `AktivenLaufSetzenAsync(Guid id, string laufId, CancellationToken ct)` | public async | Setzt `AktiveRunId` (Zeile 459+) — damit wird eine Aufgabe als "aktiv laufend" gekennzeichnet |
| `LaufBeendenAsync(Guid id, CancellationToken ct)` | public async | Setzt `AktiveRunId = null` und `LaufStatus = null` (Zeile 483–489) — bereinigt nach Laufende |

**Hinweis zu `LaufStatus`:** Wird nur beim aktiven Lauf gesetzt/bereinigt (siehe Zeile 486–488: "LaufStatus gehört nur zu einem aktiven Lauf"). Beim Beenden wird es explizit auf `null` gesetzt, um veraltete Substatuszustände zu vermeiden.

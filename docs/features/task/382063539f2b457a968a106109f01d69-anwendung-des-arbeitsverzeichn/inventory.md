# Bestandsaufnahme: Zuverlässige Anwendung des Arbeitsverzeichnisses

Diese Bestandsaufnahme dokumentiert den bestehenden Code bezüglich der zuverlässigen Anwendung des konfigurierten Arbeitsverzeichnisses (`RepositoryStartKonfiguration.WorkingDirectoryRelativePath`) in drei Abläufen: (1) CLI-Prozessstart für KI-Ausführungen, (2) Öffnen des Arbeitsverzeichnisses über Ribbon-Aktion, (3) Starten von Visual Studio Code über Ribbon-Aktion.

---

## Zusammenfassung

### ✓ Vorhanden und funktional
- **`WorkingDirectoryResolver`** — Statischer Service zur Auflösung des effektiven Arbeitsverzeichnisses aus Repository-Root + relativer Pfad; implementiert Path-Traversal-Schutz und Validierung
- **`KiAusfuehrungsService.StartCliAsync`** — Nutzt `WorkingDirectoryResolver` bereits zur Bestimmung des Arbeitsverzeichnisses und übergibt es korrekt dem CLI-Plugin
- **CLI-Tests** — Unit-Tests für `WorkingDirectoryResolver` und CLI-Start sind vorhanden und abdecken relevante Szenarien (Pfadkombination, Path-Traversal-Schutz, Validierung)
- **Services für Öffnen** — `ArbeitsverzeichnisOeffnenService` und `IdeOeffnenService` implementieren die erforderliche Funktionalität (explorer.exe starten, Solutions finden, VSCode öffnen)

### ⚠️ Teilweise vorhanden, aber mit Lücken
- **`TaskDetailViewModel.OeffneArbeitsverzeichnis()`** — Existiert, aber ruft `ArbeitsverzeichnisOeffnenService.Oeffne()` nur mit `Aufgabe.LokalerKlonPfad` auf, ohne `WorkingDirectoryResolver` zu nutzen → **konfiguriertes Arbeitsverzeichnis wird ignoriert**
- **`TaskDetailViewModel.OeffneIdeAsync()` / `OeffneVisualStudioCodeFallback()`** — Existieren, aber nutzen `WorkingDirectoryResolver` nicht → **konfiguriertes Arbeitsverzeichnis wird ignoriert, Solutions werden im Repository-Root statt im konfigurierten Arbeitsverzeichnis gesucht**
- **Ribbon-Action-Tests** — E2E-Tests für die Öffnen-Aktionen fehlen; nur CLI-Test existiert

### ❌ Nicht vorhanden
- Unit-Tests für `TaskDetailViewModel`-Methoden, die prüfen, dass `WorkingDirectoryResolver` genutzt wird
- E2E-Tests, die verifizieren, dass Ribbon-Aktionen das konfigurierte Arbeitsverzeichnis verwenden
- Implementierte Fehlerbehandlung für fehlende/ungültige Arbeitsverzeichniskonfigurationen in den Ribbon-Aktionen

---

## Details

### [Datenmodelle](inventory/models.md)

- **`RepositoryStartKonfiguration`** — Zentrale Eigenschaft `WorkingDirectoryRelativePath` ist bereits vorhanden
- **`Aufgabe`** — Enthält `LokalerKlonPfad` (Repository-Root) und Navigation zu `GitRepository` mit `RepositoryStartKonfiguration`

### [Logik-Services](inventory/logic.md)

- **`WorkingDirectoryResolver`** — Statischer Service zur Auflösung des effektiven Arbeitsverzeichnisses
  - `DetermineEffectiveWorkingDirectoryAsync()` — Zentrale async-Methode für Auflösung
  - `ResolveEffectiveWorkingDirectory()` — Synchrone Pfad-Kombinierung mit Path-Traversal-Schutz
  - `ValidateWorkingDirectory()` — Existenz-Validierung
  
- **`KiAusfuehrungsService`** — Nutzt `WorkingDirectoryResolver` bereits korrekt in `StartCliAsync` und `StartWithPseudoConsoleAsync`
  
- **`ArbeitsverzeichnisOeffnenService`** — Öffnet Verzeichnis im Dateiexplorer; führt selbst KEINE Auflösung durch
  
- **`IdeOeffnenService`** — Findet und öffnet Solutions oder VSCode; führt selbst KEINE Auflösung durch

- **`TaskDetailViewModel`** — Ribbon-Aktionen `OeffneArbeitsverzeichnis()` und `OeffneIdeAsync()`/`OeffneVisualStudioCodeFallback()` sind vorhanden, nutzen aber `WorkingDirectoryResolver` NICHT

### [Tests](inventory/tests.md)

- **`KiAusfuehrungsServiceTests_WorkingDirectory`** — 8 Tests für Auflösung und CLI-Start
  - ✓ Path-Kombination, Path-Traversal-Schutz, Validierung
  - ✓ `StartCliAsync` nutzt effektives Arbeitsverzeichnis korrekt
  
- **`ArbeitsverzeichnisOeffnenServiceTests`** — 4 Tests für Service-Logik (Prozessstart, Validierung)
  
- **`IdeOeffnenServiceTests`** — 10 Tests für Solution-Suche und IDE-Öffnen
  
- **Fehlende Tests:**
  - ❌ Unit-Tests für `TaskDetailViewModel.OeffneArbeitsverzeichnis()` mit `WorkingDirectoryResolver`-Nutzung
  - ❌ Unit-Tests für `TaskDetailViewModel.OeffneVisualStudioCodeFallback()` mit `WorkingDirectoryResolver`-Nutzung
  - ❌ E2E-Tests für Ribbon-Aktionen mit konfiguriertem Arbeitsverzeichnis

---

## Kritische Erkenntnisse

### Problem 1: Ribbon-Aktionen ignorieren Arbeitsverzeichniskonfiguration

**Betroffene Code-Stellen:**
- `TaskDetailViewModel.OeffneArbeitsverzeichnis()` (ca. Zeile 1768–1784)
- `TaskDetailViewModel.OeffneIdeAsync()` (ca. Zeile 1786–1817)
- `TaskDetailViewModel.OeffneVisualStudioCodeFallback()` (ca. Zeile 1819–1842)

**Aktuelles Verhalten:**
```
OeffneArbeitsverzeichnis() 
  → ArbeitsverzeichnisOeffnenService.Oeffne(_aufgabe?.LokalerKlonPfad)  ← nur Repository-Root!
  → explorer.exe öffnet Repository-Root, nicht konfiguriertes Arbeitsverzeichnis
```

**Problem:** `RepositoryStartKonfiguration.WorkingDirectoryRelativePath` wird nie berücksichtigt. Wenn ein Projekt ein Arbeitsverzeichnis wie `src/app` konfiguriert hat, öffnet sich trotzdem der Repository-Root.

### Problem 2: Solutions werden im falschen Verzeichnis gesucht

**Betroffene Code-Stelle:**
- `TaskDetailViewModel.OeffneIdeAsync()` ruft `IdeOeffnenService.FindeSolutions(_aufgabe?.LokalerKlonPfad)` auf

**Problem:** Wenn Solutions im Arbeitsverzeichnis `src/solutions/` liegen, wird die Suche trotzdem im Repository-Root durchgeführt und findet nichts.

### Problem 3: Async/Sync-Mismatch

**Betroffene Code-Stellen:**
- `TaskDetailViewModel.OeffneArbeitsverzeichnis()` ist synchron
- `TaskDetailViewModel.OeffneVisualStudioCodeFallback()` ist synchron
- `WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync()` ist async

**Problem:** Um die Anforderung zu erfüllen, müssen diese ViewModel-Methoden async werden (wegen des async `WorkingDirectoryResolver`-Aufrufs). Die requirement.md stellt diese Frage explizit (Frage 4).

---

## Abhängigkeitsübersicht

```
TaskDetailViewModel (UI)
├── Ruft auf: ArbeitsverzeichnisOeffnenService.Oeffne(pfad)
├── Ruft auf: IdeOeffnenService.FindeSolutions(pfad)
├── Ruft auf: IdeOeffnenService.OeffneVisualStudioCode(pfad)
└── FEHLT: WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync(...)

KiAusfuehrungsService (bereits funktional)
├── Ruft auf: WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync(...)
└── Übergibt aufgelöstes Verzeichnis an: kiPlugin.StartCliAsync(effectiveWorkdir, ...)

WorkingDirectoryResolver (statisch)
├── Nutzt: GitPlugin.ResolveEffectiveRepositoryPathAsync() (optional, für LocalDirectoryPlugin)
└── Prüft: Path-Traversal, Existenz des Verzeichnisses
```

---

## Offene Fragen aus der Anforderung

1. **Zuverlässigkeit der KI-Ausführung:** Sollte eine Regressions-E2E-Test für Ribbon-Aktionen hinzugefügt werden? → **BESTANDSAUFNAHME:** E2E-Test für CLI existiert; Tests für Ribbon-Aktionen fehlen.

2. **GitPlugin-Parameter in Ribbon-Aktionen:** Sollte `IGitPlugin` auch bei Ribbon-Aktionen berücksichtigt werden? → **BESTANDSAUFNAHME:** `KiAusfuehrungsService.StartCliAsync` nutzt bereits `gitPlugin`; Ribbon-Aktionen würden von `gitPlugin: null` profitieren (wie in requirement.md vorgeschlagen).

3. **Fehlerbehandlung für ungültige Konfiguration:** Sollte eine fehlende Arbeitsverzeichniskonfiguration zu aussagekräftiger Fehlermeldung führen? → **BESTANDSAUFNAHME:** Services werfen `DirectoryNotFoundException`; ViewModel-Error-Handler sind teilweise vorhanden.

4. **Async in nicht-async Methoden:** Wie sollte die async `WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync()` in synchronen ViewModel-Methoden genutzt werden? → **BESTANDSAUFNAHME:** Aktuell ungelöst; Anforderung schlägt async-Umwandlung vor oder synchrones Blocking mit `.GetAwaiter().GetResult()`.

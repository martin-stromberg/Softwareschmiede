# Bestandsaufnahme: Logikklassen

## `TaskDetailViewModel`

Datei: `src\Softwareschmiede.App\ViewModels\TaskDetailViewModel.cs`

### Relevante Properties

| Property | Typ | Beschreibung |
|----------|-----|-----------|
| `ShowCliPanel` | `bool` (get) | **KRITISCH:** Gibt an, ob die CLI-Ansicht angezeigt werden soll. Nutzt `AusfuehrungsStatus.SollCliAnzeigen(Status)`. |
| `KannCliNeuStarten` | `bool` (get) | Gibt an, ob die CLI neu gestartet werden kann. Prüft `SollCliAnzeigen` und `!IsCliRunning`. |
| `IsCliRunning` | `bool` (get/set) | Gibt an, ob ein CLI-Prozess läuft. Invalidiert mehrere abhängige Properties bei Änderung. |
| `Aufgabe` | `Aufgabe?` (get/set) | Die geladene Aufgabe. Bei Setzen werden mehrere Properties invalidiert, inkl. `ShowCliPanel`. |
| `AusfuehrungsStatus` | - | (Wird über `_aufgabe.AusfuehrungsStatus` gelesen) |

### Relevante Methoden

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `LadenAsync` | private async | Lädt die Aufgabe und ihre CLI-Session. Prüft `SollCliAnzeigen` und bindet existierende Sessions wieder an. |
| `CliStoppenAsync` | private async | **WICHTIG:** Stoppt den CLI-Prozess, setzt `IsCliRunning = false`, aktualisiert `_aufgabe.AusfuehrungsStatus = Beendet`, invalidiert `ShowCliPanel`. |
| `CliNeustartenAsync` | private async | Startet die CLI neu für bereits vorbereitete Aufgaben. |
| `PluginWechselAsync` | private async | Wechselt das KI-Plugin bei laufender CLI: Dialog, Stop, Restart. |
| `OnCliProcessStatusChanged` | private | Event-Handler für `KiAusfuehrungsService.CliProcessStatusChanged`. |

### Abonnierte Events

| Event | Quelle | Behandlung |
|-------|--------|-----------|
| `KiAusfuehrungsService.CliProcessStatusChanged` | Injiziert | Registriert im Constructor via `_kiService.CliProcessStatusChanged += OnCliProcessStatusChanged` |

### Property-Invalidierungen bei `IsCliRunning`-Änderung

Bei Setzen von `IsCliRunning` werden folgende Properties invalidiert:
- `KannCliStoppen`
- `KannCliNeuStarten`
- `KannAufgabeAbschliessen`
- `KannPromptVorlageSenden`
- `KannSpeichern`
- `KannLoeschen`
- `KannPullRequestErstellen`
- `CanAssignIssue`
- `CanCreateIssue`
- `ShowIssueGroup`
- `KannPromptPlanen`

### Property-Invalidierungen bei `Aufgabe`-Änderung

Bei Setzen von `Aufgabe` werden folgende Properties invalidiert (neben anderen):
- `ShowCliPanel` ← **KRITISCH FÜR DIESE ANFORDERUNG**
- `KannCliNeuStarten`
- `ShowEditPanel`
- `ShowDiffPanel`

---

## `KiAusfuehrungsService`

Datei: `src\Softwareschmiede\Application\Services\KiAusfuehrungsService.cs`

### Relevante Methoden

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `StartCliAsync` | public async | Startet einen klassischen CLI-Prozess für eine Aufgabe. |
| `StartWithPseudoConsoleAsync` | public async | Startet einen CLI-Prozess über Windows Pseudo Console (ConPTY) API. |
| `StopCliAsync` | public async | Stoppt den laufenden CLI-Prozess (SIGTERM → 5s → Kill). |
| `GetPseudoConsoleSession` | public | Gibt die aktive PseudoConsoleSession zurück oder null. |
| `IsRunning` | public | Gibt an, ob ein Prozess für eine Aufgabe läuft. |
| `HandleProcessExitedAsync` | private async | **WICHTIG:** Behandelt Process.Exited Event. Ruft `PersistAusfuehrungBeendetAsync` auf. |
| `PersistAusfuehrungBeendetAsync` | private async | Persistiert Ausführungs-Beendigung via `AufgabeService.AktivenLaufBeendenAsync`. |

### Publizierte Events

| Event | Parameter | Zweck |
|-------|-----------|-------|
| `CliProcessStatusChanged` | `Guid aufgabeId`, `CliProcessStatus status` | Wird ausgelöst wenn Prozess startet, stoppt oder fehlerhafte beendet wird. Status kann `Gestartet`, `Gestoppt`, oder `Fehler` sein. |
| `RunningCountChanged` | `int previous`, `int current` | Wird ausgelöst wenn sich die Anzahl laufender Prozesse ändert. |

### Fehlerbehandlung

- Bei Prozessbeendigung mit Exit-Code != 0: Status `Fehler`, ruft `PersistFehlgeschlagenAsync` auf
- Bei absichtlichem Stopp (`StopCliAsync`): Status `Gestoppt`
- Bei unkontrollierter Beendigung: Status `Gestoppt`

---

## `AufgabeService`

Datei: `src\Softwareschmiede\Application\Services\AufgabeService.cs`

### Relevante Methoden

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetDetailAsync` | public async | Lädt eine Aufgabe mit vollständigen Details. |
| `AktivenLaufBeendenAsync` | public async | **KRITISCH:** Setzt `AusfuehrungsStatus = Beendet`, cleared `AktiveRunId`, `LaufStatus`, `LetzterCliStartUtc`. |
| `AktivenLaufSetzenAsync` | public async | Setzt `AusfuehrungsStatus = Aktiv` und speichert aktiven Lauf-Metadaten. |

### Persistierungs-Punkte

| Kontext | Methode | Effekt auf `AusfuehrungsStatus` |
|---------|---------|--------------------------|
| CLI-Prozess startet | `AktivenLaufSetzenAsync` (vom Starter) | `NichtGestartet` → `Aktiv` |
| CLI-Prozess beendet | `AktivenLaufBeendenAsync` (vom `KiAusfuehrungsService`) | `Aktiv` → `Beendet` |

---

## `EntwicklungsprozessService`

Datei: `src\Softwareschmiede\Application\Services\EntwicklungsprozessService.cs`

### Relevante Methoden

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ProzessStartenAsync` | public async | Repository-Setup für Aufgabe: Klon, Branch, optionales Startskript. Setzt Status auf `Gestartet`. |
| `ProzessStartenUndCliStartenAsync` | public async | Kombiniert Repository-Setup und CLI-Start in einem Schritt. |
| `CliNeustartenAsync` | public async | Startet die KI-CLI erneut im bereits vorbereiteten Klon. |

### Fehlerbehandlung und Rollback

- Bei Fehler in `ProzessStartenUndCliStartenAsync`: Rollback via `RollbackStartAsync`, Status wird zurückgesetzt

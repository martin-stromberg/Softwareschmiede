# CLI-Start und Laufzeitstatus

## Echte CLI-Starts

Der eigentliche Prozessstart erfolgt in `KiAusfuehrungsService`:

- Klassischer Start: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs:153`
- ConPTY-Start: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs:175`
- Gestartet-Event beim ConPTY-Start: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs:230`

Beide Startpfade feuern `CliProcessStatusChanged(..., CliProcessStatus.Gestartet)`.

`CliProcessManager` ist der zentrale Empfaenger dieses Ereignisses:

- Switch auf `CliProcessStatus.Gestartet`: `src/Softwareschmiede/Application/Services/CliProcessManager.cs:128`
- Sofortiges Persistieren des aktiven Laufs: `src/Softwareschmiede/Application/Services/CliProcessManager.cs:133`

Damit ist `CliProcessManager` bzw. `AufgabeService.AktivenLaufSetzenAsync` der beste zentrale Ort, um den neuen "Letzter Start"-Zeitstempel zu setzen. Diese Logik wird nur bei einem echten Start-Event ausgefuehrt, nicht beim blossen Anzeigen einer bestehenden Session.

## Bestehende Lauf-Persistenz

`AufgabeService.AktivenLaufSetzenAsync` setzt aktuell:

- `AktiveRunId`: Methode ab `src/Softwareschmiede/Application/Services/AufgabeService.cs:459`
- `LastHeartbeatUtc = DateTimeOffset.UtcNow`: `src/Softwareschmiede/Application/Services/AufgabeService.cs:465`
- `LaufStatus = AufgabeLaufStatus.Laeuft`: `src/Softwareschmiede/Application/Services/AufgabeService.cs:468`

`UpdateHeartbeatAsync` setzt ebenfalls `LastHeartbeatUtc`:

- `src/Softwareschmiede/Application/Services/AufgabeService.cs:445`

Deshalb darf `LastHeartbeatUtc` nicht als "Letzter Start" wiederverwendet werden.

## Initialer Aufgabenstart

Neue Aufgaben werden ueber `TaskDetailViewModel.StartenAsync` gestartet. Der Ablauf ruft `EntwicklungsprozessService.ProzessStartenUndCliStartenAsync`:

- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs:633`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs:656`
- `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs:120`

`EntwicklungsprozessService` richtet Repository und Branch ein, setzt den Aufgabenstatus ueber `AufgabeService.StartenAsync` und startet danach die KI-CLI:

- Status/Branch/Klonpfad setzen: `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs:493`
- CLI starten: `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs:155`

Der "Letzter Start"-Zeitstempel sollte dennoch nicht in `AufgabeService.StartenAsync` gesetzt werden, weil diese Methode fachlich den Aufgabenstatus startet und nicht zwingend jeden CLI-Neustart abbildet. Die Anforderung bezieht sich explizit auf echte CLI-Neustarts.

## Manuelle und automatische CLI-Neustarts

`TaskDetailViewModel` startet CLI fuer bestehende Aufgaben ueber:

- manueller Neustart: `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs:412`
- automatischer Neustart beim Laden einer gestarteten Aufgabe ohne Prozess: `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs:727`
- gemeinsamer Start-Helfer: `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs:755`

Diese Pfade landen ebenfalls bei `KiAusfuehrungsService.StartWithPseudoConsoleAsync`, loesen also das zentrale `Gestartet`-Event aus.

## Hintergrundaufgabe wieder anzeigen

Beim Laden/Oeffnen einer bereits laufenden Aufgabe wird vorhandene Session abgefragt:

- `GetPseudoConsoleSession`: `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs:271`
- Session im Laden binden: `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs:327`

Wenn der Prozess bereits laeuft, startet `LadenAsync` keine neue CLI. Genau in diesem Fall darf "Letzter Start" nicht aktualisiert werden. Eine Aktualisierung am zentralen `CliProcessStatus.Gestartet`-Event erfuellt diese Abgrenzung.

## Achtung: CanExecute-Ausdruck

`KannCliNeuStarten` ist aktuell:

```csharp
public bool KannCliNeuStarten => _aufgabe?.Status is Domain.Enums.AufgabeStatus.Gestartet
    or Domain.Enums.AufgabeStatus.Wartend
    && !_isCliRunning;
```

Quelle: `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs:142`

Die Operator-Praezedenz sollte bei der Planung geprueft werden. Fachlich ist vermutlich gemeint: Status ist Gestartet oder Wartend, und CLI laeuft nicht.


## `CliProcessStatus`
Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs` (Zeile 589–597)

| Wert | Bedeutung |
|------|-----------|
| `Gestartet` | Prozess läuft |
| `Gestoppt` | Prozess wurde gestoppt |
| `Fehler` | Prozess ist mit einem Fehler beendet |

Wird publiziert über `KiAusfuehrungsService.CliProcessStatusChanged` (Multicast-Delegate `Action<Guid, CliProcessStatus>`), abonniert von `CliProcessManager` und `TaskDetailViewModel`. Der Invoke-Aufruf (Zeile 142 und 304 in `KiAusfuehrungsService.cs`) ist ein einfacher `?.Invoke(...)`-Aufruf ohne Isolation einzelner Abonnenten — wirft ein Abonnent, stoppt die Multicast-Kette und propagiert die Exception zum aufrufenden `process.Exited`-Handler (siehe F9/F10 in [kiausfuehrungsservice.md](kiausfuehrungsservice.md)).

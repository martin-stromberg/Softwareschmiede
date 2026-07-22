# Bestandsaufnahme - Protokoll fuer CLI-Ausgabe

## Kurzfazit

Die Anwendung besitzt bereits ein aufgabenbezogenes Protokollmodell in der Datenbank und `ProtokollService.AddCliOutputAsync` speichert einzelne CLI-Ausgabezeilen als `ProtokollTyp.CliOutput`. Dieser Service ist aber aktuell nicht in den ConPTY-Ausgabepfad eingebunden. Die echte Terminalausgabe wird in `PseudoConsoleSession.ReadLoopAsync` gelesen, per ANSI-Parser in den `TerminalBuffer` geschrieben und ueber `BufferChanged` an `TerminalControl` gemeldet; dort endet der persistente Pfad derzeit.

Der passende technische Ansatzpunkt fuer die Anforderung liegt daher nahe an der ConPTY-Leseschleife oder an einer dort ausgeloesten, aufgabenbezogenen Ausgabe-Senke. `KiAusfuehrungsService.StartWithPseudoConsoleAsync` kennt die `aufgabeId` und erstellt die `PseudoConsoleSession`, waehrend `PseudoConsoleSession` selbst derzeit keine DB- oder Service-Abhaengigkeit hat.

## Detaildokumente

- [ConPTY- und Terminal-Ausgabepfad](inventory/conpty-terminal-output.md)
- [Protokollmodell und ProtokollService](inventory/protokoll-service.md)
- [KiAusfuehrungsService und Prozess-Lifecycle](inventory/ki-ausfuehrungsservice.md)
- [TaskDetailView, TaskDetailViewModel und TerminalControl](inventory/ui-taskdetail-terminal.md)
- [Vorhandene Tests und Testluecken](inventory/tests.md)
- [Implementierungsrelevante Risiken und offene Entscheidungen](inventory/risks-and-decisions.md)

## Relevante Dateien

| Bereich | Datei | Relevanz |
|---|---|---|
| Protokollservice | `src/Softwareschmiede/Application/Services/ProtokollService.cs` | `AddCliOutputAsync`, Rate-Limit-Erkennung, Abruf pro Aufgabe |
| Protokollmodell | `src/Softwareschmiede/Domain/Entities/Protokolleintrag.cs` | DB-Entity mit `AufgabeId`, `Typ`, `Inhalt`, `Zeitstempel` |
| Protokolltyp | `src/Softwareschmiede/Domain/Enums/ProtokollTyp.cs` | `CliOutput` existiert bereits |
| EF-Konfiguration | `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs` | Cascade von Aufgabe zu Protokolleintraegen, Zeitstempel-Konvertierung |
| CLI-Lifecycle | `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs` | Start, Stop, Handle, Events, `PseudoConsoleSession` pro Aufgabe |
| ConPTY-Session | `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs` | zentrale Leseschleife fuer Terminalausgabe |
| UI-Renderer | `src/Softwareschmiede.App/Controls/TerminalControl.cs` | rendert Buffer, sendet Tastatureingaben |
| ViewModel | `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs` | laedt Protokoll, startet/bindet Sessions |
| View | `src/Softwareschmiede.App/Views/TaskDetailView.xaml(.cs)` | bindet `TerminalControl.Session`, zeigt Protokoll-Liste |

## Wichtigste Befunde

1. `ProtokollService.AddCliOutputAsync(Guid aufgabeId, string outputLine, ...)` speichert pro Aufruf einen `CliOutput`-Eintrag und optional zusaetzlich einen `RateLimit`-Eintrag, wenn ein Marker erkannt wird.
2. `PseudoConsoleSession.ReadLoopAsync` verarbeitet Output chunkweise als Bytes. Es gibt keinen Aufruf von `ProtokollService.AddCliOutputAsync` und keine andere Persistenz der Roh- oder Zeilenausgabe im Terminalpfad.
3. `KiAusfuehrungsService.StartWithPseudoConsoleAsync` ist der Ort, an dem `aufgabeId`, Prozess, `PseudoConsoleSession` und DI-Kontext zusammenkommen.
4. `TaskDetailViewModel.LadenAsync` laedt Protokolleintraege nur beim Laden in `Protokolleintraege`; neu persistierte CLI-Ausgabe wuerde ohne zusaetzlichen UI-Refresh nicht automatisch in der Info-Protokollliste erscheinen.
5. Vorhandene Tests decken `AddCliOutputAsync`, Rate-Limit-Marker, Terminal-Buffer-Synchronisierung, Sessionwechsel und ConPTY-Lifecycle ab. Eine Integration, die echten oder simulierten ConPTY-Output automatisch in `Protokolleintraege` persistiert, fehlt.


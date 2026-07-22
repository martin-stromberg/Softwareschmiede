# Protokollmodell und ProtokollService

## Datenmodell

`Protokolleintrag` enthaelt `Id`, `AufgabeId`, `Typ`, `Inhalt`, optional `AgentName`, `Zeitstempel`, Navigation zur Aufgabe und Test-/Diff-Beziehungen (`src/Softwareschmiede/Domain/Entities/Protokolleintrag.cs:6`).

`ProtokollTyp` enthaelt bereits `CliOutput` fuer "Ausgabezeile eines eingebetteten CLI-Prozesses" und `RateLimit` fuer erkannte Marker (`src/Softwareschmiede/Domain/Enums/ProtokollTyp.cs:21`, `:24`).

EF konfiguriert:

- `DbSet<Protokolleintrag>` (`src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs:27`)
- Cascade von Aufgabe zu Protokolleintraegen (`SoftwareschmiededDbContext.cs:144`)
- String-Konvertierung fuer `Typ` und Unix-Millis fuer `Zeitstempel` (`SoftwareschmiededDbContext.cs:163`, `:166`)

## Service-Verhalten

`ProtokollService.GetByAufgabeAsync` laedt alle Eintraege einer Aufgabe chronologisch mit TestErgebnissen (`src/Softwareschmiede/Application/Services/ProtokollService.cs:25`).

`AddEintragAsync` ist der generische Eintragspfad fuer Prompt, KiAntwort, GitAktion, SystemMeldung usw. (`ProtokollService.cs:37`).

`AddCliOutputAsync`:

- erstellt einen `Protokolleintrag` mit `Typ = ProtokollTyp.CliOutput` (`ProtokollService.cs:128`, `:134`)
- schreibt `outputLine` unveraendert nach `Inhalt` (`ProtokollService.cs:135`)
- setzt `Zeitstempel = DateTimeOffset.UtcNow` (`ProtokollService.cs:136`)
- prueft auf `[[SOFTWARESCHMIEDE_RATE_LIMIT...]]` und legt optional einen zweiten `RateLimit`-Eintrag an (`ProtokollService.cs:141`, `:147`, `:151`)
- ruft pro Ausgabe `SaveChangesAsync` auf (`ProtokollService.cs:163`)

## Konsequenzen fuer CLI-Vollprotokoll

`AddCliOutputAsync` ist fachlich passend, aber technisch auf "eine Zeile pro Aufruf" und einzelne DB-Transaktion je Aufruf zugeschnitten. Bei starkem Terminaloutput kann das viele `SaveChangesAsync`-Aufrufe erzeugen. Ein Plan sollte pruefen, ob Pufferung/Batches noetig sind oder ob die erwartete CLI-Ausgabemenge klein genug ist.

Die Methode speichert keine Prozess-Metadaten, Run-ID, Sequenznummer, Stream-Art oder ANSI/Rohtext-Kennzeichnung. Die Requirement-Datei nennt diese Punkte als offene Fragen; das bestehende Modell bietet dafuer aktuell keine eigenen Felder.


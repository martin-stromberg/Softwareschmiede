# Services und Lebenszyklus

## Aufgabenstart

[`TaskDetailViewModel.StartenAsync`](../../../../../src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs) loest `EntwicklungsprozessService.ProzessStartenUndCliStartenAsync` aus. Der kombinierte Service klont das Repository, richtet Branch und Startskript ein, setzt die Aufgabe auf `Gestartet`, speichert den Klonpfad und startet anschliessend die KI-CLI via `KiAusfuehrungsService.StartWithPseudoConsoleAsync`. Bei Fehler oder Abbruch erfolgt Rollback auf `Neu` und das Klonverzeichnis wird geloescht.

Der bestehende Startpfad ist damit auf einen Erststart mit Repository-Vorbereitung zugeschnitten. Ein manueller Neustart einer bereits vorbereiteten Aufgabe nutzt dagegen `CliNeustartenAsync` und startet nur die CLI im vorhandenen Klon.

## Stoppen und Prozessende

[`KiAusfuehrungsService.StopCliAsync`](../../../../../src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs) beendet den laufenden Prozess kontrolliert und notfalls per Kill. Das Prozessende loest `CliProcessStatusChanged` aus. Bei absichtlichem oder erfolgreichem Ende wird kein Gesamtstatus geaendert; bei Fehler wird ein Protokolleintrag geschrieben und der Gesamtstatus bleibt ebenfalls `Gestartet`.

[`TaskDetailViewModel.CliStoppenAsync`](../../../../../src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs) setzt danach nur UI-Zustand wie `IsCliRunning` und den CLI-Namen zurueck. Persistiert wird kein eigener Zustand fuer `beendet`.

## Endgueltiges Beenden

[`EntwicklungsprozessService.AbschliessenAsync`](../../../../../src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs) prueft offene To-Dos, loescht den lokalen Klon und ruft danach `AufgabeService.AbschliessenAsync` auf. Dieser setzt `AufgabeStatus.Beendet`, Abschlussdatum und leert Branch/Klonpfad. Dieser Pfad entspricht fachlich der geforderten Trennung, muss aber gegen laufende CLI-Prozesse und den neuen Ausfuehrungsstatus abgegrenzt werden.

## Recovery und Autostart

[`AufgabeRecoveryService`](../../../../../src/Softwareschmiede/Application/Services/AufgabeRecoveryService.cs) betrachtet Aufgaben mit Gesamtstatus `Gestartet` oder `Wartend`, altem Heartbeat und ohne laufenden Prozess als Recovery-Kandidaten. Die Logik kennt keinen separaten beendeten KI-Lauf und muss bei der Planung mit dem neuen Status abgestimmt werden.

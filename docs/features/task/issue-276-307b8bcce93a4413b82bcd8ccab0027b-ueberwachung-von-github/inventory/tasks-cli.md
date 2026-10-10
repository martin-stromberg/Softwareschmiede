# Aufgaben-, CLI- und Queue-Bestand

## Aufgabenstatus und Prozessstatus

`Aufgabe` speichert den fachlichen Aufgabenstatus (`Neu`, `Gestartet`, `Wartend`, `Beendet`, `Archiviert`) getrennt vom Ausführungsstatus der KI (`NichtGestartet`, `Aktiv` usw.). `AktiveRunId`, `LastHeartbeatUtc`, `LetzterCliStartUtc` und `LaufStatus` beschreiben einen gestarteten oder laufenden CLI-Prozess. Eine Aufgabe kann also fachlich aktiv bleiben, obwohl gerade kein CLI-Prozess läuft; das passt grundsätzlich zur Anforderung, eine Aufgabe nach der Ereignisbearbeitung aktiv zu lassen.

`AufgabeService` enthält Statusänderungen, Aufgabenabfragen und Laufpersistenz. `CliProcessManager` synchronisiert Prozessereignisse mit diesen Daten. Die Aktivitätsabfragen gruppieren Status `Gestartet` und `Wartend` als aktiv/wartend. Vor einer Umsetzung ist daher zu klären, welche bestehenden Übergänge beim Aktivieren einer `Neu`-Aufgabe vorgesehen sind und wie abgeschlossene/archivierte Aufgaben behandelt werden.

## CLI- und Terminalstart

`KiAusfuehrungsService.StartTerminalSessionAsync` startet eine Terminal-Sitzung für eine Aufgabe und bindet Prozessausgabe sowie Lebenszyklus an `CliProcessManager`. `EntwicklungsprozessService` enthält etablierte Startabläufe, die Aufgabenstatus, Arbeitsbereich und Terminalstart koordinieren. Plugin-Auswahl und Standard-KI-Plugin werden über `PluginSelectionService`, `PluginManager` und gespeicherte Plugin-Einstellungen aufgelöst.

Diese Dienste bilden den geeigneten Ausgangspunkt für den Start einer CLI aus einem SCM-Ereignis. Ein solcher Start benötigt einen lokalen Arbeitsbereich und die zur Aufgabe aufgelöste KI-Konfiguration. Die Anforderung verlangt außerdem einen Auswahl-Dialog beim Öffnen eines Queue-Eintrags; ob ein bestehender Auswahl-Dialog als wiederverwendbarer Application-/UI-Flow existiert, muss bei der Planung an den ViewModels und UI-Aktionen geprüft werden.

## Übergabe an laufende Prozesse

Der Bestand verwaltet terminalisierte CLI-Prozesse pro Aufgabe und persistiert ihren Laufstatus. In den untersuchten Verträgen ist jedoch keine explizite API zum Übermitteln eines neuen Benutzerprompts an eine bereits laufende Sitzung enthalten. Vor der Implementierung muss ermittelt werden, ob die Terminal-Sitzung Eingabe über ihren PseudoConsole-Input unterstützt und wie die UI diesen Pfad nutzt. Das ist ein Kernpunkt, da die Anforderung gerade die Weiterleitung an eine laufende CLI fordert.

## Queues

Vorhandene Queue-Konstrukte sind technische Puffer, etwa die begrenzte Channel-Queue des `CliOutputProtokollWriter`. Eine persistierte Queue für ausstehende Ereignisse mit Öffnen/Auswahl/Abarbeiten ist in den geprüften Application-/Domain-Bereichen nicht erkennbar. Für die fachliche Ereignis-Queue fehlen daher Datenmodell, Persistenz, Statusübergänge, UI-Einstieg, Wiederaufnahme und Fehler-/Duplikatbehandlung.

## Testbestand

Tests decken `AufgabeService`, `KiAusfuehrungsService`, Entwicklungsprozess- und ViewModel-Abläufe ab. Die Integration neuer Aktionen sollte diese vorhandenen Ebenen nutzen; für Queue-Öffnen ist zusätzlich ein UI-/E2E-Test des konkreten Benutzerflusses erforderlich, sofern eine neue UI-Seite oder ein Dialog entsteht.

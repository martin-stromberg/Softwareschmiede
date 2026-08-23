# Offene Aufgaben

Erstellt am: 2026-08-23
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine.

## Code-Review-Befunde

- [ ] `DirectoryStructureBrowserService.cs`: Die neue öffentliche Methode `GetFileLoadResultAsync` hat keine direkten Unit-Tests. In `DirectoryStructureBrowserServiceTests.cs` analoge Tests ergänzen, mindestens: Erfolg mit gemischten Einträgen (nur Dateien werden zurückgegeben, Verzeichnisse werden ausgefiltert), Fehlerstatus bei Plugin-Exception, sowie ein Test, der belegt, dass `GetDirectoryLoadResultAsync` und `GetFileLoadResultAsync` für dieselbe Repository-URL unabhängige Cache-Einträge verwenden (cacheKeyPrefix "dirs:" vs. "files:").
- [ ] `ProjektService.cs`: Die neue öffentliche Methode `SaveRepositoryInitialisierungskriptAsync` hat keine direkten Unit-Tests in `ProjektServiceTests.cs`. Analog zu den `SaveRepositoryStartKonfigurationAsync`-Tests ergänzen, mindestens: Neuanlage, Update einer bestehenden Konfiguration, Löschen einer bestehenden Konfiguration bei `null`/Leerstring-Eingabe, Validierungsfehler bei absolutem Pfad, und Fehler bei unbekannter `repositoryId`.

## Fehlgeschlagene Tests

Keine.

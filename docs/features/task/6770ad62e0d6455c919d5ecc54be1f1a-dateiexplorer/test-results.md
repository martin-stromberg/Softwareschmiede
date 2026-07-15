# Test-Ergebnisse

## Ergebnis

**Status:** Fehler vorhanden (nachweislich nicht Dateiexplorer-bezogen, Umgebungsproblem)

## Zusammenfassung (nach Merge von `main`)

- Gesamt: 996
- Bestanden: 994
- Fehlgeschlagen: 1
- Übersprungen: 1

## Fehlgeschlagene Tests

- `E2E_WorkingDirectory.RepositoryZuweisen_MitFehlgeschlagenemStrukturabruf_ZeigtTextBoxUndSpeichertManuellenPfad_E2E`
  — `System.UnauthorizedAccessException` beim `Directory.Delete(repositoryPath, recursive: true)` in Zeile 76 des
  Tests (Betriebssystem-Dateisperre, z. B. durch Virenscanner oder einen noch laufenden Git-Prozess auf ein Objekt
  im `.git`-Verzeichnis des geklonten Test-Repositories). Reproduziert konsistent über mehrere isolierte Läufe.
  `E2E_WorkingDirectory.cs` ist von diesem Branch nicht verändert (`git diff main...HEAD` zeigt keine Änderungen
  an dieser Datei oder an verwandten Infrastruktur-/Service-Dateien für den Repository-Klon-Vorgang). Ein reines
  Betriebssystem-/Sandbox-Dateisperrenproblem, keine Dateiexplorer-Regression.

`E2E_FileExplorer` (inkl. Regressionstest und neuem „Datei öffnen"-Button) wurde mehrfach isoliert ausgeführt und
bestand jedes Mal vollständig. Der volle Testlauf nach dem Merge von `main` zeigt insgesamt nur diesen einen,
umgebungsbedingten Fehlschlag.

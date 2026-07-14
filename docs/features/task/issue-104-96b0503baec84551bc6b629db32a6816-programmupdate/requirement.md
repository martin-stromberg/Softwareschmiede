# Kundenanforderung - Programmupdate

## Fachliche Zusammenfassung

Die Anwendung soll selbststaendig erkennen, ob eine neue Programmversion verfuegbar ist. Neue Versionen werden als GitHub-Releases im Repository `martin-stromberg/Softwareschmiede` veroeffentlicht:

https://github.com/martin-stromberg/Softwareschmiede/releases

Wenn eine neue Version verfuegbar ist, soll im linken Menue am unteren Rand ein Update-Button erscheinen. Der Button startet einen gefuehrten Update-Prozess:

1. Vor dem Update wird geprueft, ob aktive CLI-Ausfuehrungen existieren, die aktuell nicht auf Benutzereingabe warten.
2. Falls solche aktiven Ausfuehrungen vorhanden sind, muss eine Sicherheitsabfrage den Update-Prozess noch abbrechen koennen.
3. Die neue Version wird als ZIP-Datei heruntergeladen.
4. Das ZIP wird in ein temporaeres Verzeichnis innerhalb des Programmverzeichnisses entpackt.
5. Eine Skriptdatei wird erzeugt oder bereitgestellt und anschliessend gestartet.
6. Das Skript beendet die laufende Anwendung.
7. Das Skript verschiebt alle Dateien aus dem temporaeren Entpack-Verzeichnis an ihren Zielort im Programmverzeichnis.
8. Das Skript startet die Anwendung danach erneut.

Ziel ist ein automatisierter In-App-Update-Ablauf, der fuer Benutzer sichtbar nur dann angeboten wird, wenn tatsaechlich eine neuere Version verfuegbar ist, und der laufende CLI-Arbeiten nicht unbemerkt unterbricht.

## Betroffene Klassen und Komponenten

### Anwendung und Navigation
- Linkes Hauptmenue / Seitenleiste der WPF-Anwendung
- Hauptfenster-ViewModel und Navigationszustand
- UI-Command fuer den Update-Button
- Sichtbarkeit des Update-Buttons nur bei verfuegbarem Update

### Update-Erkennung
- Neuer oder erweiterter Update-Service zur Abfrage der GitHub-Releases
- Versionsvergleich zwischen aktuell laufender Anwendung und neuestem verfuegbaren Release
- Fehlerbehandlung fuer Netzwerkfehler, nicht erreichbare GitHub-API oder fehlende Release-Artefakte
- Persistenz oder Caching des letzten Pruefergebnisses, falls bereits vorhandene Infrastruktur dafuer existiert

### Download und Entpacken
- Download des Release-ZIP-Artefakts
- Ablage im Programmverzeichnis oder in einem Unterverzeichnis davon
- Temp-Verzeichnis im Programmverzeichnis fuer entpackte Update-Dateien
- Validierung, dass das heruntergeladene Archiv vollstaendig und entpackbar ist
- Aufraeumen unvollstaendiger oder alter Update-Dateien

### CLI-Ausfuehrungen und Prozessstatus
- Erkennung aktiver CLI-Ausfuehrungen
- Unterscheidung zwischen:
  - CLI laeuft aktiv und wartet nicht auf Eingabe
  - CLI laeuft, wartet aber vermutlich auf Benutzereingabe
  - keine relevante CLI-Ausfuehrung aktiv
- Sicherheitsabfrage, wenn ein Update laufende aktive CLI-Ausfuehrungen unterbrechen koennte
- Bereits vorhandene Runtime-Status-Erkennung fuer CLI-Sitzungen, insbesondere Statuswerte fuer aktive oder wartende Prozesse

### Update-Skript und Neustart
- Skriptdatei fuer den finalen Austausch der Programmdateien
- Beenden der laufenden Anwendung aus einem externen Prozess heraus
- Verschieben oder Ersetzen der entpackten Dateien im Programmverzeichnis
- Neustart der Anwendung nach erfolgreichem Austausch
- Logging und Fehlerbehandlung im Skript, soweit technisch sinnvoll

## Implementierungsansatz

### Update-Pruefung

Die Anwendung benoetigt einen Hintergrundmechanismus oder einen Startzeitpunkt, an dem sie GitHub Releases prueft. Der Service soll die aktuell installierte Version mit der neuesten veroeffentlichten Release-Version vergleichen.

Empfohlene fachliche Regeln:
- Der Update-Button erscheint nur, wenn eine hoehere Version verfuegbar ist.
- Pre-Releases sollen nur beruecksichtigt werden, wenn dies explizit konfiguriert oder bereits fachlich gewuenscht ist.
- Die Anwendung soll bei Netzwerkfehlern normal weiterlaufen und keinen Update-Button anzeigen oder eine dezente Fehlerspur protokollieren.
- Die Update-Pruefung soll keine laufenden Aufgaben blockieren.

### Benutzerfuehrung im Menue

Der Update-Button soll im linken Menue am unteren Rand erscheinen. Er soll nicht dauerhaft sichtbar sein, wenn kein Update verfuegbar ist.

Beim Klick startet der Update-Ablauf:
- Erneute Pruefung, ob das Update noch verfuegbar ist.
- Pruefung aktiver CLI-Ausfuehrungen.
- Bei riskanten laufenden Ausfuehrungen: Sicherheitsabfrage mit Abbrechen-Option.
- Danach Download und Vorbereitung des Updates.
- Start des externen Update-Skripts.

### Schutz laufender CLI-Ausfuehrungen

Vor dem Update muss die Anwendung pruefen, ob aktive CLI-Ausfuehrungen existieren, die nicht auf Eingabe warten. Diese Pruefung soll auf vorhandener Prozess- und Runtime-Status-Infrastruktur aufbauen.

Wichtig ist die fachliche Unterscheidung:
- Eine aktiv arbeitende CLI darf nicht stillschweigend durch ein Update beendet werden.
- Eine CLI, die auf Eingabe wartet, kann je nach vorhandener Produktlogik weniger kritisch sein, soll aber im Plan explizit bewertet werden.
- Der Benutzer muss bei kritischem Zustand den Update-Prozess abbrechen koennen.

### Download und Vorbereitung

Das Release-ZIP wird in ein kontrolliertes Update-Verzeichnis heruntergeladen und in ein temporaeres Verzeichnis im Programmverzeichnis entpackt. Vor dem Umschalten soll sichergestellt werden:
- Das ZIP wurde vollstaendig heruntergeladen.
- Das Archiv konnte erfolgreich entpackt werden.
- Das temporaere Ziel enthaelt die erwarteten Programmdateien.
- Ein bereits vorhandener alter Temp-Ordner blockiert das Update nicht oder wird kontrolliert bereinigt.

### Externes Update-Skript

Da die laufende Anwendung ihre eigenen Dateien nicht zuverlaessig ersetzen kann, benoetigt der finale Austausch einen externen Prozess. Dieser Prozess soll:

1. Warten oder pruefen, bis die Hauptanwendung beendet ist.
2. Dateien aus dem Temp-Verzeichnis in das Programmverzeichnis verschieben oder kopieren.
3. Alte Dateien ersetzen.
4. Bei Erfolg die Anwendung erneut starten.
5. Bei Fehlern einen nachvollziehbaren Zustand hinterlassen.

Das Skript kann beispielsweise als PowerShell- oder Batch-Skript umgesetzt werden, sofern das zur Zielplattform und zum bestehenden Deployment passt.

## Technische Randbedingungen

- Zielplattform ist die bestehende Windows/WPF-Anwendung.
- Das Update muss mit dem tatsaechlichen Installations- bzw. Programmverzeichnis der laufenden Anwendung umgehen koennen.
- Das Programmverzeichnis kann je nach Installationsort Schreibrechte erfordern.
- Die laufende Anwendung darf den finalen Dateiaustausch nicht selbst ausfuehren, wenn dadurch geladene Assemblies oder gesperrte Dateien ersetzt werden muessten.
- Der Update-Prozess muss mit parallelen oder eingebetteten CLI-Prozessen kompatibel sein.
- GitHub-Releases muessen ein geeignetes ZIP-Artefakt enthalten, das die Anwendung eindeutig identifizieren kann.

## Akzeptanzkriterien

1. Die Anwendung prueft automatisch, ob unter `https://github.com/martin-stromberg/Softwareschmiede/releases` eine neuere Version verfuegbar ist.
2. Ist keine neuere Version verfuegbar, wird kein Update-Button im linken Menue angezeigt.
3. Ist eine neuere Version verfuegbar, wird am unteren Rand des linken Menues ein Update-Button angezeigt.
4. Beim Start des Updates wird geprueft, ob aktive CLI-Ausfuehrungen existieren, die nicht auf Eingabe warten.
5. Wenn solche CLI-Ausfuehrungen existieren, erscheint eine Sicherheitsabfrage, ueber die der Benutzer das Update abbrechen kann.
6. Wird das Update fortgesetzt, wird das Release-ZIP heruntergeladen.
7. Das heruntergeladene ZIP wird in ein temporaeres Verzeichnis im Programmverzeichnis entpackt.
8. Eine Skriptdatei wird aufgerufen, die die laufende Anwendung beendet.
9. Das Skript verschiebt oder ersetzt die Programmdateien aus dem Temp-Ordner in das Programmverzeichnis.
10. Nach dem Austausch startet das Skript die Anwendung erneut.
11. Fehler beim Pruefen, Herunterladen, Entpacken oder Austauschen fuehren nicht zu einem unkontrolliert teilweise aktualisierten Zustand.

## Offene Fragen

1. **Versionsquelle:** Soll die aktuell installierte Version aus der Assembly-Version, einer Datei im Programmverzeichnis oder einer vorhandenen Anwendungskonfiguration gelesen werden?

2. **Release-Auswahl:** Sollen GitHub Pre-Releases ignoriert werden, oder duerfen sie als Update angeboten werden?

3. **ZIP-Artefakt:** Wie heisst das erwartete ZIP-Asset im GitHub-Release, und gibt es pro Release genau ein passendes Artefakt?

4. **Berechtigungen:** Wird die Anwendung immer aus einem beschreibbaren Programmverzeichnis gestartet, oder muss der Update-Prozess mit erhoehten Rechten umgehen?

5. **Skriptformat:** Soll das Update-Skript als PowerShell-, Batch- oder plattformspezifisches Skript bereitgestellt werden?

6. **Rollback:** Soll bei Fehlern waehrend des Dateiaustauschs ein Rollback auf die alte Version versucht werden, oder reicht ein protokollierter Fehlerzustand?

7. **Update-Zeitpunkt:** Soll die automatische Update-Pruefung nur beim Programmstart laufen oder auch periodisch waehrend der Anwendungslaufzeit?

8. **CLI-Wartestatus:** Welche bestehende Statusdefinition gilt verbindlich als "wartet auf Eingabe", und ab welchem Status muss die Sicherheitsabfrage erscheinen?

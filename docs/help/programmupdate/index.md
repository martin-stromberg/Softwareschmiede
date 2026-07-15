# Programmupdate

Die WPF-Anwendung prüft beim Start einmalig das neueste stabile GitHub-Release von `martin-stromberg/Softwareschmiede`. Grundlage für den Versionsvergleich ist ausschließlich die Datei `version.json` im Programmverzeichnis. Pre-Releases werden nicht angeboten; erwartet wird das Release-Asset `release.zip`.

`version.json` liegt im Root des installierten Programmverzeichnisses neben `Softwareschmiede.exe`. Die Datei enthält mindestens das Feld `version`; zusätzlich können `tagName`, `commit` und `createdAtUtc` enthalten sein. Fehlt die Datei, ist sie nicht lesbar oder enthält sie keine gültige Version, gilt die Update-Prüfung als nicht prüfbar und es wird kein Update-Button angezeigt.

## Bedienung

- Der Update-Button erscheint unten in der linken Seitenleiste nur, wenn eine höhere stabile Version verfügbar ist.
- Der Refresh-Button in der Seitenleiste startet die Prüfung manuell erneut. Während einer laufenden Prüfung oder Update-Vorbereitung sind die Update-Kommandos deaktiviert.
- Beim Starten eines Updates prüft die Anwendung erneut, ob das Update noch verfügbar ist. Ist es nicht mehr verfügbar oder nicht prüfbar, wird der Update-Button ausgeblendet und der Ablauf endet.
- Vor dem Update prüft die Anwendung aktive CLI-Aufgaben. Aufgaben mit `AktiveRunId` und Status ungleich `WartetAufEingabe` gelten als riskant; dazu gehört auch ein unbekannter Status. Bei solchen Aufgaben erscheint eine Sicherheitsabfrage mit Abbruchmöglichkeit.
- Während Download, Entpacken und Skriptvorbereitung zeigt ein Fortschrittsdialog den aktuellen Schritt. Bei bekannter Download-Größe wird ein Prozentwert angezeigt, sonst ein unbestimmter Fortschritt. Der Dialog kann abgebrochen werden, solange das externe Update-Skript noch nicht gestartet wurde.

## Ablauf

1. Das neueste stabile GitHub-Release wird über die GitHub-API geprüft. Das Asset muss `release.zip` heißen.
2. `release.zip` wird zuerst als temporäre `.download`-Datei heruntergeladen und nach erfolgreichem Download nach `{Programmverzeichnis}\updates\download\release.zip` verschoben.
3. Der Inhalt wird in `{Programmverzeichnis}\updates\extracted\{version}\` entpackt.
4. Die Anwendung validiert, dass `Softwareschmiede.exe` und `version.json` im Root des entpackten Pakets vorhanden sind.
5. `{Programmverzeichnis}\updates\update.ps1` wird erzeugt und gestartet.
6. Nach erfolgreichem Skriptstart beendet die Anwendung sich selbst.
7. Das Skript wartet kurz auf das Beenden der laufenden Anwendung, beendet sie nach Timeout, kopiert die entpackten Dateien ins Programmverzeichnis und startet `Softwareschmiede.exe` neu.

## Fehler und Logs

- Prüf-, Download-, Entpack-, Validierungs- und Skriptstartfehler brechen den Ablauf ab; die laufende Anwendung bleibt geöffnet.
- Fehler beim manuellen Refresh werden als Hinweis zur Prüfung behandelt und blenden keinen Update-Button ein.
- Unvollständige Downloads und das versionsspezifische Entpack-Verzeichnis werden bei Vorbereitungsfehlern innerhalb des `updates`-Verzeichnisses bereinigt.
- Wenn das Programmverzeichnis nicht beschreibbar ist, startet der externe Updater mit Windows-Elevation (`runas`), sodass eine UAC-Abfrage erscheinen kann. Wird diese Abfrage abgebrochen oder kann PowerShell nicht gestartet werden, bleibt die Anwendung geöffnet und das Update wird nicht gestartet.
- Das Skript schreibt nach `{Programmverzeichnis}\updates\update.log`.
- Es gibt kein automatisches Rollback. Fehler beim externen Austausch bleiben im Log nachvollziehbar; die Anwendung kann in diesem Fall bereits beendet sein und startet nur dann neu, wenn das Skript den Austausch erfolgreich abgeschlossen hat.

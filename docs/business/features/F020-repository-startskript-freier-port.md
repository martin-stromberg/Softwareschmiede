# F020 – Repository-Startskript mit freier Portzuweisung

## Einleitung

Mit dieser Funktion können Sie für jedes Repository ein Startskript hinterlegen.
Beim Start einer Aufgabe führt die Softwareschmiede das Skript im Aufgaben-Workspace aus.
Die Portlogik liegt vollständig im Skript selbst (z. B. im parameterlosen `start.ps1`).

---

## Wer nutzt es?

Diese Funktion ist für Entwickler und Projektverantwortliche gedacht, die mehrere lokale Branch-Workspaces parallel betreiben und dafür stabile, konfliktfreie Startkonfigurationen benötigen.

---

## Schritt-für-Schritt-Anleitung

1. Öffnen Sie ein Projekt und wechseln Sie auf die Detailseite.
2. Wählen Sie in der Repository-Tabelle beim gewünschten Repository **⚙️ Startskript**.
3. Tragen Sie den relativen Skriptpfad ein (z. B. `scripts/start.ps1`).
4. Optional: Hinterlegen Sie zusätzliche Skriptargumente.
5. Speichern Sie die Konfiguration.
6. Starten Sie anschließend den Entwicklungsprozess der Aufgabe.

### Lokaler Visual-Studio-Debug mit `start.ps1`

Wenn Sie direkt im Hauptrepository debuggen (ohne Aufgaben-Workflow), nutzen Sie das Skript im Repo-Root:

1. PowerShell im Repository öffnen.
2. `.\start.ps1` ausführen.
3. Danach in Visual Studio F5 starten.

Das Skript erkennt relevante Web-Projekte autonom und setzt je Projekt einen freien HTTP-Port.

Wichtige Exit-Codes:

- `0`: erfolgreich
- `10`: `launchSettings.json` fehlt
- `11`: JSON/Profil ungültig
- `12`: Port ungültig oder belegt
- `13`: Schreiben fehlgeschlagen
- `99`: unerwarteter Fehler

---

## Beispiel

Sie konfigurieren für ein Repository das Skript `scripts/start.ps1`.
Beim Prozessstart führt die Anwendung das Skript aus.
Das Skript aktualisiert die lokale Startkonfiguration des Branch-Workspaces eigenständig, ohne andere Branches zu beeinflussen.

---

## Was passiert im Hintergrund?

- Die Konfiguration wird repositorybezogen gespeichert (`RepositoryStartKonfiguration`).
- Beim Prozessstart wird zuerst geklont und der Task-Branch erstellt.
- `RepositoryStartskriptService` führt `powershell.exe` mit Skriptpfad aus.
- Bei Fehlern (Skript fehlt, Port ungültig, Skriptlauf fehlschlägt) bricht der Start kontrolliert ab.

---

## Häufige Fragen (FAQ)

**Muss das Skript im Repository liegen?**  
Ja. Es sind nur relative Pfade innerhalb des Repository-Baums erlaubt.

**Was passiert, wenn das Skript deaktiviert ist?**  
Dann wird beim Prozessstart kein Skript ausgeführt.

**Kann ich einen festen Port erzwingen?**  
Nicht über den App-Service-Vertrag. Die Portstrategie wird vom jeweiligen Skript festgelegt.

**Wie erhält mein Skript den reservierten Port?**  
Die Anwendung übergibt keine Portsteuerdaten. Das Skript ermittelt seine Ports selbst.

---

## Verwandte Funktionen

- [F003 – KI-Entwicklungsprozess](./F003-ki-entwicklungsprozess.md)
- [F001 – Projektverwaltung](./F001-projektverwaltung.md)
- [F017 – Lokales Verzeichnis Plugin](./F017-lokales-verzeichnis-plugin.md)
- [F016 – Fehlerbehandlung & Recovery](./F016-fehlerbehandlung-und-recovery.md)
- [Technischer Skriptvertrag: `start.ps1` für VS-Debug](../../api/start-ps1-visual-studio-freier-http-port.md)
- [Zurück zur Übersicht](../features.md)

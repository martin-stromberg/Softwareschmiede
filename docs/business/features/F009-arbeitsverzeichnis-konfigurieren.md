# F009 – Arbeitsverzeichnis für lokale Repositories konfigurieren

## Einleitung

Mit dieser Funktion legen Sie fest, **wo lokale Repository-Klone auf Ihrem Rechner abgelegt werden**.
So können Sie Speicherort, Laufwerk und Backup-Strategie an Ihre Arbeitsweise anpassen.

Ohne eigene Einstellung nutzt die Softwareschmiede automatisch den System-Temp-Bereich als Basis.

---

## Wer nutzt es?

Alle Nutzer, die die Softwareschmiede lokal betreiben und den Speicherort der Arbeitskopien steuern möchten.

Typische Fälle:

- Trennung von Quellcode und System-Temp-Verzeichnis
- Nutzung eines schnelleren Laufwerks (z. B. SSD)
- Nutzung eines festen Pfads für bessere Nachvollziehbarkeit

---

## Schritt-für-Schritt

1. Öffnen Sie **Einstellungen**.
2. Tragen Sie unter **Arbeitsverzeichnis** einen absoluten Pfad ein (z. B. `D:\Repos`).
3. Klicken Sie auf **Speichern**.
4. Für den Default klicken Sie auf **Default verwenden** (Feld wird geleert).

---

## Was passiert im Hintergrund?

- Der Wert wird als globale App-Einstellung `repositories.workdir` gespeichert.
- Beim Start einer Aufgabe wird zur Laufzeit geprüft, ob der Pfad nutzbar ist.
- Der eigentliche Klonpfad enthält immer einen Softwareschmiede-Unterordner und die Aufgaben-ID:

`<Basispfad>\softwareschmiede\<aufgabeId>`

Beispiele:

- Konfiguriert: `D:\Repos` → `D:\Repos\softwareschmiede\<aufgabeId>`
- Default/Fallback: `<Temp>` → `<Temp>\softwareschmiede\<aufgabeId>`

Hinweis: Das **Local Directory**-Plugin besitzt kein separates `WorkingDirectory`-Setting.
Das in dieser Funktion konfigurierte Basis-Arbeitsverzeichnis steuert daher auch den Zielpfad für `SeparateWorkingDirectory`.

Wenn der Modus `SeparateWorkingDirectory` genutzt wird, greift beim Start eine feste Git-Workflow-Fallback-Logik:

- Git-Quelle vorhanden → Arbeitskopie per Clone
- Keine Git-Quelle + Init-Bestätigung aktiv → `git init` in der Quelle, danach Clone
- Keine Git-Quelle + keine Init-Bestätigung → sichere Dateikopie als Fallback

---

## Validierung und Fallback

- Nur **absolute und gültige Pfade** werden akzeptiert.
- Wenn ein gespeicherter Pfad später nicht nutzbar ist (z. B. Laufwerk nicht verfügbar), verwendet die Anwendung automatisch den Fallback.
- In diesem Fall erscheint ein Hinweis in den Einstellungen, damit Sie den Pfad korrigieren können.
- Wenn der Zielordner einer Arbeitskopie bereits befüllt ist, wird der Start aus Sicherheitsgründen abgebrochen (kein Überschreiben).

---

## Grenzen und offene Punkte

- Das Arbeitsverzeichnis steuert den Speicherort, ersetzt aber keine Remote-Git-Funktionen.
- Sehr große Verzeichniskopien können durch Schutzgrenzen (Dateien, Datenmenge, Laufzeit) gestoppt werden.
- Für konkurrierende gleichzeitige Synchronisationsvorgänge auf identischen Pfaden werden weitere Leitplanken fortlaufend verfeinert.

---

## Hinweise für bestehende Projekte (Migration)

- Die Funktion ist rückwärtskompatibel.
- Bereits angelegte Aufgaben behalten ihren bisherigen Klonpfad.
- Neue Prozessstarts verwenden den aktuell aufgelösten Basispfad.

---

## Häufige Fragen (FAQ)

**Muss ich das Arbeitsverzeichnis setzen?**  
Nein. Ohne Eingabe wird automatisch der Default verwendet.

**Werden bestehende Klone verschoben, wenn ich den Pfad ändere?**  
Nein. Die Änderung gilt für zukünftige Prozessstarts.

**Was passiert bei ungültigem Pfad?**  
Das Speichern wird mit einer verständlichen Fehlermeldung verhindert.

---

## Verwandte Funktionen

- [F001 – Projektverwaltung](./F001-projektverwaltung.md)
- [F003 – KI-Entwicklungsprozess](./F003-ki-entwicklungsprozess.md)
- [F014 – Standardplugin je Pluginart & KI-Plugin-Auswahl](./F014-standardplugin-ki-plugin-auswahl.md)
- [F015 – Einstellungen & Persistenz](./F015-einstellungen-und-persistenz.md)
- [F016 – Fehlerbehandlung & Recovery](./F016-fehlerbehandlung-und-recovery.md)
- [F017 – Lokales Verzeichnis Plugin](./F017-lokales-verzeichnis-plugin.md)
- [Zurück zur Übersicht](../features.md)

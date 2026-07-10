# Programmsymbol — Beschreibung

← [Zurück zur Übersicht](index.md)

## Zweck

Das Programmsymbol macht die Softwareschmiede im Windows-System visuell erkennbar. Das Hammer-/Spitzhacken-Symbol wird in drei Kontexten angezeigt:

- **Windows-Explorer:** Datei-Icon für `Softwareschmiede.App.exe`
- **Taskleiste:** Symbol in der Taskleistenschaltfläche
- **Fenster-Titelleiste:** Symbol neben dem Fenstertitel

## Funktionsweise

Das Symbol ist eine Multi-Resolution-Icon-Datei (`.ico`-Format) mit vier Auflösungen:

| Auflösung | Kontext |
|-----------|---------|
| 16×16 Pixel | Explorer-Listenansicht, Suchindizes |
| 32×32 Pixel | Taskleiste, Fenster-Titelleiste |
| 64×64 Pixel | große Explorer-Symbole |
| 256×256 Pixel | Kachel-Ansicht, Thumbnail-Cache |

Das Symbol wird beim Build über die MSBuild-Property `<ApplicationIcon>` automatisch als Win32-Ressource in die ausführbare Datei (`Softwareschmiede.App.exe`) eingebettet. Zusätzlich wird es in der WPF-Anwendung durch das `Icon`-Attribut des Hauptfensters explizit referenziert, um die Darstellung in allen Kontexten zu garantieren.

**Grafik-Inhalt:** Dunkler Kreis mit gelb/orangem überkreuztem Hammer- und Spitzhacken-Symbol — die visuelle Markenidentität des Projekts.

## Beispiele

Nach der Installation oder dem Build der Anwendung:

1. Öffne Windows Explorer und navigiere zum Build-Verzeichnis (`bin\Release` oder `bin\Debug`).
2. Die Datei `Softwareschmiede.App.exe` zeigt das Hammer-Symbol an statt des generischen Windows-Icons.
3. Starte die Anwendung — das Symbol erscheint in der Taskleiste neben dem Fenstertitel.
4. Wenn die Anwendung mehrmals geöffnet ist, nutzt jedes Fenster das gleiche Symbol.

## Einschränkungen

- **Windows-Icon-Cache:** Der Windows-Explorer cacht Icons im `IconCache.db`-Dateibank-Index. Bei Änderungen am Icon kann der Cache auf einigen Systemen einen alten Wert anzeigen, bis der Cache gelöscht wird oder das System neu gestartet wird. Dies ist kein Fehler der Anwendung, sondern normales Windows-Verhalten.
- **Nur visuelle Funktion:** Das Icon hat keine interaktiven Funktionen und kann nicht vom Anwender konfiguriert werden — es ist fest in das Projekt eingebettet.

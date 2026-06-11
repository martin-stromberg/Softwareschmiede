📘 Lastenheft – Softwareschmiede

1. Zielsetzung der Anwendung
Die Anwendung Softwareschmiede dient der Verwaltung von Entwicklungsprojekten und deren Aufgaben. Sie ersetzt eine bestehende Webanwendung und wird als WPF‑Desktopanwendung für Windows 11 neu entwickelt.
2. Zielgruppe
    • Primäre Zielgruppe: der Entwickler selbst (Einzelanwender).
    • Sekundäre Zielgruppe: Freunde oder andere Entwickler, die die Anwendung ebenfalls nutzen möchten.
    • Keine Mehrbenutzerfähigkeit erforderlich.
    • Keine Rechteverwaltung: der Anwender hat stets Vollzugriff.
3. Einsatzumgebung
    • Betriebssystem: Windows 11
    • Ausführung ausschließlich lokal auf der Entwicklungsmaschine
    • Portable Installation (keine Setup‑Routine notwendig)
    • Keine Server‑ oder Remote‑Komponenten
4. Datenhaltung
    • Speicherung aller Daten in einer lokalen SQLite‑Datenbank
    • Keine parallele Mehrbenutzerunterstützung
    • Gespeichert werden:
        ◦ Projekte
        ◦ Aufgaben
        ◦ Statushistorie
        ◦ Plugin‑Einstellungen
        ◦ Anwendungseinstellungen
        ◦ Protokolle der KI‑CLI‑Ausgaben
5. Architektur & Plugin-System
5.1 Plugin‑Konzept
    • Plugins liegen als DLLs im Unterverzeichnis /plugins
    • Keine Hot‑Reload‑Funktionalität
    • Plugins implementieren definierte Interfaces:
        ◦ IGitProvider
        ◦ IAiCliProvider
    • Plugins dürfen keine eigenen Konfigurationsdateien mitbringen
    • Plugin‑Einstellungen werden über die Anwendung verwaltet
5.2 Plugin‑Einstellungen
Jedes Plugin kann eigene Eigenschaften definieren, z. B.:
    • API‑Keys
    • Tokens
    • Anmeldedaten
    • Pfade zu CLI‑Programmen
    • Feature‑Flags
Die Anwendung generiert automatisch passende UI‑Eingabefelder anhand der Typen der Eigenschaften.
6. Git‑Plugin – Funktionsumfang
Ein Git‑Plugin muss mindestens folgende Funktionen bereitstellen:
    • Repository‑Liste abrufen
    • Issues abrufen
    • Repository klonen
    • Commit erstellen
    • Push ausführen
    • Pull ausführen
    • Pull Request erstellen
    • Issue einem Pull Request zuordnen
Nicht erforderlich:
    • Issues erstellen
7. KI‑CLI‑Plugins – Funktionsumfang
7.1 Allgemeines
    • Die Anwendung startet die CLI als externen Prozess
    • Kommunikation erfolgt abhängig vom Plugin:
        ◦ Standard Input/Output
        ◦ Parameterübergabe
        ◦ CLI‑spezifische Mechanismen
    • Plugins müssen definierte Schnittstellen implementieren
7.2 Anforderungen
    • Parameter müssen an die CLI übergeben werden können
    • Plugins können eigene Einstellungen definieren (z. B. API‑Keys, Tool‑Freischaltungen)
    • Die Anwendung muss erkennen:
        ◦ ob die CLI aktiv arbeitet → Status „In Arbeit“
        ◦ ob sie auf Eingaben wartet → Status „Wartend“
7.3 Einschränkung paralleler KI‑Ausführungen
Einstellung im Bereich „KI‑Ausführung“:
    • Deaktiviert (keine Einschränkung)
    • Pro Aufgabe (max. 1 Prozess pro Aufgabe)
    • Pro KI‑Anbieter (z. B. nur 1 Copilot‑CLI gleichzeitig)
    • Insgesamt (nur 1 KI‑Prozess global)
7.4 Protokollierung
    • Jede CLI‑Ausgabe wird in einem Aufgabenprotokoll gespeichert
8. Benutzeroberfläche
8.1 Hauptfenster
Aufteilung:
    • Titelleiste (oben)
    • Menübereich (links, einklappbar)
    • Inhaltsbereich (zentral)
8.2 Menübereich
Einträge:
    • Dashboard
    • Projekte
        ◦ Unterpunkte: alle Projekte
    • Einstellungen
Zusätzlich:
    • Liste aller Aufgaben mit Status „In Arbeit“ oder „Wartend“, sortiert nach letzter Statusänderung (absteigend)
8.3 Dark Mode
    • Die Anwendung unterstützt einen Dark Mode
    • Standardmodus kann in den Einstellungen gewählt werden
9. Dashboard
Das Dashboard zeigt:
    • Anzahl Projekte
    • Anzahl offener Aufgaben
    • Liste der zuletzt geänderten Aufgaben (absteigend nach Statusänderung)
    • Direkter Aufruf der Aufgabenansicht
10. Projekte
10.1 Projektübersicht
    • Tabelle aller Projekte
    • Aktionen:
        ◦ Erstellen
        ◦ Bearbeiten
        ◦ Löschen
    • Auswahl eines Projekts öffnet die Projektansicht
10.2 Projekt
Ein Projekt hat:
    • Name
    • Beschreibung
    • Optional: Verknüpfung mit einem Git‑Repository (1:1)
10.3 Git‑Integration
    • Auswahl eines Git‑Plugins
    • Anzeige der verfügbaren Repositories
    • Auswahl eines Repositories
10.4 Aufgabenliste
    • Aufgaben erstellen, bearbeiten, archivieren, löschen
    • Aufgaben aus Git‑Issues erzeugen (Eigenschaften werden übernommen)
11. Aufgaben
11.1 Eigenschaften
    • Titel
    • Beschreibung
    • Protokoll (CLI‑Ausgaben)
    • Status
11.2 Statusmodell
Eine Aufgabe kann folgende Status haben:
    • Neu
    • Arbeitsverzeichnis eingerichtet
    • Gestartet
    • In Arbeit
    • Wartend
    • Beendet
    • Archiviert
11.3 Statusübergänge
    • Neu → Arbeitsverzeichnis eingerichtet Automatisch, wenn:
        ◦ Projekt Git‑Repo hat
        ◦ Einstellung „Arbeitsverzeichnis automatisch verwalten“ aktiv ist → Repository wird geklont
    • Arbeitsverzeichnis eingerichtet → In Bearbeitung Durch Start einer KI‑CLI
    • In Bearbeitung → In Arbeit / Wartend Automatisch anhand der CLI‑Ausgabe
    • In Bearbeitung → Beendet Durch Button „Beenden“ (CLI‑Prozess wird beendet)
    • Beendet → Archiviert Durch Button „Archivieren“
    • Archiviert → In Arbeit Durch Start einer neuen CLI
11.4 Automatisches Wiederherstellen
Wenn eine Aufgabe den Status „In Arbeit“ hat, aber kein Prozess existiert:
    • Anwendung startet automatisch eine neue CLI‑Instanz
    • Status bleibt konsistent
11.5 UI‑Verhalten
    • Beschreibung ist ab Status ≠ „Neu“ über ein Info‑Overlay abrufbar
    • CLI‑Fenster wird eingebettet angezeigt
    • Beim Verlassen der Aufgabenansicht:
        ◦ CLI‑Fenster wird versteckt, Prozess läuft weiter
12. Einstellungen
12.1 Quellcodeverwaltung
    • Auswahl Standard‑Git‑Plugin
    • Arbeitsverzeichnis
    • Arbeitsverzeichnis automatisch verwalten (Ja/Nein)
    • Unterregister: Einstellungen der geladenen Git‑Plugins
12.2 KI‑Ausführung
    • Ereignismeldungen:
        ◦ Deaktiviert
        ◦ Banner
        ◦ Ton
    • Benutzerdefinierter Hinweiston (Upload: .mp3, .wav, .ogg)
    • Auswahl Standard‑KI‑Plugin
    • Einschränkung paralleler KI‑Ausführungen
    • Unterregister: Einstellungen der geladenen KI‑Plugins
13. Logging
    • Aktivierbar in den Einstellungen
    • Logdatei enthält:
        ◦ Fehler
        ◦ Warnungen
        ◦ Prozessstarts
        ◦ Plugin‑Ladevorgänge
        ◦ Statuswechsel
14. Plugin‑Vorgaben (Erweiterung)
14.1 Allgemeine Anforderungen an Plugins
    • Plugins müssen definierte Interfaces implementieren:
        ◦ IGitProvider
        ◦ IAiCliProvider
    • Plugins werden als DLLs im Verzeichnis /plugins abgelegt.
    • Keine Hot‑Reload‑Funktionalität.
    • Keine eigenen Konfigurationsdateien.
    • Plugins können eigene Einstellungseigenschaften definieren, die automatisch im Einstellungsbereich angezeigt werden.
    • Plugins müssen vollständig isoliert sein und dürfen keine Abhängigkeiten außerhalb ihres eigenen Projektverzeichnisses benötigen (außer Standard‑.NET‑Bibliotheken).
15. Quellcodeverwaltungs‑Plugins
15.1 Bereitgestellte Standard‑Plugins
Folgende Git‑Plugins müssen implementiert und mit der Anwendung ausgeliefert werden:
    1. GitHub
    2. BitBucket
    3. Lokales Verzeichnis
Alle drei Plugins werden beim Kompilieren der Hauptanwendung automatisch mitgebaut und in das Build‑Plugin‑Verzeichnis kopiert.
15.2 Funktionsumfang der Git‑Plugins
15.2.1 GitHub & BitBucket
Diese Plugins müssen folgende Funktionen bereitstellen:
    • Repository‑Liste abrufen
    • Issues abrufen
    • Repository klonen
    • Commit erstellen
    • Push ausführen
    • Pull ausführen
    • Pull Request erstellen
    • Issue einem Pull Request zuordnen
Nicht erforderlich:
    • Issues erstellen
15.2.2 Plugin „Lokales Verzeichnis“
Dieses Plugin dient der Arbeit mit einem lokalen Quellverzeichnis ohne echten Git‑Server.
Besonderheiten:
    • Ein „Klon“ ist eine Dateikopie des Quellverzeichnisses in das Arbeitsverzeichnis.
    • Wenn das Quellverzeichnis selbst ein Git‑Repository enthält:
        ◦ Im Arbeitsverzeichnis wird ein Arbeitsbranch erstellt (nur organisatorisch).
        ◦ Commits werden lokal verwaltet.
    • Ein „Pull“ ist eine Dateioperation, bei der Änderungen aus dem Arbeitsverzeichnis zurück in das Quellverzeichnis kopiert werden.
    • Kein Merge‑Handling.
    • Kein Pull Request.
    • Keine Server‑Interaktion.
16. KI‑Plugins
16.1 Bereitgestellte Standard‑Plugins
Folgende KI‑CLI‑Plugins müssen implementiert und mit der Anwendung ausgeliefert werden:
    1. GitHub Copilot CLI
    2. Claude CLI
16.2 Funktionsumfang der KI‑Plugins
    • Starten der jeweiligen CLI als externer Prozess.
    • Übergabe von Parametern (z. B. Autopilot‑Modus, Tool‑Freischaltungen).
    • Auslesen der CLI‑Ausgabe (stdout/stderr).
    • Erkennen von:
        ◦ aktiver Verarbeitung → Status „In Arbeit“
        ◦ Wartezustand → Status „Wartend“
    • Falls die CLI es unterstützt:
        ◦ Fortsetzen einer vorherigen Session beim Start.
16.3 Prozessverwaltung
    • Pro Aufgabe darf nur eine CLI laufen.
    • Abhängig von den Einstellungen kann die Anwendung weitere parallele KI‑Prozesse verhindern:
        ◦ Deaktiviert
        ◦ Pro Aufgabe
        ◦ Pro KI‑Anbieter
        ◦ Insgesamt
    • Die CLI‑Fenster müssen eingebettet werden können (Fensterhandle wird bereitgestellt).
17. Build‑ und Entwicklungsprozess
17.1 Build‑Integration der Plugins
    • Die Plugin‑Projekte sind Teil der Solution.
    • Beim Kompilieren der Hauptanwendung werden alle Plugin‑Projekte automatisch mitkompiliert.
    • Die erzeugten DLLs werden automatisch in das Build‑Verzeichnis der Hauptanwendung kopiert:
        ◦ bin/<Configuration>/plugins/
    • Beim Start aus Visual Studio sind alle Plugins sofort verfügbar.
17.2 Projektstruktur (Vorgabe)
Empfohlene Struktur:
Code
/Softwareschmiede
  /Softwareschmiede.App
  /Softwareschmiede.Core
  /Softwareschmiede.Tests
  /Softwareschmiede.E2E
  /Plugins
    /GitHub
    /BitBucket
    /LocalDirectory
    /Copilot
    /Claude
18. Testanforderungen
18.1 Unit‑Tests
    • Alle Klassen mit Programmlogik müssen durch Unit‑Tests abgedeckt werden.
    • Ziel: hohe Testabdeckung, insbesondere:
        ◦ Statuswechsel der Aufgaben
        ◦ Plugin‑Ladeprozess
        ◦ Prozessverwaltung der KI‑CLI
        ◦ Git‑Operationen (Mocking der Dateisystem‑ und API‑Zugriffe)
        ◦ Einstellungen und Validierungen
        ◦ Datenbankzugriffe (SQLite‑In‑Memory)
18.2 End‑to‑End‑Tests
    • E2E‑Tests müssen sicherstellen, dass:
        ◦ UI‑Elemente korrekt angezeigt werden
        ◦ Statuswechsel sichtbar sind
        ◦ Aufgaben korrekt erstellt, bearbeitet und archiviert werden
        ◦ Plugins korrekt geladen werden
        ◦ KI‑CLI‑Prozesse gestartet und eingebettet werden
    • Möglichst wenig Mocking:
        ◦ SQLite‑Datenbank real
        ◦ Dateisystem real (Testverzeichnis)
        ◦ CLI‑Prozesse können durch Test‑Dummies ersetzt werden, aber nicht durch reine Mocks
18.3 Testautomatisierung
    • Tests sollen automatisiert ausführbar sein (z. B. via GitHub Actions).
    • E2E‑Tests müssen headless‑fähig sein oder einen Testmodus besitzen.

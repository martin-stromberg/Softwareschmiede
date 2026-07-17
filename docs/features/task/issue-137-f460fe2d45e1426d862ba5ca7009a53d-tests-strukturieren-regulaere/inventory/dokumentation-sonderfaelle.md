# Dokumentation und bekannte Sonderfaelle

## Vorhandene Dokumentation

Relevante vorhandene Hilfedokumente:

- `docs/help/terminal/`
- `docs/help/stabilitaet/`
- `docs/CI_CD.md`
- `README.md`
- `CLAUDE.md`

`docs/help/terminal/` beschreibt ConPTY, TerminalControl, Clipboard-Paste und bekannte Terminal-Abhaengigkeiten bereits fachlich. Eine zentrale Test-Sonderfalldokumentation fuer `OsInterface` existiert noch nicht.

## Bereits dokumentierte Sonderfaelle

### ConPTY-Verfuegbarkeit

`src/Softwareschmiede.Tests/E2E/ConPtyEnvironmentProbe.cs` dokumentiert den aktuellen Umgang mit ConPTY-E2E-Tests:

- keine automatische Laufzeit-Erkennung
- explizite Steuerung ueber `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`
- Hintergrund: automatische Probes lieferten in einer funktionierenden Umgebung falsch negative Ergebnisse

### Clipboard

`TerminalControlTests.ClipboardPaste.cs` dokumentiert, dass die Windows-Zwischenablage eine prozessuebergreifende, systemweite Ressource ist und `CLIPBRD_E_CANT_OPEN` transient auftreten kann. Der Testcode nutzt deshalb Retry beim Setzen/Leeren der Zwischenablage.

### Dateisystem-Locks

Vorhandene Feature-Artefakte nennen `UnauthorizedAccessException` bei `Directory.Delete(repositoryPath, recursive: true)`. Das ist kein zentraler Projektleitfaden, aber ein belegter Problemfall fuer Test-Cleanup.

## Geeigneter Zielort fuer zentrale Dokumentation

Naheliegende Ablage:

- `docs/help/stabilitaet/os-interface-tests.md`

Alternative:

- `docs/help/terminal/testing.md`, falls der Fokus nur Terminal/ConPTY waere

Da die Anforderung auch Clipboard, Prozessstart, Dateisystem und CI umfasst, ist `docs/help/stabilitaet/os-interface-tests.md` passender.

## Inhalte der neuen Dokumentation

Die zentrale Dokumentation sollte enthalten:

- Definition regulaerer Tests vs. OS-Schnittstellen-Tests
- Kategorie-Konvention `Category=OsInterface`
- Liste der Testgruppen, die OS-Schnittstellen beruehren
- lokale Befehle fuer regulaere und OS-Schnittstellen-Laeufe
- CI-Verhalten: blockierender Pflichtlauf vs. optionaler/best-effort Lauf
- bekannte Fehlerbilder:
  - ConPTY nicht verfuegbar
  - `CLIPBRD_E_CANT_OPEN`
  - Dateisystem-Locks bei Cleanup
  - Testhost-/Build-Artefakt-Rennen
- gueltige Env-Flags:
  - `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`
- Retry-Regeln:
  - kein Retry fuer regulaere Tests
  - Retry nur fuer bekannte OS-/Infrastrukturfehler und separat ausweisen

## Dokumentationsabhaengigkeiten

Wenn Kategorien geaendert werden, sollten mindestens folgende Stellen angepasst werden:

- `.github/workflows/test.yml` Kommentare und Befehle
- `README.md` Testabschnitt, falls vorhanden
- `docs/CI_CD.md`
- `CLAUDE.md`, Abschnitt Testing
- Kommentare in E2E-Testdateien, die aktuell `Category!=E2E` nennen

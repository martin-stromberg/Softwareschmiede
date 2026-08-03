← [Zurück zur Übersicht](index.md)

# Basis-Branch-Konfiguration — Beschreibung

## Zweck

Die Basis-Branch-Konfiguration ermöglicht es, pro Git-Repository einen benutzerdefinierten Branch festzulegen, von dem neue Feature-Branches für Aufgaben abgezweigt werden. Dadurch können Staging-Branches, Release-Branches oder andere Entwicklungszweige als Basis für Aufgabenarbeit verwendet werden, statt automatisch vom Remote-Standard-Branch (`main`, `master` etc.) zu branchen.

## Funktionsweise

### Konfiguration beim Repository-Zuweisen

Wenn Sie ein Git-Repository zu einem Projekt zuweisen, zeigt der Repository-Zuordnungs-Dialog ein Textfeld „Basis-Branch für Feature-Branches". Dieses Feld ist optional:

- **Leer lassen:** Der Remote-Standard-Branch des Repositories wird verwendet (Standardverhalten).
- **Basis-Branch eingeben:** Der eingegebene Branch-Name wird konfiguriert; die verfügbaren Remote-Branches können aus einer Dropdown-Liste ausgewählt oder manuell eingegeben werden.

Die Konfiguration wird beim Speichern des Dialogs persistiert.

### Validierung beim Aufgabenstart

Wenn eine Aufgabe für das Repository gestartet wird, validiert die Anwendung, ob der konfigurierte Basis-Branch im Remote-Repository existiert:

- **Branch existiert:** Der neue Feature-Branch wird vom konfigurierten Basis-Branch abgezweigt. Aufgabenstart ist erfolgreich.
- **Branch existiert nicht:** Aufgabenstart schlägt mit einer Fehlermeldung fehl (z.B. „Branch 'staging' existiert nicht im Repository"). Der Benutzer kann dann die Repository-Konfiguration anpassen.

Die Validierung erfolgt vor dem Repository-Klon — so werden fehlerhafte Konfigurationen früh erkannt.

### Feature-Branch-Erstellung

Beim Starten der Aufgabe wird der neue Feature-Branch nicht mehr vom aktuellen HEAD des Standard-Branches abgezweigt, sondern vom konfigurierten Basis-Branch:

```
git checkout -b <feature-branch-name> origin/<basis-branch>
```

Dies stellt sicher, dass die Aufgabenarbeit auf der korrekten Basis aufgebaut wird.

### Pull-Request-Ziel

Wenn Sie einen Pull Request aus der Aufgabe heraus erstellen, wird der konfigurierte Basis-Branch automatisch als Ziel-Branch (`base`) verwendet:

- **Mit Basis-Branch konfiguriert:** PR wird gegen den konfigurierten Basis-Branch erstellt.
- **Keine Konfiguration:** PR wird gegen den Remote-Standard-Branch erstellt (Fallback).

Dies ermöglicht nahtlose Workflows mit nicht-Standard-Entwicklungszweigen.

### Nachträgliche Bearbeitung

Die Basis-Branch-Konfiguration kann jederzeit in der Projektdetailansicht geändert werden:

1. Projekt öffnen → Projektdetailansicht
2. Repository in der Liste auswählen
3. Neben dem Repositorynamen: „Bearbeiten" klicken
4. Neuen Basis-Branch eingeben oder auswählen
5. „Speichern" klicken

Die neue Konfiguration wird sofort aktiv für alle zukünftigen Aufgabenstarts.

## Beispiele

### Staging-Branch-Workflow

Sie haben ein Repository mit Branches `main`, `develop` und `staging`:

1. Repository zum Projekt hinzufügen
2. Basis-Branch auf `staging` setzen
3. Aufgaben starten → Feature-Branches werden von `staging` abgezweigt
4. Pull Requests → automatisch gegen `staging` erstellt

Dies ermöglicht einen stufenweisen Merge-Prozess: Aufgaben → `staging` → Test → `main`.

### Release-Branch-Verwaltung

Sie arbeiten an einem Release-Branch `release/1.5.0`:

1. Basis-Branch auf `release/1.5.0` setzen
2. Hotfixes und Release-Tweaks werden direkt auf diesem Branch durchgeführt
3. Feature-Branches basieren auf dem Release-Stand, nicht auf `main`
4. PRs gehen direkt in den Release-Branch zurück

## Einschränkungen

- **Lazy-Validierung:** Der konfigurierte Basis-Branch wird erst beim Aufgabenstart validiert, nicht beim Speichern der Konfiguration. Dies erlaubt Szenarien, in denen der Branch später erstellt wird, aber auch die Möglichkeit, eine ungültige Konfiguration temporär zu speichern.
- **Keine automatische Aktualisierung:** Wenn der konfigurierte Basis-Branch später gelöscht wird, schlägt die nächste Aufgabe mit Fehlermeldung fehl — die Konfiguration wird nicht automatisch zurückgesetzt.
- **Single Basis-Branch:** Pro Repository kann nur ein Basis-Branch konfiguriert werden. Mehrere Basis-Branches pro Repository sind nicht unterstützt.
- **Branch-Liste in UI:** Die Auswahl verfügbarer Branches erfolgt über eine Dropdown-Liste oder manuelle Eingabe; die Liste wird beim Auswählen des Repositories geladen, kann aber veraltet sein, wenn sich Branches in der Zwischenzeit ändern.

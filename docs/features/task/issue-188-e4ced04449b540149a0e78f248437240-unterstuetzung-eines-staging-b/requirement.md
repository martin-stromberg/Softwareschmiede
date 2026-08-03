# Anforderung

## Fachliche Zusammenfassung

Bei der Zuweisung eines Git-Repositories zu einem Projekt soll ein Basis-Branch konfigurierbar sein, auf dem neue Feature-Branches für Aufgaben basieren. Standardgemäß ist der Hauptbranch (`main` oder `master`) vorausgewählt. Wenn eine Aufgabe gestartet und ein lokaler Repository-Klon erstellt wird, wird der neue Feature-Branch vom konfigurierten Basis-Branch abgezweigt. Existiert der konfigurierte Basis-Branch nicht im Remote-Repository, wird eine Fehlermeldung geworfen und das Starten der Aufgabe scheitert. Die Basis-Branch-Auswahl kann nachträglich für ein Repository geändert werden, und Pull Requests, die aus Aufgaben heraus erstellt werden, verwenden den konfigurierten Basis-Branch als Ziel-Branch statt automatisch den Standard-Branch des Repositories.

## Betroffene Klassen und Komponenten

### Datenmodell

- `GitRepository` – neue Eigenschaft für den konfigurierten Basis-Branch-Namen (z. B. `DefaultSourceBranchName` oder `BaseBranchName`)
- `RepositoryStartKonfiguration` – Erweiterung um die Basis-Branch-Auswahl (alternativ: eigenes Konfigurationsobjekt)
- Datenbank-Migration zur Ergänzung des neuen Feldes in der `git_repository`-Tabelle

### Persistierung und Repository-Zugriff

- Datenbankzugriff und Entity Framework-Mapping für das neue Feld
- Persistierungs-Layer zur Verwaltung der Basis-Branch-Konfiguration pro Repository

### Git-Operationen und Validierung

- `RepositoryStartskriptService` oder ähnlicher Service – Anpassung beim Erstellen von Feature-Branches, um statt des Remote-Standard-Branches den konfigurierten Basis-Branch zu verwenden
- Validierungslogik beim Starten einer Aufgabe: Prüfung, ob der konfigurierte Basis-Branch im Remote-Repository existiert
- Git-Fehlerbehandlung: Falls der Branch nicht existiert, aussagekräftige Fehler mit Rückgabecode, die in der Aufgabenverwaltung verarbeitet werden können

### Pull-Request-Integration

- GitHub-Plugin (oder allgemeines PR-Plugin): Anpassung beim Erstellen von Pull Requests, um den konfigurierten Basis-Branch statt des Remote-Standard-Branches als Ziel (`base`) zu verwenden
- Bestehende Ribbon-Action oder PR-Erstell-Logik: Übergabe des Basis-Branches an die Plugin-API

### Benutzeroberfläche

- Projekt-Detailansicht oder Repository-Zuordnungs-Formular: UI-Steuerelement zum Auswählen oder Eingeben des Basis-Branches (z. B. Dropdown oder Textfeld mit Autocomplete)
- Validierungs-Feedback bei Eingabe (z. B. "Branch nicht im Repository vorhanden")
- Anzeige des aktuell konfigurierten Basis-Branches bei Repository-Details
- Fehlerbehandlung im UI bei Aufgabenstart (klare Meldung, wenn der konfigurierte Basis-Branch nicht existiert)

### Tests

- Unit-Tests für die Basis-Branch-Validierung
- Unit-Tests für die Basis-Branch-Auswahl beim Feature-Branch-Erstellen
- Integration-Tests für die Datenpersistenz
- E2E-Tests für Workflows: Repository zuordnen mit Basis-Branch-Auswahl, Aufgabe starten, Feature-Branch erstellen
- E2E-Tests für PR-Erstellung mit konfigurierten Basis-Branch als Ziel

## Implementierungsansatz

### 1. Datenmodell erweitern

Ergänze `GitRepository` um ein neues Feld (z. B. `DefaultSourceBranchName: string?`), das den Namen des Basis-Branches speichert. Bei `null` oder leerem String wird der Remote-Standard-Branch verwendet (Abwärtskompatibilität). Alternativ kann das Feld in `RepositoryStartKonfiguration` ergänzt werden, wenn diese als centraler Konfigurationsort für alle Repository-Einstellungen genutzt werden soll.

### 2. Persistierung und Datenzugriff

- Schreibe eine EF Core-Migration zur Ergänzung der Spalte in der Datenbank.
- Aktualisiere den DbContext und die Repositorys, um Lesezugriff und Schreibzugriff auf das neue Feld zu ermöglichen.

### 3. Validierung beim Repository-Konfigurieren

Beim Zuordnen eines Repositories zu einem Projekt oder beim Ändern der Basis-Branch-Auswahl:
- Wenn der Benutzer einen Basis-Branch eingibt, validiere, ob dieser im Remote-Repository existiert (Abfrage über Git oder Plugin-API).
- Zeige eine Validierungsmeldung im UI, falls der Branch nicht existiert.
- Erlaube das Speichern auch mit einem nicht-existierenden Branch (Szenario: Branch wird später erstellt), oder blockiere es strikt (je nach Anforderung; siehe offene Fragen).

### 4. Feature-Branch-Erstellung beim Aufgabenstart

Passe `RepositoryStartskriptService.CreateFeatureBranch()` an:
- Lies die Basis-Branch-Konfiguration aus `GitRepository`.
- Verzweige den Feature-Branch vom konfigurierten Basis-Branch statt vom Remote-Standard-Branch (`git checkout -b <feature-branch> <remote>/<base-branch>`).
- Werfe eine aussagekräftige Fehlermeldung, wenn der konfigurierte Basis-Branch nicht existiert (z. B. `GitBranchNotFoundException` oder ähnlich).

### 5. Pull-Request-Erstellung

Passe die Logik zum Erstellen von Pull Requests an (GitHub-Plugin oder Ribbon-Action):
- Lies die Basis-Branch-Konfiguration aus `GitRepository`.
- Übergebe den konfigurierten Basis-Branch als `base`-Parameter an die GitHub-API oder das Plugin-Interface.
- Fallback auf Remote-Standard-Branch, falls keine Konfiguration vorhanden ist (Abwärtskompatibilität).

### 6. Benutzeroberfläche

- Ergänze das Formular zur Repository-Zuordnung (Projekt-Detailansicht oder separates Modal) um ein Eingabefeld für den Basis-Branch.
- Verwende einen Vorschlag-Autocomplete oder ein Dropdown, das verfügbare Branches aus dem Repository lädt (falls praktikabel).
- Zeige den aktuellen Basis-Branch in der Repository-Übersicht an.
- Bei Fehler beim Aufgabenstart (Basis-Branch nicht existiert): Zeige eine klare Fehlermeldung und Navigationsoptionen (z. B. zu Repository-Konfiguration).

## Konfiguration

- **Datenbank**: Neues optionales Feld `DefaultSourceBranchName` in der `git_repository`-Tabelle.
- **Anwendungseinstellungen**: Ggf. ein Projekt-weiter Standard-Basis-Branch, falls nicht pro Repository konfiguriert (z. B. über `Projekt.DefaultSourceBranchName`); optional je nach Anforderung.
- **UI**: Die Branch-Auswahl ist Teil der Repository-Konfiguration beim Zuordnen oder Bearbeiten eines Repositories.

## Offene Fragen

1. **Basis-Branch bei Default entfernen**: Was soll passieren, wenn der Basis-Branch gelöscht wird? Soll die Konfiguration auf `null` zurückgesetzt werden, oder soll eine Fehlermeldung geworfen werden?

2. **Validierungszeitpunkt**: Soll die Existenz des Basis-Branches sofort beim Speichern validiert werden (stricte Validierung), oder erst beim Aufgabenstart (lazy Validierung)? Lazy ermöglicht Szenarien, in denen der Branch später erstellt wird; strict verhindert Überraschungen.

3. **Repository-Standard-Branch**: Soll die Anwendung den Default-Branch des Repositories (z. B. aus GitHub API) automatisch ermitteln und vorschlagen, oder soll der Benutzer manuell eingeben/auswählen?

4. **Autocomplete / Branch-Liste**: Soll die UI eine Live-Liste der verfügbaren Branches im Repository anbieten (Abfrage über Git oder GitHub-API), oder nur ein Textfeld mit manueller Eingabe?

5. **Abwärtskompatibilität**: Für bestehende Repositories ohne Konfiguration – soll der Remote-Standard-Branch verwendet werden, oder ein Projekt-weiter Fallback-Branch?

6. **Pull-Request-Verhalten**: Wenn ein PR mit konfigurierten Basis-Branch erstellt wird, der sich vom Remote-Standard-Branch unterscheidet – können Merge-Konflikte oder Integrationsprobleme entstehen, die speziell beobachtet werden müssen?

7. **Fehlerbehandlung in Workflows**: Wenn das Starten einer Aufgabe fehlschlägt, weil der Basis-Branch nicht existiert – soll der Benutzer automatisch zur Konfiguration geleitet werden, oder nur eine Fehlermeldung angezeigt werden?

8. **Multi-Branch-Strategie**: Ist es zukünftig geplant, mehrere Basis-Branches pro Projekt zu unterstützen (z. B. für unterschiedliche Release-Branches), oder bleibt es bei einer 1:1-Zuordnung?

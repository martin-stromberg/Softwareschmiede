# Anforderung – Codesprache finalisieren

Quelle: Issue #5 (martin-stromberg/Softwareschmiede), Kommentar von martin-stromberg.

## Ziel

Die Sprache im Programmcode wird finalisiert (verbindlich festgelegt und durchgängig hergestellt).

## Fachliche Anforderungen

1. **Bezeichner in Englisch**: Variablen, Klassen, Methoden etc. (d. h. sämtliche Bezeichner:
   Typnamen, Member, Parameter, lokale Variablen, Namespaces, Dateinamen, soweit sie
   Bezeichner abbilden) sind in **englisch** zu benennen.
2. **XML-Kommentare in Deutsch**: XML-Dokumentationskommentare (`/// <summary>` usw.)
   sind auf **deutsch** zu verfassen bzw. deutsch zu belassen.
3. **Dokumentationsdateien in Deutsch**: Dokumentationsdateien (Markdown-Dokumente unter
   `docs/`, Hilfeseiten, Projektdokumentation) sind auf **deutsch** verfasst.

## Betroffene Bereiche

- `src/Softwareschmiede` (Domain, Application, Infrastructure, Migrations)
- `src/Softwareschmiede.App` (ViewModels, Views/XAML, Ressourcen)
- `src/Softwareschmiede.Plugin.Contracts` (öffentliche Plugin-API)
- `src/Softwareschmiede.Tests`, `src/Softwareschmiede.IntegrationTests`
- `docs/` (Hilfe, Feature-Dokumente, Release Notes, CI/CD-Doku)
- Root-Dokumente nur soweit sie „Dokumentationsdateien" im Sinne der Anforderung sind
  (README/CONTRIBUTING/CHANGELOG/SECURITY: siehe Offene Punkte)

## Akzeptanzkriterien

- Der komplette Code kompiliert; die stabile Testlane
  (`dotnet test src/Softwareschmiede.Tests --filter "Category!=OsInterface"`) läuft grün.
- Kein deutschsprachiger Bezeichner (Typ, Member, Parameter, lokale Variable) verbleibt
  im produktiven Code und in den Tests. Bestands-Migrations unter `src/Softwareschmiede/Migrations/`
  dürfen aus Upgrade-Sicherheitsgründen unverändert bleiben (Nicht-Ziel).
- XML-Kommentare sind deutsch; englischsprachige XML-Kommentare werden übersetzt.
- Dokumentationsdateien sind deutsch; englischsprachige Dokumentationsdateien werden übersetzt.
- Persistenz- und Kompatibilitätsrisiken (EF-Core-Tabellen-/Spaltennamen, serialisierte
  Enum-/JSON-Werte, gespeicherte Einstellungen, Plugin-Verträge, XAML-Verweise) sind je
  Entscheidung sauber behandelt (Migration, Mapping oder begründeter Ausschluss).

## Explizite Nicht-Ziele

- Keine Übersetzung der Benutzeroberfläche selbst (sichtbare UI-Texte bleiben deutsch;
  es geht um Bezeichner im Code, nicht um Sprachausgabe an den Anwender).
- Keine Umbenennung des Produkts/Repo-Namens „Softwareschmiede" (Eigenname).
- Keine Änderung historischer EF-Migrationsklassen unter `src/Softwareschmiede/Migrations/`
  (Upgrade-Sicherheit: Migrations-IDs und Schemafolge bleiben stabil).
- Keine Umbenennung von Zeichenketten-Literalen mit fachlicher Bedeutung (Log-Meldungen,
  Ausnahme-Texte, Protokolleinträge) — es sei denn, sie sind zugleich persistierte
  Schlüssel/Werte; dann entscheidet die jeweilige Schrittbeschreibung.
- `continue-done.md`, `issue.md`, `changes.log` im Repo-Root sind SCM-/Workflow-Arbeitsdateien,
  keine Produktdokumentation.

## Offene Fragen / zu entscheidende Punkte

1. **Zielbezeichnung für die Kernentität `Aufgabe`**: `Task` kollidiert mit
   `System.Threading.Tasks.Task`. Vorschlag: `WorkItem` für die Entität (eindeutig, branchenüblich);
   zusammengesetzte Namen entsprechend (`AufgabenListe` → `WorkItemList` usw.).
   *Entscheidung: als Annahme dokumentiert, siehe Plan.*
2. **Serialisierte Werte**: Werden Enum-Werte/Property-Namen in SQLite-DB, JSON-Dateien oder
   SCM-Payloads persistiert, ist je Fall zu prüfen: DB-Migration, tolerante Deserialisierung
   oder bewusster Kompatibilitätsverzicht mit Begründung.
3. **Plugin-Verträge** (`Softwareschmiede.Plugin.Contracts`): Umbenennungen sind breaking
   für externe Plugins. Vorschlag: ebenfalls vereinheitlichen (Major-/Feature-Release ohne
   Stabilitätszusage in der Anforderung), sofern die Bestandsaufnahme keine Vertragszusage findet.
4. **Root-Markdowns** (README.md, CONTRIBUTING.md, CHANGELOG.md, SECURITY.md,
   THIRD_PARTY_LICENSES.md): README ggf. übersetzen; LICENSE/THIRD_PARTY_LICENSES bleiben
   rechtlich bedingt englisch (keine Dokumentation im fachlichen Sinne).
5. **GitHub-Workflows/CI-Skripte** (`.github/workflows`, PowerShell-/Python-Skripte):
   Bezeichner dort zählen ebenfalls zu „Programmcode"; Workflow-/Job-Namen nur ändern,
   wenn keine externen Verweise darauf aufbauen.

## Annahmen (belegt aus dem Auftrag)

- Der Kommentar verlangt den projekt-skill für schrittweise Umbenennungen → dieses Projekt.
- PR-Ziel ist `staging` (Upstream-Basisbranch des Task-Branches), nicht `main`.

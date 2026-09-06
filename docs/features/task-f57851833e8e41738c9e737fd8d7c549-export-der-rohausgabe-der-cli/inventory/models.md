## `Protokolleintrag`
Datei: `src/Softwareschmiede/Domain/Entities/Protokolleintrag.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige ID des Protokolleintrags. |
| `AufgabeId` | `Guid` | Referenz auf die zugehörige Aufgabe; zentrale Filtergrundlage pro Aufgabe. |
| `Typ` | `ProtokollTyp` | Typisierung des Eintrags (u. a. `CliOutput`). |
| `Inhalt` | `string` | Inhalt der protokollierten Zeile/Nachricht; bei CLI-Rohdaten die persistierte Ausgabezeile. |
| `AgentName` | `string?` | Optionaler Agentenname (nicht für jeden Typ gesetzt). |
| `Zeitstempel` | `DateTimeOffset` | Zeitliche Einordnung; wird u. a. für chronologische Sortierung genutzt. |
| `Aufgabe` | `Aufgabe` | Navigation zur zugehörigen Aufgabe. |
| `TestErgebnisse` | `List<TestErgebnis>` | Zugeordnete Testergebnisse für Einträge vom Typ `TestErgebnis`. |
| `DiffResult` | `DiffResult?` | Optional verknüpftes Diff-Ergebnis. |

# Enums

## `AufgabeStatus`
Datei: `src/Softwareschmiede/Domain/Enums/AufgabeStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Neu` | Aufgabe wurde erstellt und wartet auf Bearbeitung. |
| `Gestartet` | Aufgabe wurde gestartet; Branch/CLI laufen oder sollten laufen. |
| `Wartend` | Aufgabe wartet nach Rate-Limit auf Wiederaufnahme. |
| `Beendet` | Aufgabe wurde beendet. |
| `Archiviert` | Aufgabe ist archiviert. |

## `AufgabeLaufStatus`
Datei: `src/Softwareschmiede/Domain/Enums/AufgabeLaufStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Laeuft` | CLI hat kuerzlich Eingabe oder Ausgabe verarbeitet. |
| `WartetAufEingabe` | CLI laeuft, erzeugt aber seit laengerer Zeit keine Ausgabe und wartet vermutlich auf Eingabe. |

## `ProtokollTyp`
Datei: `src/Softwareschmiede/Domain/Enums/ProtokollTyp.cs`

| Wert | Bedeutung |
|------|-----------|
| `Prompt` | Prompt, der an den KI-Agenten gesendet wurde. |
| `KiAntwort` | Antwort des KI-Agenten. |
| `StatusUebergang` | Statusuebergang der Aufgabe. |
| `TestErgebnis` | Ergebnis eines Testlaufs. |
| `GitAktion` | Git-Aktion. |
| `CliOutput` | Ausgabezeile eines eingebetteten CLI-Prozesses. |
| `RateLimit` | Erkannter Rate-Limit-Marker aus CLI-Ausgabe. |
| `SystemMeldung` | Interne Systemmeldung. |

## `PluginType`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/PluginType.cs`

| Wert | Bedeutung |
|------|-----------|
| `SourceCodeManagement` | SCM-Plugin. |
| `DevelopmentAutomation` | KI-/Automatisierungs-Plugin. |

## `PluginSettingFieldType`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PluginSettingFieldType.cs`

| Wert | Bedeutung |
|------|-----------|
| `Text` | Einzeiliger Text. |
| `Secret` | Passwort- oder Tokenfeld. |
| `Url` | URL-Eingabe. |
| `Integer` | Ganzzahl. |
| `Boolean` | Checkbox. |
| `Enum` | Auswahl ueber feste Werte. |
| `FilePath` | Dateipfad mit Auswahldialog. |
| `CommandLineParameters` | Kommandozeilenparameter fuer CLI-Aufrufe. |

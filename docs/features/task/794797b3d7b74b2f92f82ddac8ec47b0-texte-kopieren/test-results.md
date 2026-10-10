# Testergebnisse

Ausgeführt am: 2026-10-06
Branch: `task/794797b3d7b74b2f92f82ddac8ec47b0-texte-kopieren`

## Ergebnis

**Status: Nicht bestanden.** Der Build und die gezielten Buffer-, Control- und Tastaturtests waren erfolgreich. Die isolierte Live-Abnahme E‑01 ist erfolgreich. E‑02 ist zwar registriert und enthält die geforderten Auswahl-, Pixel- und Clipboard-Prüfungen, aber sein integrierter E2E-Lauf wurde nach 150 Sekunden ohne Fortschritt manuell abgebrochen und lieferte daher kein Testergebnis.

## Build und gezielte Tests

Befehl:

```text
dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --no-restore --filter "FullyQualifiedName~TerminalControlTests|FullyQualifiedName~TerminalBufferTests|FullyQualifiedName~KeyToVt100EncoderTests" --logger "console;verbosity=minimal"
```

Ergebnis: **bestanden**, Build erfolgreich, 94 bestanden, 0 fehlgeschlagen, 0 übersprungen. Enthalten sind unter anderem `Ctrl+C`-Kodierungs-/Weiterleitungsregressionen, `Ctrl+V`, Auswahl-Lifecycle-/Scrollback-Tests und Buffer-Snapshot-/Scrollverhalten.

## Verbindliche E2E-Abnahmen

| Szenario | Existiert | Registriert | Pflicht-Assertions im Testcode | Laufstatus | Abnahme |
|---|---|---|---|---|---|
| E‑01 `TerminalText_LiveMarkCopyAndKeepSelection` | Ja, als Methode in `E2E_ConPtyLifecycle.cs` | Ja, direkter isolierter Fact in `MainTest.cs` | **Ja.** Echter Mausdrag über UIA-Viewportgeometrie, Vorher-/Nachher-Pixelvergleich, Strg+Umschalt+C mit STA-Clipboardvergleich sowie erneuter Kopiernachweis nach weiterer Ausgabe und Scrollback-Verschiebung. | `TerminalText_LiveMarkCopyAndKeepSelection_E2E`: 1/1 bestanden, 22 s. | **Bestanden** |
| E‑02 `ReplayText_MarkAndCopy` | Ja, in `E2E_KonsolenTestfenster.cs` | Ja, direkter Aufruf in `MainTest.RunGeneralTests` | **Ja, bis auf einen expliziten Negativnachweis zur Live-Prozess-Eingabe.** Enthalten sind eine deterministische Fixture mit echten CR/LF, Replay laden und vollständig abspielen, Mausauswahl mit Vorher-/Nachher-Pixelvergleich, exakter mehrzeiliger STA-Clipboardvergleich sowie Tastaturauswahl mit Pixelvergleich und exaktem Clipboardvergleich. Ein expliziter Assert, dass dabei keine Live-Prozess-Eingabe ausgelöst wird, ist nicht vorhanden. | `RunGeneralTests` wurde ausgeführt, blieb 150 Sekunden ohne Ausgabe/Fortschritt und wurde mit Ctrl+C abgebrochen; kein xUnit-Ergebnis. | **Nicht bestanden** |

E‑01 wird aufgrund seines erfolgreichen isolierten Benutzerflusses als bestanden gewertet. E‑02 bleibt offen; die bloße Registrierung ersetzt keinen erfolgreichen Lauf.

## E2E-Laufdetails

Befehl für E‑02 (das Szenario ist Teil des integrierten General-Runners):

```text
dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~End2EndTest.RunGeneralTests" --logger "console;verbosity=minimal"
```

Der Testprozess lieferte nach insgesamt 150 Sekunden keine weiteren Ausgaben. Der Testlauf wurde manuell unterbrochen; die installierte `Softwareschmiede`-Anwendung wurde nicht beendet. E‑01 gehört zum separaten `RunConPtyTests`-Runner und wurde in diesem Lauf nicht gestartet.

## Fehlgeschlagene bzw. offene Abnahmen

- E‑02 `ReplayText_MarkAndCopy` — integrierter `RunGeneralTests`-Lauf ohne Ergebnis nach 150 Sekunden abgebrochen; außerdem fehlt im Testcode der ausdrückliche Negativ-Assert zur Live-Prozess-Eingabe.
- Verbindliche UI-Abnahme insgesamt — nicht erfolgreich, da E‑02 gemäß Plan noch erfolgreich ausgeführt werden muss.

## Umgebungshinweise

- Ein `artifacts`-Verzeichnis wurde im Repository bei der Bestandsprüfung nicht gefunden; es gab daher nichts zu entfernen.
- Die installierte Anwendung lief während der Tests unter `C:\Users\Martin\Desktop\Softwareschmiede\Softwareschmiede.exe` (PID 23516). Sie wurde nicht beendet. Der Build in den Repository-`bin\Debug`-Ordnern war erfolgreich.

# Usability-Review

## Ergebnis

**Status:** Keine Befunde

Hinweis (Runde 4 — Nacharbeitslauf aus `continue.md`, ohne Unteragenten selbst durchgeführt): Der Diff
gegen `a361c95` enthält keine UI-Änderungen. Die Korrekturen betreffen ausschließlich die interne
Status-/Fehlerbehandlung von Terminal-Sessions (`KiAusfuehrungsService`, `ITerminalSession.Failure`,
`PseudoConsoleSession`) und deren Tests.

Die einzige benutzerseitig wahrnehmbare Konsequenz ist eine *Korrektur*: Stirbt eine CLI vor der
Event-Verdrahtung mit einem Fehlercode, oder bricht die Terminal-Leseschleife mit einem Laufzeitfehler
zusammen, meldet die Oberfläche jetzt korrekt einen Fehlerzustand statt „Gestoppt" (bzw. hängen zu
bleiben) — und das Aufgabenprotokoll erhält einen verständlichen Eintrag („CLI-Prozess mit Fehler beendet
(ExitCode: …)" bzw. „Terminal-Session mit einem Laufzeitfehler beendet. Aufgabe bleibt im Status Gestartet —
CLI-Start kann erneut versucht werden."). Beide Texte sind für Endanwender ohne technisches Vorwissen
verständlich und enthalten keine internen Kennungen.

Die Befundfreiheit der Runde 3 (alle geprüften Interaktionen, Pipe-Fallback-Hinweis,
RequiresPty-Fehlermeldung, AutomationId-Korrektur) bleibt unverändert gültig.

## Geprüfte Interaktionen

- Anzeige des CLI-Status bei einem sehr frühen Prozessabbruch mit Fehlercode → korrekt „Fehler" statt „Gestoppt"
- Anzeige des CLI-Status bei einem fatalen Terminal-Session-Fehler (Leseschleife tot, Prozess läuft) →
  korrekt „Fehler" und aufgeräumter Zustand statt dauerhaft „Gestartet" ohne weitere Ausgabe
- Protokoll-Einträge bei Session-Fehlern → verständliche deutsche Klartextmeldungen, keine internen IDs
- Alle übrigen Interaktionen der Runde-3-Prüfung → unverändert (kein Diff in UI-Dateien)

## Geprüfte Dateien

Keine UI-Dateien im Diff dieser Runde geändert. Fachlich betroffen (nicht-UI):
- `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`
- `src/Softwareschmiede/Infrastructure/Terminal/ITerminalSession.cs`
- `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs`

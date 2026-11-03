# Übersetzte Anforderung

Quelle: Issue #321 „Aufgabe-Abschluss: Einheitlicher Pass-Einstieg für alle Aufrufer
(Aufgabensperre, CLI-Stopp, Prompt-Abbruch)" — Befund aus dem Code-Review in #316.
Auftrag per Issue-Kommentar: „Erledige diese Korrekturen".

## Fachliche Zusammenfassung

Der Geschäftsvorgang „Aufgabe abschließen" soll einen einzigen produktiven
Einstiegspunkt erhalten, der unter der `ScmTaskCoordinator`-Aufgabensperre die
vollständige Sequenz fährt: `KiAusfuehrungsService.StopCliAsync` →
`PromptZeitVersandService.CancelScheduledPrompt` → Offene-To-do-Prüfung →
Klon-Verzeichnis löschen → Status `Beendet` + Lauf-Felder räumen + Protokolleintrag.
Alle produktiven Aufrufer (UI `TaskDetailViewModel`, automatische Abschlussüberwachung
`ScmTaskCompletionMonitoringService`) nutzen denselben Einstieg; das ViewModel behält
nur Dialog-/Anzeigelogik.

## Betroffene Klassen und Komponenten

- `EntwicklungsprozessService` — `AbschliessenAsync` wird zum koordinierten
  Pass-Einstieg (Muster `RunAsync` + `…CoordinatedAsync`, analog
  `ProzessStartenAsync`/`CliNeustartenAsync`).
- `EntwicklungsprozessServiceOptions` — neuer optionaler Parameter
  `PromptZeitVersandService` (Singleton, analog `KiAusfuehrungsService`).
- `App.xaml.cs` — DI-Registrierung übergibt `PromptZeitVersandService` in die Options.
- `TaskDetailViewModel.AufgabeAbschliessenAsync` — entfernt den eigenen
  `CancelScheduledPrompt`-Aufruf; behält To-do-Vorprüfung für die Anzeige.
- `ScmTaskCompletionMonitoringService.CheckTaskAsync` — ruft nur noch den
  Pass-Einstieg; die Revalidierung bleibt unter der Aufgabensperre.
- `EntwicklungsprozessService.ProzessStartenAsync` — kein produktiver Aufrufer;
  Sichtbarkeit auf `internal` einschränken (Tests bleiben über
  `InternalsVisibleTo` erreichbar; `Softwareschmiede.IntegrationTests`
  wird ergänzt).
- `ScmE2ERuntimeControl` — geprüft: führt CLI-Sitzungs-Ersatz
  (Sperre → `StopCliAsync` → `StartTerminalSessionAsync`) durch, keinen
  Aufgaben-Abschluss; bleibt unverändert (Abweichung vom Issue-Wortlaut,
  im Plan begründet).
- Tests: neue/angepasste Tests für Prompt-Abbruch und CLI-Stopp im Pass.

## Implementierungsansatz

- Bestehendes Koordinationsmuster wiederverwenden:
  `public Task AbschliessenAsync(...) => ScmTaskCoordinator.Shared.RunAsync(aufgabeId, () => AbschliessenCoordinatedAsync(...), ct)`.
  Die Sperre ist über `AsyncLocal`-Besitzer reentrant — verschachtelte Aufrufe
  (z. B. Monitoring hält die Sperre bereits) laufen ohne Deadlock.
- Kein neues Datenmodell, keine Migration, keine Konfigurationseinträge.

## Konfiguration

Keine.

## Offene Fragen

Keine — die Abweichung zu `ScmE2ERuntimeControl` wird als Designentscheidung
im Plan begründet (die Klasse vollzieht keinen Aufgaben-Abschluss).

# Bestandsaufnahme – Schritt 2: Re-Checks bei geschlossenem Issue

Verdichtete Projekt-Bestandsaufnahme: `docs/projects/task/…/inventory.md`.
Verifizierte schrittrelevante Fakten:

- `ScmEventOrchestrator.DispatchPreparationAsync` (`:615–812`):
  - Vor-Dispatch-Check bereits vorhanden: `:662–680` — konto-konsistente
    Neuauflösung + `ReadIssueAsync`; `null` → `WaitAsync("…konnte nicht gelesen
    werden. Erneut prüfen.")`; `IsClosed` → `Verarbeitet` + Protokolleintrag
    „bereits geschlossen; ohne Änderung abgeschlossen".
  - Zwischen Check und Zustellung liegen: Plugin-/Limit-Auflösung, ggf. CLI-Start
    (`StartProcessAndCliAsync`/`RestartCliAsync` `:721–733`), `WaitForInputReadyAsync`,
    `CheckExchangeAccessAsync` — beliebig langwierig. `exchange.PrepareAsync` bei `:762`.
  - `issue`-Variable (`Issue?`) kann für den Zweit-Check wiederverwendet werden;
    `issue.IssueUrl` fließt in die Request (`:764`).
- `ReconcilePreparationAttemptAsync` (`:832–979`): erhält aktuell nur
  `IScmEventResponder` — Signatur muss um `IScmEventSource` erweitert werden
  (Aufrufer `:632` hat `source`). Aktionen laufen im Block
  `if (messages.Accepted is not null)` (`:853–958`); Schleife ab `:860`.
  Leeres `messages.Actions` → nichts remote → kein Read nötig.
- Ergebnis-Protokoll je Aktion über `exchange.WriteResultAsync` — Muster bei
  deaktiviertem Kommentar-Schalter (`:877`): `success=false` + Begründung.
- `ScmIncomingEventStatus.Verarbeitet` ist Endstatus und gibt die Aufgabe für
  die Abschlusslogik frei (nicht-blockierende Stati: Verarbeitet, Abgebrochen,
  VerarbeitungUngeklärt, ZustellungUngewiss).
- Tests: `ScmEventDispatchTests_IssuePreparation.cs` — `fixture.ConfigureIssue()`
  mockt `ReadIssueAsync` offen; das `Issue`-Record trägt `IsClosed`;
  `fixture.DispatchAsync(eventId)`, `fixture.Responses` (Remote-Aktionen),
  `fixture.Sessions`/`fixture.Prompts`. Für „offen→geschlossen" sequentielle
  Returns via Zähler-/Flag-Lambda.
- Doku: `business-rules.md` Abschnitt „Issue erstellt"-Erkennung,
  Aufzählungspunkt **Ausführung** (Z. 45).

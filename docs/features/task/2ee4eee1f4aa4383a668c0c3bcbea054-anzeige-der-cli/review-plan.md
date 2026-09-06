# Plan-Review: Anzeige der CLI

## Annahmen

- `GetDetailAsync` verwendet `AsNoTracking` – im Code bestätigt.
- `_aufgabe` wird von `ResolvePluginViaDialogAsync` verwendet und kann daher direkt synchronisiert werden.

## Risiken

- Ähnliche Fehler können an anderen Stellen auftreten, wo `AufgabeService.UpdateAsync` aufgerufen wird, ohne das in-memory-Objekt zu aktualisieren. Für diese Aufgabe ist die angepasste Stelle aber der einzige relevante Weg für einen Plugin-Wechsel.
- `StartCliAndUpdateStateAsync` ruft `EntwicklungsprozessService.CliNeustartenAsync` auf, das den Prefix via `ResolveDevelopmentAutomationPluginAsync` auflöst. Da `_aufgabe.KiPluginPrefix` nun korrekt gesetzt ist, wird das richtige Plugin gestartet.

## Urteil

- Keine kritischen Probleme.
- Plan ist für die gemeldete Regression ausreichend und umsetzbar.
- E2E-Validierung muss außerhalb des Sandbox-Environments nachgeholt werden.

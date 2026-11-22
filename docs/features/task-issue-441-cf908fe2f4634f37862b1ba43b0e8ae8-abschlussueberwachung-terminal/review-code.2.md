# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### ScmIncomingEventStatus.cs (ScmIncomingEventStatus)

- **Hardcodierte Werte / verbliebene Doppelwahrheit** — Die Klasse deklariert ihre Konstanten als
  „verbindliche Statuswerte" von `ScmIncomingEvent.Status`, und `ScmEventOrchestrator.cs`,
  `ScmTaskCompletionMonitoringService.cs` sowie alle geänderten Testdateien wurden sauber
  umgestellt. Der produktive Restbestand nutzt die Werte aber weiterhin als String-Literale —
  einschließlich des einzigen verbleibenden produktiven Schreibers außerhalb des Orchestrators:
  `ScmEventStore.cs` Z. 101 (`item.Status = "Verzögert"`, Verzögerungsdirektive),
  `ScmEventExchangeService.cs` Z. 27 (`x.Status != "Verarbeitet" && x.Status != "Abgebrochen"`),
  `ScmDispatchPriority.cs` Z. 43 (`item.Status == "Verzögert"`, Prioritätsentscheidung) und
  `RunningAutomationsCount.cs` Z. 60 (`x.Status == "Verzögert"`). Damit steht der „verbindlich"-
  Anspruch der neuen Klasse der Realität entgegen: Ein Tippfehler oder eine Umbenennung im
  Schreibpfad (`ScmEventStore`) bliebe kompilierbar und liefe den Konstanten-Lesern still davon —
  exakt das Risiko, das der Runde-1-Befund beseitigen sollte.

  Empfehlung: Die vier genannten Produktivstellen ebenfalls auf `ScmIncomingEventStatus.*`
  umstellen (mechanisch, gleiche Werte). Alternativ — falls die Ausweitung bewusst aus dem
  Scope gehalten werden soll — die „verbindlich"-Formulierung in der Klassendoku entschärfen,
  damit die Dokumentation keine falsche Sicherheit vortäuscht.

### PullRequestReferenceService.cs (PullRequestKey)

- **Dokumentation ungenau** — Die XML-Doku des privaten Records `PullRequestKey` (Z. 316–318)
  beschreibt den Schlüssel als „abgeleiteter Provider + normalisierte Repository-Id + Nummer",
  der Record enthält aber nur `Provider` und `RepositoryId`; die Pull-Request-Nummer bleibt ein
  separater Parameter der privaten `FindByKeyAsync`-Überladung. Die Doku suggeriert damit einen
  dreiteiligen Schlüssel, den der Typ nicht abbildet.

  Empfehlung: Kommentar auf den tatsächlichen Inhalt reduzieren (z. B. „abgeleiteter Provider +
  normalisierte Repository-Id; die Nummer bleibt separater Parameter") oder die Nummer in den
  Record aufnehmen.

## Geprüfte Dateien

- `src/Softwareschmiede/Domain/Entities/ScmIncomingEventStatus.cs` (neu)
- `src/Softwareschmiede/Domain/Entities/ScmIncomingEvent.cs`
- `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs`
- `src/Softwareschmiede/Application/Services/ScmTaskCompletionMonitoringService.cs`
- `src/Softwareschmiede/Application/Services/PullRequestReferenceService.cs`
- `src/Softwareschmiede.Tests/Domain/Entities/ScmIncomingEventStatusTests.cs` (neu)
- `src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs`
- `src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests_Ordering.cs` (neu)
- `src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests_Reconcile.cs` (neu)
- `src/Softwareschmiede.IntegrationTests/Services/ScmTaskCompletionMonitoringServiceTests.cs`

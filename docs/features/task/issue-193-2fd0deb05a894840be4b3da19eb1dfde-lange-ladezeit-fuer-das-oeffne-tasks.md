# Tasks: Asynchrones Laden von Aufgabenprotokollen (Issue 193)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik (Services) | `AufgabeService.GetDetailAsync()` — `.Include(a => a.Protokolleintraege).ThenInclude(p => p.TestErgebnisse)` entfernen (Zeilen 56–57) | Offen | — |
| 2 | ViewModel | `TaskDetailViewModel.LadeProtokolleAsynch(CancellationToken ct)` — neue private async Task implementieren mit Try-Catch und Logger | Offen | Neue Unit-Tests |
| 3 | ViewModel | `TaskDetailViewModel.LadenAsync(CancellationToken ct)` — Fire-and-Forget `_ = LadeProtokolleAsynch(ct)` einführen; sequenzielle `GetByAufgabeAsync`-Blockierung entfernen | Offen | Unit-Tests + E2E-Tests |
| 4 | Tests - Unit | `AufgabeServiceTests` — Alle 6 Tests überprüfen, die `GetDetailAsync()` nutzen; Assertions auf `.Protokolleintraege` entfernen oder Tests umschreiben | Offen | Test-Lauf |
| 5 | Tests - Unit | Neue Test: `TaskDetailViewModelTests.LadeProtokolleAsynch_ShouldLoadProtocols_WhenSuccessful()` — Protokolle werden async geladen | Offen | Test-Lauf |
| 6 | Tests - Unit | Neue Test: `TaskDetailViewModelTests.LadeProtokolleAsynch_ShouldLogError_WhenProtokollServiceFails()` — Fehler werden geloggt, Exception nicht propagiert | Offen | Test-Lauf |
| 7 | Tests - Unit | Neue Test: `TaskDetailViewModelTests.LadeProtokolleAsynch_ShouldIgnoreCancellation_WhenCancelled()` — OperationCanceledException wird ignoriert | Offen | Test-Lauf |
| 8 | Tests - Unit | Neue Test: `TaskDetailViewModelTests.LadenAsync_ShouldNotWaitForProtocols()` — Aufgabe wird schnell gesetzt, ohne auf Protokoll zu warten | Offen | Test-Lauf |
| 9 | Tests - Unit | `TaskDetailViewModelTests` — Bestehende Tests überprüfen (AufgabeId_Setter_*, ShowEditPanel_*, ShowCliPanel_*, ShowDiffPanel_*); Waits auf Protokoll anpassen oder Assertions auf Aufgabe-only umstellen | Offen | Test-Lauf |
| 10 | Tests - E2E | Neue Test: `E2E_TaskDetail_OpensQuicklyWithBasicInfo()` — TaskDetailView öffnet, Aufgabenbasisinformation sichtbar, nicht blockiert von Protokoll | Offen | E2E-Test-Lauf |
| 11 | Tests - E2E | Neue Test: `E2E_TaskDetail_LoadsProtocolAsynchronously()` — Nach View-Öffnung werden Protokolleinträge asynchron nachgeladen und angezeigt | Offen | E2E-Test-Lauf |
| 12 | Tests - E2E | Neue Test: `E2E_TaskDetail_ProtocolLoadErrorDoesNotAffectBasicInfo()` — Fehler beim Protokoll-Laden beeinflussen nicht Aufgabenbasisinfo | Offen | E2E-Test-Lauf |
| 13 | Tests - E2E | Bestehende E2E-Tests für TaskDetailView überprüfen; Waits auf Protokoll-Elemente hinzufügen, falls nötig | Offen | Test-Lauf |
| 14 | Verifikation | Code-Suche: Alle Aufrufer von `AufgabeService.GetDetailAsync()` identifizieren; prüfen ob sie auf `Protokolleintraege` vertrauen; ggf. anpassen | Offen | Code-Review |

# Offene Aufgaben

Erstellt am: 2026-09-06
Abbruchgrund: Kein Fortschritt zwischen den letzten zwei Iterationen

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

_Keine — der Plan wurde vollständig umgesetzt (`review.md` Status: Vollständig umgesetzt)._

## Code-Review-Befunde

> Hinweis: Alle Befunde betreffen pre-existierenden Code, der vor der Feature-Implementierung bereits vorhanden war. Die Feature-Implementierung selbst (der `## Verknüpftes Issue`-Block in `CreateIssueFileAsync` und die drei neuen Testmethoden) hat keine Befunde.

- [ ] **Hardcoded Value** — `PrepareCloneDirectoryAsync`, Zeile 463: `"softwareschmiede"` als String-Literal hartcodiert. Empfehlung: Als `private const string KlonBasisVerzeichnis = "softwareschmiede";` extrahieren und in Service und Tests verwenden.
- [ ] **God Method** — `SetupBranchAsync` (84 Zeilen, Z. 485–568): Behandelt drei konzeptuell getrennte Branch-Fälle. Empfehlung: In private Hilfsmethoden `CheckoutReviewSourceAsync`, `CheckoutExistingBranchAsync`, `CreateNewTaskBranchAsync` aufteilen.
- [ ] **Doppelte Repository-Auflösung** — `ProzessStartenUndCliStartenAsync` ruft `ResolveRepositoryAsync`/`ResolvePluginAsync` ein zweites Mal auf. Empfehlung: Internen `ProzessStartenCoreAsync`-Overload einführen.
- [ ] **Lazy Class** — `StartskriptErgebnis` (`sealed record` mit einzelner `string`-Eigenschaft). Empfehlung: Rückgabetyp von `RepositoryStartskriptAusfuehrenAsync` auf `string` ändern und das Record entfernen.
- [ ] **Doppelter Code — SUT-Verdrahtung** (8 Testmethoden): `new EntwicklungsprozessService(...)` wird nahezu identisch 8-mal verdrahtet. Empfehlung: Private Hilfsmethode `CreateSut(EntwicklungsprozessServiceOptions, ILogger?)` einführen.
- [ ] **Überflüssige Bereinigung** (Zeilen ~1051–1052 und ~1101–1102): In `UpdateGitignoreAsync_ShouldLogWarning_WhenFileOperationFails` und `ProzessStartenAsync_ShouldContinue_WhenGitignoreUpdateFails` wird das Unterverzeichnis vor dem rekursiven `uniqueBase`-Löschen redundant gelöscht. Empfehlung: Den ersten `DeleteDirectoryIfExists`-Aufruf entfernen.

## Fehlgeschlagene Tests

> Hinweis: Alle 8 Fehler sind pre-existierende Infrastruktur-Ausfälle in WPF-E2E-Tests, die eine laufende WPF-Applikation mit UI-Automatisierung voraussetzen. Sie sind **nicht durch die Feature-Implementierung verursacht**. Die 46 Unit-Tests in `EntwicklungsprozessServiceTests` (inkl. aller 3 neuen Tests) laufen erfolgreich durch.

- [ ] WpfE2ETests.WpfBasisSzenarien — `COMException 0x80040201` (Ein Ereignis konnte keinen Abonnenten aufrufen) — Pre-existing E2E infrastructure failure
- [ ] ProjectDetailE2ETests.ProjektDetailSzenarien — `TimeoutException: Element nicht innerhalb 15s gefunden` — Pre-existing E2E infrastructure failure
- [ ] E2E_RepositoryManagementTests.BasisBranchVerwaltung — `TimeoutException: Element nicht innerhalb 15s gefunden` — Pre-existing E2E infrastructure failure
- [ ] E2E_RepositoryInitialisierungConfigTests.InitialisierungsskriptKonfiguration — `TimeoutException: Element nicht innerhalb 20s gefunden` — Pre-existing E2E infrastructure failure
- [ ] E2E_RepositoryInitialisierungAusfuehrungTests.InitialisierungsskriptAusfuehrung — `TimeoutException: Element nicht innerhalb 20s gefunden` — Pre-existing E2E infrastructure failure
- [ ] E2E_AutonomAufgabenFeatureFlagDisabled.AutonomAufgabeInitialisieren_ZeigtFehlermeldungStattDialog_WennFeatureFlagDeaktiviert — `TimeoutException` — Pre-existing E2E infrastructure failure
- [ ] End2EndTest.RunGeneralTests — `TimeoutException: Element nicht innerhalb 20s gefunden` — Pre-existing E2E infrastructure failure
- [ ] End2EndTest.RunConPtyTests — `TimeoutException: Element nicht innerhalb 20s gefunden` — Pre-existing E2E infrastructure failure

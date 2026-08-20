# Tasks: Korrektur des Arbeitsablaufs (CLI-Panel-Sichtbarkeit)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | Bedingung in `AufgabeAusfuehrungsStatusExtensions.SollCliAnzeigen` von `== AufgabeAusfuehrungsStatus.Aktiv` zu `is (AufgabeAusfuehrungsStatus.Aktiv or AufgabeAusfuehrungsStatus.Beendet)` ändern | Offen | — |
| 2 | Datenmodell | XML-Dokumentation von `AufgabeAusfuehrungsStatusExtensions.SollCliAnzeigen` aktualisieren, um neue Logik zu erklären | Offen | — |
| 3 | Tests | Neue Unit-Testmethode `SollCliAnzeigen_WhenAusfuehrungsStatusIsBeendet_AndAufgabeStatusIsGestartet_ReturnsTrue` hinzufügen | Offen | — |
| 4 | Tests | Neue Unit-Testmethode `SollCliAnzeigen_WhenAusfuehrungsStatusIsBeendet_AndAufgabeStatusIsWartend_ReturnsTrue` hinzufügen | Offen | — |
| 5 | Tests | Neue Unit-Testmethode `SollCliAnzeigen_WhenAusfuehrungsStatusIsBeendet_AndAufgabeStatusIsBeendet_ReturnsFalse` hinzufügen | Offen | — |
| 6 | Tests | Bestehende Unit-Tests in `TaskDetailViewModelTests` überprüfen und ggf. anpassen, falls Tests mit `AusfuehrungsStatus == Beendet` und erwarteter `ShowCliPanel == false` vorhanden sind | Offen | — |
| 7 | Tests | E2E-Test `CliPanelVisibility_AfterExecution_RemainsVisible` hinzufügen, um zu überprüfen, dass CLI-Panel nach Beendigung sichtbar bleibt | Offen | — |
| 8 | Tests | E2E-Test `CliPanelVisibility_DuringPluginSwitch_RemainsVisible` hinzufügen, um zu überprüfen, dass CLI-Panel während Plugin-Wechsel nicht verschwindet | Offen | — |
| 9 | Verifikation | Vollständiger `dotnet build` und `dotnet test` mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` ausführen | Offen | — |

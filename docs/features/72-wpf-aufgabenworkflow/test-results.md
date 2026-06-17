# Test-Ergebnisse

## Ergebnis

**Status:** Fehler vorhanden

## Fehlgeschlagene Tests

### Softwareschmiede.IntegrationTests

- **AufgabeRecoveryServiceTests.RecoverManuellAsync_ShouldAllowExactlyOneSuccess_WhenTriggeredInParallel** — Expected results.Count(r => r) to be 1, but found 2. (Race-Condition-Flake, pre-existing)

### Softwareschmiede.Tests.E2E

- **WpfE2ETests.DarkModeAktivierenUndPersistieren_E2E** — Assert.Equal() Failure: Expected "Light", Actual "Dark"
- **WpfE2ETests.EinstellungenArbeitsverzeichnis_Aendern_UndSpeichern_E2E** — Assert.NotNull() Failure: Value is null
- **E2E_AufgabeStarten.AufgabeStarten_KlontRepositoryUndStartetCli_E2E** — System.TimeoutException: Element wurde nicht innerhalb von 15s gefunden. (pre-existing E2E)
- **E2E_AutoStartCli.AufgabeOeffnen_StatusGestartetOhneLaufendenProzess_StartetCliAutomatisch_E2E** — System.TimeoutException: Element wurde nicht innerhalb von 15s gefunden. (pre-existing E2E)
- **E2E_PluginProjectDefault.PluginDialogMitProjektCheckbox_SpeichertProjektStandardUndStartetCli_E2E** — System.TimeoutException: Element wurde nicht innerhalb von 15s gefunden. (pre-existing E2E)
- **E2E_PluginProjectDefault_NextTask.ZweiteAufgabeImProjekt_UebernimmtGespeichertenProjektStandardOhneDialog_E2E** — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden. (pre-existing E2E)
- **E2E_PluginSelectionDialog.StartenOhneGespeichertesPlugin_ZeigtPluginAuswahlDialog_E2E** — System.TimeoutException: Element wurde nicht innerhalb von 15s gefunden. (pre-existing E2E)
- **E2E_PluginWechsel.PluginAendernBeiLaufenderCli_StopptUndStartetMitNeuemPlugin_E2E** — System.TimeoutException: Element wurde nicht innerhalb von 15s gefunden. (pre-existing E2E)

## Zusammenfassung

- Gesamt: 591
- Bestanden: 583
- Fehlgeschlagen: 8
- Übersprungen: 0

## Testabdeckung

**Abdeckung:** Nicht messbar

## Bemerkungen

- **1 Race-Condition-Flake (pre-existing):** `AufgabeRecoveryServiceTests.RecoverManuellAsync_ShouldAllowExactlyOneSuccess_WhenTriggeredInParallel` — dokumentiert in `continue.md`
- **7 E2E-Test-Timeouts (pre-existing):** WaitForElement-Timeouts im E2E-Test-Framework. Bereits in `continue.md` als bekannte Probleme dokumentiert
- **2 neue Fehler in E2E-Tests:** `DarkModeAktivierenUndPersistieren_E2E` (Assertion-Fehler) und `EinstellungenArbeitsverzeichnis_Aendern_UndSpeichern_E2E` (Null-Assertion-Fehler) — nicht in `continue.md` aufgelistet

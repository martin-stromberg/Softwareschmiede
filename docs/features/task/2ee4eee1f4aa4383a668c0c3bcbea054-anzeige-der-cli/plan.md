# Plan: Anzeige der CLI

## Annahmen

- `TaskDetailViewModel` lädt die Aufgabe über `AufgabeService.GetDetailAsync`, das `AsNoTracking()` verwendet.
- `AufgabeService.UpdateAsync` schreibt den neuen `KiPluginPrefix` in die Datenbank, aktualisiert aber nicht das bereits im ViewModel gehaltene `_aufgabe`-Objekt.
- `AktiverCliName` und spätere Neustarts greifen auf `_aufgabe.KiPluginPrefix` zurück, weshalb eine fehlende Synchronisierung den alten Projekt-Default verwendet.

## Änderungen

1. In `TaskDetailViewModel.ResolvePluginViaDialogAsync` nach erfolgreichem `UpdateAsync` auch `aufgabe.KiPluginPrefix` mit dem gewählten Prefix aktualisieren.
2. Unit-Regressionstest hinzufügen:
   - `PluginAendernCommand` zeigt neuen Namen, wenn zuvor Projekt-Default lief.
   - `CliNeustartenCommand` nach Plugin-Wechsel verwendet geändertes Plugin.
3. E2E-Test `E2E_PluginAuswahlUndWechsel` ergänzen um Assertions auf `AktiverCliName` in Fußzeile.
4. E2E-View-Helper `TaskDetailView.GetActiveCliName()` hinzufügen.

## Tests

### Unit-Tests

- `PluginAendernCommand_ZeigtNeuenAktivenCliName_WennVorherProjektDefaultLief`
- `CliNeustartenCommand_NachPluginWechsel_VerwendetGeaendertesPlugin_NichtProjektDefault`

### E2E-Tests

- `PluginAendernBeiLaufenderCli_StopptUndStartetMitNeuemPlugin_E2E` prüft nach dem Wechsel `AktiverCliName == "Claude CLI"`.
- `PluginAuswahl_AbbrechenBleibtNeu_UndOkStartetCli_E2E` prüft nach dem Start `AktiverCliName == "KI Simulator"`.

## Risiken

- `AsNoTracking` kann an weiteren Stellen vergleichbare Probleme verursachen; nur `ResolvePluginViaDialogAsync` anzupassen, da dort das Aufgaben-Objekt direkt verfügbar ist.
- E2E-Tests können in diesem Sandbox-Environment nicht ausgeführt werden; sie müssen im Desktop-Environment validiert werden.

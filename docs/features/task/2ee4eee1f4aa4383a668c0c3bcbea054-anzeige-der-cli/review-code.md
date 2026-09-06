# Code Review: Anzeige der CLI

## Geprüfte Dateien

- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_PluginAuswahlUndWechsel.cs`
- `src/Softwareschmiede.Tests/E2E/Views/TaskDetailView.cs`

## Befund

- `ResolvePluginViaDialogAsync` synchronisiert nach dem Datenbank-Update nun auch das in-memory `Aufgabe`-Objekt (`aufgabe.KiPluginPrefix = ...`).
- Dies behebt den Fall, dass anschließende Neustarts oder `AktualisiereAktivenCliNameAusAufgabeAsync` auf den alten Projekt-Default zugreifen.
- Keine zusätzlichen Kommentare eingefügt, keine bestehenden Kommentare entfernt.
- Formatierung mit `dotnet format Softwareschmiede.slnx` geprüft.

## Offene Punkte

- E2E-Assertions wurden ergänzt, konnten aber im Sandbox-Environment nicht ausgeführt werden.

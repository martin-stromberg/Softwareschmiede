# Bestandsaufnahme: Export der Rohausgabe der CLI

Analysiert wurde der bestehende CLI-/Protokollpfad rund um `TaskDetailViewModel`, `KiAusfuehrungsService` und `ProtokollService` bezogen auf die Anforderung, CLI-Rohdaten als `*.raw` zu exportieren. Fokus war auf bereits vorhandenen Datenquellen, UI-Aktionen, Dialog-Abstraktionen und Testabdeckung.

## Zusammenfassung

- CLI-Rohdaten werden bereits als `Protokolleintrag` mit `ProtokollTyp.CliOutput` persistiert (`ProtokollService.AddCliOutputAsync`), inklusive Reihenfolge über `Zeitstempel`.
- Die CLI-Ausgabe wird im ConPTY-Pfad automatisch über `CliOutputProtokollWriter` in das Aufgabenprotokoll geschrieben (`KiAusfuehrungsService.StartWithPseudoConsoleAsync`).
- `TaskDetailViewModel` lädt Protokolle über `LadeProtokolleAsync` aus `ProtokollService.GetByAufgabeAsync` in `Protokolleintraege`; eine dedizierte Export- oder Dateischreib-Logik existiert dort nicht.
- `TaskDetailView.xaml` enthält in der Gruppe `CLI` derzeit Aktionen für Plugin-Wechsel, CLI-Start/Stop und Promptvorlagen, aber keinen Export-Button.
- `IDialogService`/`WpfDialogService` enthalten aktuell keinen Save-File-Dialog für einen Zielpfad (`*.raw`).
- Es gibt umfangreiche bestehende Tests für CLI-Output-Persistenz, Protokollreihenfolge und Task-Detail-Protokollnachladen; explizite Export-Tests sind nicht vorhanden.

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)

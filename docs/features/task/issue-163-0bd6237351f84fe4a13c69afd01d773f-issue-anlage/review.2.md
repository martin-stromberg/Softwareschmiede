# Plan-Review

Datum: 2026-07-21
Status: Vollstaendig umgesetzt

## Gepruefte Punkte

- Die Korrekturrueckmeldung aus `continue.md` wurde ausschliesslich auf den Encoding-Pfad der KI-Ausfuellhilfe begrenzt.
- `CliKiPluginBase.RunOneShotTextGenerationAsync` konfiguriert stdin, stdout und stderr jetzt explizit mit UTF-8.
- `CodexPlugin.FillIssueTemplateAsync` und `ClaudeCliPlugin.FillIssueTemplateAsync` nutzen weiterhin den gemeinsamen One-Shot-Pfad und profitieren damit beide von der zentralen Encoding-Korrektur.
- `IssueCreateDialogViewModel.KiAusfuellenAsync` uebernimmt das vom Generator gelieferte Ergebnis direkt in `Body`; ein zusaetzlicher Encoding-Umbau im ViewModel ist nicht erforderlich.
- Die Tests decken deutsche Umlaute, `ß`, kaufmaennisches Und und Euro-Zeichen im Prozesspfad und im Dialog-Body ab.

## Offene Aufgaben

Keine.

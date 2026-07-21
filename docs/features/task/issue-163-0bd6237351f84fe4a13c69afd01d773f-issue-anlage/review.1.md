# Plan-Review

Datum: 2026-07-21
Status: Vollstaendig umgesetzt

## Gepruefte Punkte

- Der Dialogpfad `IssueCreateDialogViewModel.KiAusfuellenAsync` uebergibt weiterhin `SelectedTemplate.Body` und `_originalRequirement` an `IIssueTemplateTextGenerator.FillIssueTemplateAsync`.
- `CodexPlugin.FillIssueTemplateAsync` baut den Prompt aus Template-Body und Originalanforderung und transportiert ihn verlaesslich ueber StandardInput an `codex exec`.
- `ClaudeCliPlugin.FillIssueTemplateAsync` baut denselben Prompt und transportiert ihn im Print-Modus ueber StandardInput, statt den kompletten Template-Inhalt als Kommandozeilenargument zu verwenden.
- Neue Tests belegen die Dialog-Weitergabe, den Codex-Invocation-Aufbau und den Claude-Invocation-Aufbau.

## Offene Aufgaben

Keine.

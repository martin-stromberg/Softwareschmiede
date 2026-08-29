# Plan-Check – KI-Funktion in Issue-Anlage mit Devin & Copilot

## Status

**Plan vollständig.**

## Überprüfte Dimensionen

### Annahmen

- Devin CLI unterstützt einen nicht-interaktiven One-Shot-Modus (`devin -p "prompt"`).
  - Belegt durch offizielle Dokumentation (https://docs.devin.ai/cli/reference/commands und https://docs.devin.ai/cli/essential-commands).
- GitHub Copilot CLI unterstützt einen nicht-interaktiven One-Shot-Modus (`copilot -p "prompt" -s --no-ask-user`).
  - Belegt durch offizielle Dokumentation (https://docs.github.com/en/copilot/reference/copilot-cli-reference/cli-programmatic-reference).
- `CliKiPluginBase.RunOneShotTextGenerationAsync` ist die korrekte Infrastruktur für One-Shot-Prompts.
  - Belegt durch bestehende Implementierungen in `CodexPlugin` und `ClaudeCliPlugin`.
- `IssueCreateDialogViewModel` filtert auf `IIssueTemplateTextGenerator`.
  - Belegt durch Code in `IssueCreateDialogViewModel.Initialize` (Zeile 218-225).

### Entscheidungen

- `DevinPlugin` und `GitHubCopilotPlugin` implementieren `IIssueTemplateTextGenerator` analog zu `CodexPlugin`/`ClaudeCliPlugin`.
- One-Shot-Aufrufe nutzen `ProcessStartInfo.ArgumentList` zur korrekten Argument-Escapung.
- Devin wird mit `--respect-workspace-trust false` aufgerufen, damit der Prompt in `Path.GetTempPath()` nicht an Workspace-Trust scheitert.
- Für E2E-Tests wird der `KiSimulatorPlugin` um `IIssueTemplateTextGenerator` erweitert, um das Ausfüllen ohne echte CLI testen zu können.

### Risiken

- **Gering:** Benutzerdefinierte `CommandLineParameters` können den One-Shot-Modus beeinflussen. Akzeptiert, da es Anwender-Konfiguration ist.
- **Gering:** Authentifizierung für Copilot erfordert `GH_TOKEN` oder `COPILOT_GITHUB_TOKEN`. `GitHubCopilotPlugin` setzt `GH_TOKEN`, was für `gh copilot` ausreichend sein sollte.
- **Gering:** Devin-Versionen vor Einführung von `--print`/`-p` könnten fehlschlagen. Voraussetzung: Aktuelle Devin CLI.

### Vollständigkeit

- Happy-Path, Negative-Cases (kein Textgenerator, fehlende CLI) und Regression für Codex/Claude sind abgedeckt.
- UI-E2E-Test ist vorgesehen.
- Dokumentationsaktualisierung ist vorgesehen.

## Befunde

Keine kritischen oder wesentlichen Mängel. Der Plan kann umgesetzt werden.

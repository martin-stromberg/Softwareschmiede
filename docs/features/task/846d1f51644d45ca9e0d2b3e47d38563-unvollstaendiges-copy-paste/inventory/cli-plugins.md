# KI-/CLI-Plugin-Bezug

## Relevante Dateien

- `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IKiPlugin.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/CliKiPluginBase.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Plugins/ClaudeCliPluginTests.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Plugins/CodexPluginTests.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubCopilotPluginTests.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Plugins/DevinPluginTests.cs`

## Plugin-Rolle im Startpfad

KI-Plugins liefern ueber `IKiPlugin.StartCliAsync` eine `ProcessStartInfo`. `KiAusfuehrungsService.StartWithPseudoConsoleAsync` baut daraus einen Kommandozeilenstring und sendet diesen nach dem Start von `cmd.exe` in die Pseudokonsole.

Der Plugin-spezifische Teil endet damit beim Startbefehl. Die interaktive Eingabe danach, einschliesslich Copy & Paste in die laufende Pseudokonsole, laeuft ueber die gemeinsame `PseudoConsoleSession` und nicht ueber eine Claude-spezifische Schnittstelle.

## Claude-Bezug

Die Anforderung nennt Claude als beobachteten Fall. Aus Code-Sicht ist Claude relevant, weil Claude-CLI-Sitzungen denselben `TerminalControl`-/`PseudoConsoleSession`-Pfad verwenden wie andere KI-CLI-Plugins, sofern sie ueber die Development-Automation-Plugin-Auswahl gestartet wurden.

Eine isolierte Aenderung am Claude-Plugin waere daher nur sinnvoll, wenn sich bei der Reproduktion zeigt, dass der Startbefehl, die Shell-Konfiguration oder Claude-spezifische Terminalmodi das Paste-Verhalten beeinflussen. Die aktuelle Bestandsaufnahme zeigt keinen Claude-spezifischen Paste-Code.

## Regressionserwartung

Eine Korrektur im gemeinsamen Input-Pfad wirkt auf alle CLI-Plugins:

- Claude
- Codex
- GitHub Copilot
- Devin
- KiSimulator bzw. Test-Plugins, soweit sie ueber die Pseudokonsole laufen

Deshalb sollten Tests nicht nur Claude namentlich abbilden, sondern den gemeinsamen Mechanismus pruefen. Ein optionaler E2E-/Integrationstest kann spaeter mit einem kontrollierbaren CLI-Prozess arbeiten, um pluginunabhaengig nachzuweisen, dass lange mehrzeilige Eingaben vollstaendig ankommen.

## Nicht relevante Pfade

`CliSessionService` verwaltet einen klassischen Prozess mit Standard-Input/-Output und `SendAsync(string input)`, das `WriteLine` verwendet. Dieser Dienst ist nicht der sichtbare Pseudokonsolen-Paste-Pfad aus der Anforderung. Er ist nur als historischer oder alternativer CLI-Pfad relevant, falls einzelne Funktionen nicht ueber ConPTY laufen.

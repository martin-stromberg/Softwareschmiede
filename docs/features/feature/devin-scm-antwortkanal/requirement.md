# Übersetzte Anforderung: Devin CLI im SCM-Antwortkanal

## Fachliche Zusammenfassung

Der SCM-Ereignis-Antwortkanal (GitHub-Ereignisse → KI-CLI) unterstützt derzeit ausschließlich
`Softwareschmiede.Codex`, `Softwareschmiede.ClaudeCli` und den `Softwareschmiede.KiSimulator`.
Aufgaben mit konfigurierter Devin-CLI (`Softwareschmiede.Devin`, `devin.exe`) erhalten zugestellte
Ereignisse nie, weil `ScmCliAccessEvaluator` den Prefix pauschal als `Unknown` bewertet und
`ScmPromptSessionAdapter` weder Devins Banner noch dessen Eingabeprompt erkennt. Zu liefern ist ein
eigener, sicherer Startvertrag für Devin: Whitelist-Auswertung der Devin-Startparameter
(u. a. `--permission-mode`, `--respect-workspace-trust`), gepinnte Banner-/Prompt-Erkennung sowie
der unveränderte Kanalnachweis über die Dateitool-Probe. Ein SCM-Ereignis an eine Devin-Aufgabe soll
den vollständigen Pfad bis `Zugestellt` durchlaufen.

## Betroffene Klassen und Komponenten

- `ScmCliAccessEvaluator` (`src/Softwareschmiede/Application/Services/ScmCliAccessEvaluator.cs`) —
  `Capture(...)`: Devin-Konfigurationsdateien erfassen; `Evaluate(...)`: Devin-Whitelist für
  Startparameter und Konfigurationsinhalte.
- `ScmPromptSessionAdapter` (`src/Softwareschmiede/Application/Services/ScmPromptSessionAdapter.cs`) —
  `TrustDialogMarkers` um Devins Trust-Dialog erweitern; `RecognizeStartup` um Devin-Banner;
  `RefreshScreen` um Devin-Promptzeile und Busy-Marker.
- `ScmCliWorkspaceTrust` — prüfen, ob für Devin eine Trust-Vorbelegung nötig ist
  (Devin wird bereits mit `--respect-workspace-trust false` gestartet).
- `DevinPlugin` (`plugins/Softwareschmiede.Plugin.Devin/DevinPlugin.cs`) — Startparameterquelle
  (`optionalParameters` + `CommandLineParameters`-Credential); voraussichtlich keine Änderung nötig.
- Tests:
  - `ScmEventCliExchangeAccessTests` (`src/Softwareschmiede.IntegrationTests/Services/`, inkl.
    `.PendingProbe.cs`) — Devin-Fälle in den bestehenden Theory-Mustern.
  - `ScmDispatchFixture`-basierte Dispatch-Tests — vollständiger Zustellpfad mit Devin-Profil.
- Dokumentation: `docs/help/plugins/github-ereignisse/` (Derzeit-Abschnitt nennt Devin explizit als
  nicht unterstützt), `README.md` (Abschnitt „CLI-Unterstützung"), `changes.log`.

## Implementierungsansatz

- Erweiterung der beiden vorhandenen Auswertungs-/Erkennungspfade um `Softwareschmiede.Devin` —
  keine neuen Klassen, keine geänderten Contracts (`IScmPromptSessionAdapter`,
  `ScmExchangeAccess`, `ScmPromptReadyStatus` bleiben unverändert).
- Der Kanal-Sicherheitsvertrag bleibt unverändert: Vor erfolgreicher Klassifizierung und bestandener
  Dateitool-Probe (`ScmExchangeAccessProbe`) wird kein Ereignisinhalt in die Sitzung geschrieben.
- Devin-Startvertrag (zu begründende Designentscheidung): `--permission-mode` mit den Modi
  `accept-edits`, `dangerous` und `bypass` (bypass = produktiv beobachteter Legacy-Wert) gilt als
  Schreibfreigabe-Kandidat; `auto` und `smart` geben keine automatische Schreibfreigabe → `Unknown`.
  Die tatsächliche Freigabe beweist allein die Dateitool-Probe — ein pauschales „alles erlaubt" ohne
  Kanalnachweis gibt es nicht.
- Whitelist für übrige Devin-Parameter: `--respect-workspace-trust true|false`, `--model`,
  `-c`/`--continue`, `-r`/`--resume` (optionaler Wert), `--sandbox`; `--config`, `--cloud`,
  `-p`/`--print`, `--export`, `--prompt-file` und alle unbekannten Parameter → `Unknown`.
- Env-Variable `DEVIN_PERMISSION_MODE` in `spec.EnvironmentVariables` wird wie der Parameter
  gewertet (Argument überschreibt Env).
- Devin-Konfiguration: `~/.devin/config.json` und `~/.config/devin/config.json` erfassen;
  `permissions.deny`/`permissions.ask` (nicht leer) → `Denied`, `defaultMode`-ähnliche Felder in die
  Modusauswertung einbeziehen; unbekannte restliche Schlüssel tolerieren (die Probe bleibt der
  Nachweis), Managed-Dateien (`*managed*`) bleiben `Unknown`.
- Prompt-Erkennung (belegt durch `devin.exe` v3000.11.92 auf dem Entwicklungsrechner):
  Banner `Devin CLI` + `v3000.11.92`; Eingabezeile beginnt mit `❭` (U+276D);
  Busy-Marker `esc twice to interrupt` / `esc again to interrupt`;
  Trust-Dialog `Do you trust the authors of this directory?`.

## Konfiguration

Keine neue Anwendungskonfiguration. Die bestehende Plugin-Einstellung
`Softwareschmiede.Devin.CommandLineParameters` muss einen unterstützten Startvertrag enthalten
(z. B. `--permission-mode accept-edits --respect-workspace-trust false`).

## Offene Fragen

- Keine. Das Banner-/Promptformat wurde am installierten `devin.exe` (v3000.11.92,
  `C:\Users\Martin\AppData\Local\devin\cli\bin\devin.exe`) verifiziert; ältere/abweichende
  Devin-Versionen bleiben bewusst `Unknown` (Pinning-Prinzip der Bestands-CLIs).

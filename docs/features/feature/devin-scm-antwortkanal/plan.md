# Umsetzungsplan: Devin CLI im SCM-Antwortkanal

## Übersicht

`ScmCliAccessEvaluator` und `ScmPromptSessionAdapter` werden um den Prefix `Softwareschmiede.Devin`
erweitert: Whitelist-Auswertung der Devin-Startparameter und -Konfiguration, gepinnte
Banner-/Prompt-/Busy-/Trust-Erkennung für Devin CLI `v3000.11.92`. Der bestehende
Kanal-Sicherheitsvertrag (kein Ereignisinhalt vor Kanalnachweis; Dateitool-Probe als einziger
Freigabenachweis) bleibt unverändert und ist der eigentliche Nachweis — die Klassifizierung steuert
nur, ob die Probe überhaupt gesendet wird. Keine neuen Klassen, keine Contract-Änderungen.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Devin `--permission-mode` | `accept-edits`, `dangerous` und `bypass` → Kandidat für `Supported`; `auto`, `smart` und unbekannte Werte → `Unknown` | `accept-edits` entspricht Claude-`acceptEdits` (Workspace-Edits auto-freigegeben). `dangerous`/`bypass` ist eine umfassendere Freigabe, aber keine „pauschale Freigabe ohne Kanalnachweis": Die Einstufung erlaubt nur das Senden der inhaltslosen Probe; den tatsächlichen Schreibzugriff beweist allein die Dateitool-Probe unter der rungebundenen Kanal-ACL. `bypass` wird als produktiv beobachteter Legacy-Wert mitbewertet, da die installierte CLI (v3000.11.92) nur `auto`/`accept-edits`/`smart`/`dangerous` dokumentiert. `auto`/`smart` können die Probe nicht zuverlässig erfüllen (nur lesende bzw. modellbewertete Freigaben) → `Unknown`. |
| Devin-Konfigurationsdateien | `~/.devin/config.json`, `~/.config/devin/config.json` und `<workdir>/.devin/config.json` erfassen; `permissions.deny`/`permissions.ask` (nicht leer) → `Denied`; `permissions.defaultMode` (String) als modussetzend werten; übrige Schlüssel tolerieren | Devins `config.json`-Schema ist nicht vollständig dokumentiert; auf dem Referenzsystem enthält es freie Schlüssel (`hooks`, `encoding`). Eine strikte Vollwhitelist machte `Supported` faktisch unerreichbar. Da Konfiguration höchstens einschränken, nie den Probennachweis ersetzen kann, genügt die Auswertung der berechtigungsrelevanten Felder; die Datei fließt in Fingerprint und `IsUnchanged`-Invalidierung ein. `*managed*`-Dateien bleiben `Unknown` (bestehende Regel greift prefixübergreifend). |
| `DEVIN_PERMISSION_MODE` (Env) | Wenn in `spec.EnvironmentVariables` gesetzt: Wert wird wie `--permission-mode` gewertet; explizites Argument gewinnt | Die CLI wertet die Variable dokumentiert aus; ein unbekannter Env-Wert ohne Argument → `Unknown`. |
| Prompt-Erkennung | Banner `Devin CLI` + `v3000.11.92` in `_recognizedCli`; Cursorzeile `❭` oder `❭ …` (U+276D) → `Ready`; `esc twice to interrupt`/`esc again to interrupt` → `Busy`; `do you trust the authors of this directory` → `TrustPrompt` | Am installierten `devin.exe` verifiziert (TUI-Start, Trust-Dialog, Help-Ausgabe, Binary-Strings). Pinning-Prinzip der Bestands-CLIs wird fortgesetzt; andere Versionen bleiben `Unknown`. |
| `--respect-workspace-trust` | Nur mit explizitem Wert `true`/`false` whitelisted (auch `--respect-workspace-trust=false`); bare Flag → `Unknown` | Devin-Dokumentation: optionales Bool; `false` überspringt den Trust-Check (für Automatisierung nötig), `true` bleibt sicher, weil der Adapter offene Trust-Dialoge als `TrustPrompt` markiert und keine Eingabe sendet. |
| Weitere Devin-Parameter | Whitelist: `--model`/`-m` (Wert), `-c`/`--continue`, `-r`/`--resume` (bare oder `--resume=<id>`), `--sandbox` (Flag). Alles andere (`--config`, `--cloud`, `-p`/`--print`, `--export`, `--prompt-file`, positionale Argumente, `--`, Subkommandos) → `Unknown` | `--continue`/`--resume` werden vom Host bei Session-Fortsetzung tatsächlich übergeben (`SupportsSessionContinuation`). `--sandbox` schränkt höchstens ein (Workspace-Schreibzugriff reicht für den Kanal). Nicht whitelistbare Parameter können Rechte oder den Ausführungsmodus unauflösbar ändern. |
| Workspace-Trust | Kein `ScmCliWorkspaceTrust`-Eintrag für Devin | Devin hat keinen dokumentierten pfadgenauen Vorbelegungsmechanismus in einer Benutzerkonfiguration; das Plugin startet bereits mit `--respect-workspace-trust false`, und `TrustPrompt` bleibt als Wartezustand abgesichert. |
| E2E-Nachweis | `ScmDispatchFixture` um wählbares KI-Profil (Prefix, Banner, Promptzeile, Env-Variablen, Probe-Vervollständigung) erweitern; Dispatch-Test mit Devin-Profil bis `Zugestellt` | Erfüllt die geforderte Ende-zu-Ende-Abdeckung mit bestehender Infrastruktur, ohne FlaUI/ConPTY. |

## Programmabläufe

### SCM-Ereigniszustellung an Devin-Aufgabe (Happy Path)

1. `ScmEventOrchestrator.DispatchCoreAsync` löst `KiPlugin` `Softwareschmiede.Devin` auf und startet
   die Sitzung (`devin.exe` im Aufgaben-Arbeitsverzeichnis, Args z. B.
   `--permission-mode accept-edits --respect-workspace-trust false`).
2. `KiAusfuehrungsService` ruft `ScmCliAccessEvaluator.Capture("Softwareschmiede.Devin", spec)` —
   erfasst die drei Devin-Konfigurationspfade — und erzeugt den `ScmPromptSessionAdapter`.
3. Devin zeigt Banner `Devin CLI`/`v3000.11.92` → `_recognizedCli = true`; Cursorzeile `❭ …` →
   `Ready`.
4. `WaitForInputReadyAsync` → `Ready`; `CheckExchangeAccessAsync`: `Evaluate` klassifiziert den
   whitelisted Startvertrag als `Supported`, bereitet die `ScmExchangeAccessProbe` vor und sendet den
   Probe-Prompt (kein Ereignisinhalt).
5. Devins Dateitool schreibt `proof.json`/`challenge-read.json`; `ReadAccessProbeAsync` bestätigt →
   `Supported`.
6. `PrepareAsync` legt den rungebundenen Attempt an; `WritePromptAsync` revalidiert und schreibt den
   Ereignis-Prompt → Status `Zugestellt`.

### Verweigerte/unklare Startverträge

1. `--permission-mode auto|smart|<unbekannt>`, nicht whitelistbare Parameter, `*managed*`-Dateien
   oder `permissions.deny`/`ask`-Regeln → `Unknown` bzw. `Denied`.
2. Orchestrator schreibt Wartegrund „Antwortkanal für diese CLI nicht verfügbar: <Reason>" — kein
   Prompt, keine Probe wird gesendet (außer `Supported`-Klassifizierung ohne bestandene Probe:
   nur die inhaltslose Probe läuft, Status bleibt wartend).

### Trust-Dialog bei Devin

1. `--respect-workspace-trust true` oder Dialog trotz `false` sichtbar → `TrustDialogMarkers` trifft
   `Do you trust the authors of this directory` → `TrustPrompt`.
2. Orchestrator meldet den bestehenden Trust-Wartegrund; nach Beantwortung läuft der Probe-Pfad
   weiter (Resend-Mechanik unverändert).

## Neue Klassen

Keine.

## Änderungen an bestehenden Klassen

### `ScmCliAccessEvaluator` (Logik)

- **Geänderte Methode `Capture`:** zusätzlicher Zweig `prefix == "Softwareschmiede.Devin"` — Pfade
  `~/.devin/config.json`, `~/.config/devin/config.json` (bzw. `XDG_CONFIG_HOME`-Belegung aus
  `spec.EnvironmentVariables`/Prozess) und `<spec.WorkingDirectory>/.devin/config.json` erfassen.
- **Geänderte Methode `Evaluate`:** Prefix-Gate um `"Softwareschmiede.Devin"` erweitern;
  Devin-Dateien als JSON lesen: `permissions.deny`/`permissions.ask` mit nicht-leerem Array →
  `Denied`; `permissions.defaultMode` (String) oder Top-Level-String `permissionMode`/
  `permission_mode` setzt `mode`, sofern kein `--permission-mode`-Argument folgt (Argumente werden
  nach den Dateien ausgewertet und überschreiben); unbekannte übrige Schlüssel tolerieren.
- **Token-Whitelist in `Evaluate`:** für Devin `--permission-mode` (Wert; setzt `mode`),
  `--respect-workspace-trust` (Wert `true`/`false` nötig; `--respect-workspace-trust=<wert>`),
  `--model`/`-m` (Wert; bestehende Regel), `-c`/`--continue`, `-r`/`--resume` und
  `--resume=<id>` (Flags), `--sandbox` (Flag). Jeder andere Token → `Unknown`.
- **Env-Auswertung in `Evaluate` (nur Devin):** `spec.EnvironmentVariables["DEVIN_PERMISSION_MODE"]`
  wird als `mode` gewertet, wenn kein `--permission-mode`-Argument gesetzt wurde; unbekannter Wert →
  `Unknown`.
- **Supported-Regel in `Evaluate`:** für Devin `mode is "accept-edits" or "dangerous" or "bypass"` →
  `Supported`; `mode` gesetzt aber nicht freigebend → `Unknown`; ohne `mode` → `Unknown`.
- Begründungstexte auf Deutsch im Stil der vorhandenen Reasons; Devin-spezifische Reason z. B.
  „Keine unterstützte automatische Dateifreigabe (Devin accept-edits/dangerous) erfasst."

### `ScmPromptSessionAdapter` (Logik)

- **`TrustDialogMarkers`:** `"do you trust the authors of this directory"` ergänzen.
- **`RecognizeStartup`:** `PluginPrefix == "Softwareschmiede.Devin"` und `text` enthält
  `Devin CLI` sowie `v3000.11.92` → `_recognizedCli = true`.
- **`RefreshScreen`:** Busy-Erkennung für Devin um `esc twice to interrupt`/
  `esc again to interrupt` erweitern (OrdinalIgnoreCase); Prompt-Regel
  `PluginPrefix == "Softwareschmiede.Devin"` und Cursorzeile (getrimmt) ist `❭` oder beginnt mit
  `❭ ` → `Ready`.

### `ScmDispatchFixture` (Testhilfe)

- **Neue optionale Konstruktor-/Konfigurationspunkte:** wählbarer Ki-Prefix
  (Standard bleibt `Softwareschmiede.KiSimulator`), wählbares Banner, wählbare Ready-Promptzeile,
  wählbare Spec-Argumente/Env-Variablen sowie ein optionaler `WritePromptAsync`-Callback zum
  Vervollständigen der Dateitool-Probe (Standard: nur `Prompts.Add`). Bestehende Aufrufer bleiben
  unverändert (Standardwerte = bisheriges Simulator-Verhalten).

## Datenbankmigrationen

Keine.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `spec.Arguments` (Devin) | Nur whitelisted Tokens; Wert-Parameter brauchen Wert | `Unknown` |
| `--respect-workspace-trust` | Wert muss `true`/`false` sein | `Unknown` |
| `--permission-mode` (Devin) | `accept-edits`/`dangerous`/`bypass` → Kandidat; sonst `Unknown` | `Unknown` |
| `permissions.deny`/`ask` (Devin-Config) | nicht-leeres Array | `Denied` |
| `DEVIN_PERMISSION_MODE` (Env) | unbekannter Wert | `Unknown` |

## Konfigurationsänderungen

Keine neuen Konfigurationseinträge. Dokumentiert wird die Anforderung an
`Softwareschmiede.Devin.CommandLineParameters`.

## Seiteneffekte und Risiken

- **Codex/Claude-Pfade:** Prefix-Verzweigungen werden ausschließlich erweitert; bestehende
  InlineData-Fälle decken Regressionen ab.
- **Fremde Terminalinhalte:** `Devin CLI`-Banner-Erkennung ist bewusst versionsgepinnt; Screenshots
  anderer Tools mit ähnlichem Text bleiben `Unknown`.
- **Devin-Probe:** unter `--permission-mode auto` wird `Evaluate` `Unknown` → es wird nie eine Probe
  gesendet; korrekt, da Devin Schreibfreigaben sonst interaktiv erfragen würde.
- **Plugin-Parameter:** `BuildIssueTemplateFillInvocation` (`-p`, `--respect-workspace-trust false`)
  betrifft den Einmal-Lauf außerhalb des Terminals — keine Änderung, kein Einfluss auf den Kanal.

## Umsetzungsreihenfolge

1. **Evaluator: Devin-Capture + Evaluate-Whitelist**
   - Voraussetzungen: keine
   - Beschreibung: `Capture`-Zweig, Prefix-Gate, Devin-JSON-Auswertung, Token-Whitelist,
     `DEVIN_PERMISSION_MODE`-Env, Supported-Regel.
2. **Adapter: Devin-Erkennung**
   - Voraussetzungen: keine
   - Beschreibung: `TrustDialogMarkers`, `RecognizeStartup`, Busy-Marker, `❭`-Promptregel.
3. **Fixture-Erweiterung für Devin-Profil**
   - Voraussetzungen: keine
   - Beschreibung: `ScmDispatchFixture` optional parameterisierbar machen (Prefix/Banner/Prompt/Args/
     Env/Probe-Callback), bestehende Defaults unverändert.
4. **Integrationstests Devin**
   - Voraussetzungen: Schritte 1–3
   - Beschreibung: Devin-Fälle in `ScmEventCliExchangeAccessTests`-Theorien und neue Dispatch-Tests.
5. **Build + Testlane**
   - Voraussetzungen: Schritte 1–4
   - Beschreibung: `dotnet build Softwareschmiede.slnx`, dann gefilterte Testläufe (siehe CLAUDE.md).
6. **Dokumentation** (`docs/help/plugins/github-ereignisse/`, `README.md`, `changes.log`)
   - Voraussetzungen: Schritte 1–5

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `ActualStartSpec_AllowsWorkspaceChannelWithoutAddingBypass` | `ScmEventCliExchangeAccessTests` | Devin-InlineData (`--permission-mode accept-edits --respect-workspace-trust false`, Banner, `❭`-Prompt) — Probe-Prompt ohne Ereignisinhalt, kein `Supported` vor Probe |
| `ActualStartSpec_RejectsOverridesWithoutMutatingThem` | dto. | Devin: `auto`/`smart`/unbekannte Modi → `Unknown`; `--config`, `--cloud`, `--print`, positionale Tokens, bare `--respect-workspace-trust` → `Unknown`; `dangerous`/`bypass` → `Supported` |
| `ConfigurationFixtures_EnforceRestrictionsAndPreserveFiles` | dto. | Devin-JSON: `permissions.deny`/`ask` → `Denied`; `defaultMode:"accept-edits"` → `Supported`; freie Schlüssel → `Supported` nur mit whitelistbarem Argument |
| `LivingRun_RequiresUnchangedCapturedConfiguration` | dto. | Devin-InlineData — Konfigurationsänderung/Laufwechsel invalidiert |
| `PendingProbe_ResendsAfterDelay_UpToCap`, `ProbeLostToTrustDialog_ResumesAfterAnswer`, `TrustPrompt_NeverReceivesProbeOrConfirmation` | `.PendingProbe.cs` | Devin-InlineData inkl. Devin-Trust-Text `Do you trust the authors of this directory?` |
| `Spec`-Helfer | dto. | `DevinPlugin`-StartSpec über `ICredentialStore`-Mock aufnehmen |
| Devin-Evaluierung ohne Plugin (reine Specs/Args/Env) | dto. oder neue Theory | `DEVIN_PERMISSION_MODE` Env-Wertung; `--resume`, `--continue`, `--sandbox` whitelisted |
| Dispatch bis `Zugestellt` mit Devin-Profil | neue Testklasse `ScmEventDispatchTests_Devin` o. ä. | `ScmDispatchFixture` mit Devin-Prefix/Banner/`❭`-Prompt; Probe-Callback vollendet die Dateioperationen; Ereignis endet `Zugestellt`, Prompt landet in `Prompts` |
| Devin-Dispatch ohne freigebenden Modus | dto. | `auto`-Args → Ereignis wartet mit Kanal-Reason, kein Prompt gesendet |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `Spec`-Helfer in `ScmEventCliExchangeAccessTests` | `provider`-Mapping um `Devin`/`DevinPlugin` erweitern |
| `ScmDispatchFixture` | optionale Profil-Parameter; bestehende Tests unverändert |

### E2E-Tests (Pflicht)

Der Happy Path wird durch den `ScmDispatchFixture`-basierten Integrations-Dispatch-Test bis
`Zugestellt` abgedeckt (echte Services, SQLite, Terminal-Boundary als Mock). Kein neuer FlaUI-/ConPTY-E2E-Test:
kein neues UI-Verhalten; die vorhandenen E2E-Simulator-Tests bleiben unverändert.

## Offene Punkte

Keine.

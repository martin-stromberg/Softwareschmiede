# CLI-Startparameter und Trust-Mechanismen (Prüfauftrag)

Zurück: [../inventory.md](../inventory.md)

## Verifizierte Trust-Mechanismen (externe Recherche, Stand der Umsetzung)

### Claude Code

- **Trust-Status** liegt in `~/.claude.json` unter `projects["<absoluter Projektpfad>"].hasTrustDialogAccepted` (bool). Die Vorbelegung pro Verzeichnis ist der von Claude Code selbst in seiner Fehlermeldung empfohlene Weg („…or set `projects["<dir>"].hasTrustDialogAccepted: true`").
- **Es gibt kein CLI-Flag** (`--trust` o. ä. existiert nicht).
- `--dangerously-skip-permissions` umgeht den Dialog, setzt den Trust-Status aber **nicht** persistent und deaktiviert sämtliche Berechtigungsabfragen → verletzt den Sicherheitsvertrag des Kanals (Evaluator würde ohnehin `Unknown` liefern, da freier Parameter).
- Zwei Gatter: Dialog-Gate läuft Elternverzeichnisse bis zur Git-Wurzel hoch; Settings-Gate (ob `.claude/settings.json` des Projekts honoriert wird) verlangt **exakten** Pfadschlüssel.
- Aktivierung von Trust lädt projektgebundene `.claude/`-Konfiguration (Settings, Hooks, Commands) → **Vorbelegung nur, wenn `<workdir>/.claude` nicht existiert.**
- `~/.claude.json` wird vom `ScmCliAccessEvaluator.Capture` nicht erfasst → Vorbelegung berührt weder Fingerprint noch `IsUnchanged`.

### OpenAI Codex

- Codex zeigt seit **v0.113** (pinned: „OpenAI Codex (v0.114.0)") einen blockierenden Trust-Dialog: „Do you trust the contents of this directory? … › 1. Yes, continue / 2. No, quit". Die Auswahl wird **nicht** persistiert — jedes neue Aufgabenverzeichnis fragt erneut.
- Dokumentierter Mechanismus: `~/.codex/config.toml` → `[projects."<absoluter Pfad>"] trust_level = "trusted"` (Werte `"trusted"`/`"untrusted"`; `untrusted` überspringt projektgebundene `.codex/`-Ebenen).
- Der Dialog erscheint trotz `approval_policy`/`sandbox_mode`-Vorgaben und trotz `--dangerously-bypass-approvals-and-sandbox` (bestätigter Upstream-Regressionsbug in v0.113/v0.114) → kein sicherer Parameter-Weg.
- `trust_target` ist die **Git-Wurzel** („Trusting will apply to the repository root"), wenn das Arbeitsverzeichnis in einem Repo liegt — der Schlüssel muss die Git-Wurzel treffen (oder das Verzeichnis, wenn kein Repo).
- Aktivierung von Trust lädt projektgebundene `.codex/`-Ebenen (config, hooks, exec policies) → **Vorbelegung nur, wenn `<workdir>/.codex` nicht existiert.**
- `CODEX_HOME` (spec-Env → Prozess-Env) verlegt den Konfigurationsort — wie in `ScmCliAccessEvaluator.Capture` (Z. 45).

## Evaluator-Whitelist (belegt)

`src/Softwareschmiede/Application/Services/ScmCliAccessEvaluator.cs`:

- `Capture` (Z. 38-64): Codex `config.toml`/`requirements.toml`/`managed_config.toml` in `CODEX_HOME`+Managed+Workdir-Kette `.codex/config.toml`; Claude `settings.json`/`settings.local.json`/Managed — **`~/.claude.json` nicht enthalten**.
- TOML-Parsing (Z. 107-143): Abschnitte ausschließlich `[sandbox_workspace_write]` oder `[features]`; Zeilen via `^([a-z_]+)\s*=\s*("…"|true|false)`; `[projects.*]` → heute `Unknown`.
- Parameter-Whitelist (Z. 167-192): Codex `--sandbox/-s`, `--ask-for-approval/-a`, `--model/-m`; Claude `--permission-mode`, `--model/-m`. Freie Parameter → `Unknown`.
- `Evaluate`-Supported: Codex `sandbox_mode == "workspace-write"`; Claude `permissions.defaultMode == "acceptEdits"` (Z. 193-196).
- `IsUnchanged` (Z. 67-71) vergleicht erfasste Dateien byte-genau — ein von der Vorbelegung geänderter `config.toml` muss **vor** `Capture` geschrieben werden (Einfügepunkt `KiAusfuehrungsService.cs:238-240`).

## Startpfad

`CliKiPluginBase.GetTerminalStartSpecAsync` → `BuildTerminalStartSpec` → `BuildProcessStartInfo` (plugin-eigen): Claude `plugins/…/ClaudeCliPlugin.cs:129-158` (frei konfigurierbare `CommandLineParameters` via `AppendCommandLineParameters`), Codex `plugins/…/CodexPlugin.cs:105-128`. `KiAusfuehrungsService.StartTerminalSessionCoreAsync` (`src/…/KiAusfuehrungsService.cs:217-348`): Spec Z. 238 → `ScmCliAccessEvaluator.Capture` Z. 240 → `factory.StartAsync` Z. 273 → Adapter Z. 300-301 (`_timeProvider` injiziert Z. 50).

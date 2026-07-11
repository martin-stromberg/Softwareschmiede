# Bestandsaufnahme: Hook-System und Build-Orchestrierung

## Hook-Konfiguration

**Datei:** `.claude/settings.json`, Abschnitt `"PreToolUse"` (Zeilen 117–129)

### PreToolUse-Hook: "Building solution before dotnet test"

```json
{
  "matcher": "Bash",
  "hooks": [
    {
      "type": "command",
      "if": "Bash(*dotnet test*)",
      "statusMessage": "Building solution before dotnet test...",
      "command": "pwsh -NoProfile -Command \"Set-Location (git rev-parse --show-toplevel); python .claude/hooks/build_before_test.py\""
    }
  ]
}
```

**Auslöser:** Vor jedem `Bash`-Befehl, der `"dotnet test"` enthält.

**Aktion:** Führt Python-Hook `build_before_test.py` aus, die einen `dotnet build` durchführt (mit shared Datei-Lock zum Schutz vor paralleler Ausführung mit dem Stop-Hook).

**Verhalten bei Build-Fehler:** Hook blockiert `dotnet test` **nicht** — Testlauf wird gestartet und zeigt den echten Build-Fehler selbst.

---

## Build-Hook-Implementierung

**Datei:** `.claude/hooks/build_before_test.py`

```python
import subprocess
from dotnet_lock import dotnet_build_lock

def main():
    with dotnet_build_lock() as got_lock:
        if not got_lock:
            print("[dotnet-lock] Timeout beim Warten auf den Build-Lock - baue trotzdem weiter.")
        result = subprocess.run(["dotnet", "build"])

    if result.returncode != 0:
        print("BUILD FAILED - fix before running tests")

if __name__ == "__main__":
    main()
```

### Logik

1. Versucht, einen **Datei-Lock** über `dotnet_build_lock()` zu erwerben (siehe unten).
2. Führt `dotnet build` aus.
   - Falls der Lock erworben wurde: gibt den Lock nach dem Build frei.
   - Falls der Lock **nicht** erworben wurde (Timeout nach 120s): gibt eine Warnung aus und fährt ohne Lock fort (verhindert, dass Claude blockiert wird).
3. Prüft `result.returncode` und gibt Meldung "BUILD FAILED" aus (blockiert jedoch nicht).

### Zweck des Locks

Der Hook nutzt einen **gemeinsamen Datei-Lock** mit dem Stop-Hook (`test-csharp-startup.ps1`), um zu verhindern, dass beide Hooks gleichzeitig in die gleichen `bin/`- und `obj/`-Verzeichnisse schreiben und diese korrumpieren (z. B. unvollständige `runtimeconfig.json` bei WPF-Projekten → ".NET Desktop Runtime nicht gefunden"-Fehler).

---

## Datei-Lock-Mechanismus

**Datei:** `.claude/hooks/dotnet_lock.py`

### Konfiguration

```python
TIMEOUT_SECONDS = 120
STALE_SECONDS = 300
POLL_INTERVAL = 0.5
```

- **TIMEOUT_SECONDS:** Wie lange auf Lock-Erwerb warten (120s).
- **STALE_SECONDS:** Lock gilt nach 300s als verwaist (z. B. nach Absturz ohne Cleanup); wird automatisch entfernt.
- **POLL_INTERVAL:** Polling-Interval beim Warten auf Lock (0,5s).

### Lock-Mechanismus

**Lock-Pfad:** `.claude/.locks/dotnet-build.lock/` (atomare Verzeichniserstellung als Mutex).

| Funktion | Beschreibung |
|----------|-------------|
| `_repo_root()` | Ermittelt Git-Repo-Root via `git rev-parse --show-toplevel`, fällt auf `os.getcwd()` zurück |
| `_lock_dir()` | Gibt Lock-Verzeichnis zurück: `.claude/.locks/dotnet-build.lock` im Repo-Root |
| `_try_acquire(lock_dir)` | Versucht Lock über `os.makedirs(lock_dir)` zu erwerben (atomar); bei Erfolg schreibt "owner.txt" mit PID und Timestamp |
| `_lock_age(lock_dir)` | Berechnet Age des Lock-Verzeichnisses (Zeit seit letzter Änderung) |
| `acquire(timeout, stale)` | Blockiert bis Lock erworben oder `timeout` überschritten; gibt `True` (Erfolg) oder `False` (Timeout) zurück; entfernt verwaiste Locks automatisch |
| `release()` | Entfernt Lock-Verzeichnis |
| `dotnet_build_lock(timeout, stale)` | Context-Manager: akquiriert Lock, yieldet `True/False`, entfernt Lock bei Erfolg im `finally` |

### Eigenschaften

- **Atomar:** Verwendet `os.makedirs()` für atomare Verzeichniserstellung.
- **Repo-Root-verankert:** Lock-Pfad unabhängig vom aktuellen Arbeitsverzeichnis.
- **Automatische Bereinigung verwaister Locks:** Falls ein Lock älter als 300s ist (z. B. nach Crash), wird es entfernt.
- **Graceful Timeout:** Nach 120s Timeout gibt die Funktion `False` zurück statt Claude zu blockieren.

---

## Stop-Hook: PowerShell-Startup-Test

**Datei:** `.claude/hooks/test-csharp-startup.ps1`

Der Stop-Hook führt einen "Smoke Test" für ausführbare C#-Projekte durch (WPF/Web/Console-Apps). Er nutzt **denselben Datei-Lock** wie `build_before_test.py`, um simultane Zugriffe auf `bin/obj` zu vermeiden.

### Lock-Synchronisierung in PowerShell

```powershell
$LockDir      = Join-Path $WorkDir '.claude\.locks\dotnet-build.lock'
$LockTimeout  = 120
$LockStaleSec = 300

function Enter-DotnetBuildLock { ... }  # Parallele Implementierung wie Python
function Exit-DotnetBuildLock { ... }
```

Die PowerShell-Funktionen `Enter-DotnetBuildLock` und `Exit-DotnetBuildLock` spiegeln exakt das Python-Lock-Protokoll wider.

### Startup-Test

Für jedes gefundene ausführbare Projekt (`*.csproj` mit `OutputType=Exe/WinExe` oder `Sdk=Microsoft.NET.Sdk.Web`, ausgenommen Test-Projekte):

1. **Port-Check:** Falls das Projekt eine `applicationUrl` oder Port in `launchSettings.json` definiert, prüft der Hook ob der Port bereits belegt ist (App läuft schon, z. B. in Visual Studio) → überspringt den Start.
2. **Build:** `dotnet build <project> --no-incremental -v quiet`.
3. **Run:** `dotnet run --project <project> --no-build` (mit 30s Timeout).
   - Überwacht stdout auf Web-Signale: "Application started", "Now listening on", "Content root path".
   - Bei Erfolg (Signal erkannt oder 30s ohne Absturz): Prozess wird gekilled (über `taskkill /F /T`).
   - Bei Absturz vor Ablauf: Fehlercode, stdout/stderr (letzte 15 Zeilen) werden reportet.

---

## Hook-Registry in settings.json

| Hook-Typ | Trigger | Datei | Zweck |
|----------|---------|-------|--------|
| `PreToolUse` / Bash | `Bash(*dotnet test*)` | `.claude/hooks/build_before_test.py` | `dotnet build` vor jedem `dotnet test` |
| `Stop` | Ende der Sitzung | `.claude/hooks/test-csharp-startup.ps1` | Smoke Test für ausführbare Projekte (WPF, Web, Console) |
| `Stop` | Ende der Sitzung | `.claude/hooks/log_token_usage.py` | Token-Nutzung loggen |
| `SubagentStop` | Ende eines Subagenten | `.claude/hooks/log_token_usage.py` | Subagent Token-Nutzung loggen |
| `PostToolUse` / Edit\|Write | Nach Datei-Änderung | Verschiedene `.claude/hooks/check_*.py` | Validierungen für AL-Labels, XML-Docs, Razor-Komponenten, CSS-Klassen, EF EntityState, etc. |

---

## Timing und Sequenzierung

**Szenario: `dotnet test` ausführen**

1. Claude empfängt `Bash(dotnet test ...)`-Befehl.
2. **PreToolUse-Hook wird ausgelöst** → `build_before_test.py`.
3. Lock erwerben (mit 120s Timeout):
   - Falls bereits von Stop-Hook gehalten: warten bis frei.
   - Falls Timeout: Warnung ausgeben, ohne Lock weitermachen.
4. `dotnet build` ausführen.
5. Lock freigeben.
6. **Dann** wird der eigentliche `dotnet test`-Befehl ausgeführt.

**Szenario: Claude-Sitzung endet**

1. **Stop-Hook wird ausgelöst** → `test-csharp-startup.ps1`.
2. Lock erwerben (mit 120s Timeout).
3. Für jedes ausführbare Projekt: Build + Startup-Test (mit Port-Check).
4. Lock freigeben.
5. Token-Nutzung loggen.

Falls PreToolUse-Hook gerade Lock hält: Stop-Hook wartet bis zu 120s.
Falls Stop-Hook gerade Lock hält: nächster `dotnet test` wartet bis zu 120s.

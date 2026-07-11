"""
PreToolUse-Hook: baut die Solution, bevor ein "dotnet test"-Befehl laeuft.

Nutzt denselben Lock wie test-csharp-startup.ps1 (siehe dotnet_lock.py),
damit dieser Build nicht parallel zum automatischen WPF-Start-Smoke-Test
im Stop-Hook laeuft und dessen obj/bin-Ordner korrumpiert.

WICHTIG: Der Lock wird hier bewusst NICHT wieder freigegeben (kein
"with dotnet_build_lock()"), sondern bleibt ueber das Ende dieses Skripts
hinaus bestehen. Der eigentliche, oft mehrere Minuten laufende
"dotnet test"-Prozess startet erst NACH diesem PreToolUse-Hook und wuerde
sonst ungeschuetzt neben einem parallel feuernden Stop-Hook laufen (der
E2E-Tests starten wiederholt Softwareschmiede.App.exe, waehrend der
Stop-Hook dieselbe exe neu baut und killt - das korrumpiert obj/bin
mitten im Testlauf, sichtbar z.B. als fehlende runtimeconfig.json /
".NET Desktop Runtime nicht gefunden"). Die Freigabe uebernimmt der
PostToolUse-Hook release_build_lock.py, der auf denselben "dotnet test"-
Bash-Befehl matcht und erst nach dessen Abschluss feuert. Faellt dieser
Schritt aus (z.B. abgebrochener Bash-Aufruf), raeumt der Stale-Lock-
Mechanismus in dotnet_lock.py (STALE_SECONDS) den verwaisten Lock nach
spaetestens 5 Minuten automatisch auf.

Blockiert "dotnet test" absichtlich NICHT bei einem Build-Fehler (wie
zuvor als reines Shell-Kommando) - der Testlauf soll den echten Fehler
selbst zeigen. Wird der Lock nach TIMEOUT_SECONDS nicht frei, baut das
Skript trotzdem (ohne Lock) statt Claude dauerhaft zu blockieren.

Vor dem Build wird zusaetzlich geprueft, ob eine Softwareschmiede.App.exe
laeuft: eine laufende Instanz kann DLLs im bin/-Ordner sperren und den
Build mit MSB3027/MSB3026 fehlschlagen lassen. Der Hook beendet diese
Instanz NIEMALS selbst (Self-Hosting-Risiko, siehe CLAUDE.md), sondern
warnt nur, damit ein solcher Fehler nachvollziehbar bleibt.

ACHTUNG: "dotnet test" muss synchron im Vordergrund laufen (kein
run_in_background). Der PostToolUse-Hook release_build_lock.py feuert
beim Bash-Tool sobald der Tool-Aufruf selbst zurueckkehrt - bei einem
Hintergrund-Lauf ist das sofort, nicht wenn der eigentliche Testprozess
fertig ist, wodurch der Lock die tatsaechliche Testlaufzeit nicht mehr
abdeckt. Ein technischer Block hierfuer (exit 2 im PreToolUse-Hook) wurde
versucht, verhindert die Hintergrundausfuehrung im Claude-Code-Harness
aber nachweislich NICHT - das muss daher als verbindliche Verhaltensregel
eingehalten werden (siehe CLAUDE.md, Abschnitt Testing).
"""
import json
import subprocess
import sys

import dotnet_lock

APP_PROCESS_NAME = "Softwareschmiede.App.exe"


def _tool_use_id():
    """Liest die tool_use_id aus dem PreToolUse-JSON auf stdin. Sie ist fuer
    das gesamte Bash-Tool-Aufruf-Paar (dieser PreToolUse-Hook UND der
    PostToolUse-Hook release_build_lock.py) identisch und dient als
    Owner-Token fuer den Lock (siehe dotnet_lock._is_owner)."""
    try:
        data = json.load(sys.stdin)
    except Exception:
        return None
    return data.get("tool_use_id")


def _is_app_running():
    """Prueft per tasklist, ob eine Softwareschmiede.App.exe-Instanz laeuft.
    Liefert bei Fehlern (z.B. tasklist nicht verfuegbar) False, statt den
    Build zu blockieren."""
    try:
        result = subprocess.run(
            ["tasklist", "/FI", f"IMAGENAME eq {APP_PROCESS_NAME}"],
            capture_output=True, text=True, errors="replace", check=False,
        )
        return APP_PROCESS_NAME.lower() in result.stdout.lower()
    except Exception:
        return False


def main():
    token = _tool_use_id()
    got_lock = dotnet_lock.acquire(token=token)
    if not got_lock:
        print("[dotnet-lock] Timeout beim Warten auf den Build-Lock - baue trotzdem weiter.")

    if _is_app_running():
        print(
            f"[build-warning] Eine laufende Instanz von {APP_PROCESS_NAME} wurde erkannt. "
            "Der folgende Build kann mit MSB3027/MSB3026 (Datei-Lock) fehlschlagen, falls diese "
            "Instanz DLLs im bin/-Ordner gesperrt haelt. Die Instanz wird NICHT automatisch "
            "beendet (Self-Hosting-Risiko, siehe CLAUDE.md) - bitte bei Bedarf selbst schliessen, "
            "falls es sich um eine unwichtige Testinstanz handelt."
        )

    try:
        result = subprocess.run(["dotnet", "build"])
    except Exception:
        if got_lock:
            dotnet_lock.release(token)
        raise

    if result.returncode != 0:
        print("BUILD FAILED - fix before running tests")
        if got_lock:
            # Bei Build-Fehler startet kein "dotnet test" mehr, das den
            # Lock ueber den PostToolUse-Hook freigeben koennte.
            dotnet_lock.release(token)


if __name__ == "__main__":
    main()

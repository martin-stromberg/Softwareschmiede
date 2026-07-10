"""
PreToolUse-Hook: baut die Solution, bevor ein "dotnet test"-Befehl laeuft.

Nutzt denselben Lock wie test-csharp-startup.ps1 (siehe dotnet_lock.py),
damit dieser Build nicht parallel zum automatischen WPF-Start-Smoke-Test
im Stop-Hook laeuft und dessen obj/bin-Ordner korrumpiert.

Blockiert "dotnet test" absichtlich NICHT bei einem Build-Fehler (wie
zuvor als reines Shell-Kommando) - der Testlauf soll den echten Fehler
selbst zeigen. Wird der Lock nach TIMEOUT_SECONDS nicht frei, baut das
Skript trotzdem (ohne Lock) statt Claude dauerhaft zu blockieren.
"""
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

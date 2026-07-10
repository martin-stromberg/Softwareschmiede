"""
Gemeinsamer Datei-Lock fuer dotnet build/run-Vorgaenge.

Wird von build_before_test.py (PreToolUse, vor "dotnet test") verwendet und
spiegelt das gleiche Lock-Protokoll wie test-csharp-startup.ps1 (Stop-Hook,
dort in PowerShell nachgebildet). Ohne diesen Lock koennen beide Hooks
gleichzeitig "dotnet build" fuer dasselbe Projekt ausloesen und sich dabei
die obj/bin-Ordner gegenseitig korrumpieren (z.B. unvollstaendige
runtimeconfig.json bei WPF-Projekten -> ".NET Desktop Runtime nicht
gefunden" bei E2E-Tests).

Lock-Protokoll: atomare Verzeichniserstellung (os.makedirs) als Mutex,
verankert am Git-Repo-Root, damit beide Hooks unabhaengig vom aktuellen
Arbeitsverzeichnis denselben Pfad verwenden. Ein Lock gilt nach
STALE_SECONDS als verwaist (z.B. nach einem Absturz ohne Cleanup) und wird
dann automatisch entfernt.
"""
import os
import time
import shutil
import subprocess
import contextlib

TIMEOUT_SECONDS = 120
STALE_SECONDS = 300
POLL_INTERVAL = 0.5


def _repo_root():
    try:
        result = subprocess.run(
            ["git", "rev-parse", "--show-toplevel"],
            capture_output=True, text=True, check=True,
        )
        return result.stdout.strip()
    except Exception:
        return os.getcwd()


def _lock_dir():
    return os.path.join(_repo_root(), ".claude", ".locks", "dotnet-build.lock")


def _try_acquire(lock_dir):
    try:
        os.makedirs(lock_dir)
    except FileExistsError:
        return False
    try:
        with open(os.path.join(lock_dir, "owner.txt"), "w", encoding="utf-8") as f:
            f.write(f"pid={os.getpid()} time={time.time()}\n")
    except OSError:
        pass
    return True


def _lock_age(lock_dir):
    try:
        return time.time() - os.path.getmtime(lock_dir)
    except OSError:
        return 0


def acquire(timeout=TIMEOUT_SECONDS, stale=STALE_SECONDS):
    """Blockiert bis der Lock erworben ist oder timeout ueberschritten wird.
    Gibt True zurueck bei Erfolg, False wenn nach timeout aufgegeben wurde
    (der Aufrufer laeuft dann ohne Lock weiter statt Claude zu blockieren)."""
    lock_dir = _lock_dir()
    os.makedirs(os.path.dirname(lock_dir), exist_ok=True)
    deadline = time.time() + timeout
    while True:
        if _try_acquire(lock_dir):
            return True
        if _lock_age(lock_dir) > stale:
            shutil.rmtree(lock_dir, ignore_errors=True)
            continue
        if time.time() >= deadline:
            return False
        time.sleep(POLL_INTERVAL)


def release():
    shutil.rmtree(_lock_dir(), ignore_errors=True)


@contextlib.contextmanager
def dotnet_build_lock(timeout=TIMEOUT_SECONDS, stale=STALE_SECONDS):
    """Context-Manager: yield True/False je nachdem ob der Lock erworben wurde."""
    got = False
    try:
        got = acquire(timeout=timeout, stale=stale)
        yield got
    finally:
        if got:
            release()

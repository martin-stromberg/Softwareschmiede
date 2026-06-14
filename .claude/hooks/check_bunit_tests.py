"""
bUnit-Test-Check für Razor-Komponenten und -Seiten.
Prüft, ob jede bearbeitete .razor-Datei einen entsprechenden bUnit-Test
im Projektverzeichnis hat.

Ausnahmen (werden nicht geprüft):
  - Dateien, deren Name mit _ beginnt (_Imports.razor, _Host.razor, ...)
  - App.razor, Routes.razor (App-Level-Einstiegspunkte)
"""
import sys
import json
import os

data = json.load(sys.stdin)
file = (
    data.get("tool_input", {}).get("file_path")
    or data.get("tool_response", {}).get("filePath")
    or ""
)
if not file.endswith(".razor") or not os.path.isfile(file):
    sys.exit(0)

component_name = os.path.splitext(os.path.basename(file))[0]

SKIP_NAMES = {"App", "Routes"}
if component_name.startswith("_") or component_name in SKIP_NAMES:
    sys.exit(0)

SKIP_DIRS = {"obj", "bin", ".git", "node_modules", ".vs"}


def find_csproj_dir(start_path):
    current = os.path.dirname(os.path.abspath(start_path))
    while True:
        for name in os.listdir(current):
            if name.endswith(".csproj"):
                return current
        parent = os.path.dirname(current)
        if parent == current:
            return os.path.dirname(os.path.abspath(start_path))
        current = parent


def find_solution_root(csproj_dir):
    current = csproj_dir
    while True:
        for name in os.listdir(current):
            if name.endswith(".sln"):
                return current
        parent = os.path.dirname(current)
        if parent == current:
            return csproj_dir
        current = parent


def test_exists(name, root):
    patterns = {
        f"{name}Test.cs",
        f"{name}Tests.cs",
        f"{name}Test.razor",
        f"{name}Tests.razor",
    }
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        for fname in filenames:
            if fname in patterns:
                return True
    return False


csproj_dir = find_csproj_dir(file)
solution_root = find_solution_root(csproj_dir)

if not test_exists(component_name, solution_root):
    rel = os.path.relpath(file, solution_root)
    msg = (
        "[bUnit-Test-Check] Kein bUnit-Test für Razor-Datei:\n"
        "  ✗ {}\n"
        "  Erwartet: {}Tests.cs oder {}Tests.razor\n"
        "  Bitte lege den Test an, bevor du fortfährst."
    ).format(rel, component_name, component_name)
    print(msg, file=sys.stderr)
    sys.exit(2)

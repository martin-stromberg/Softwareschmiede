"""
Razor Component Usage Check
Prüft, ob jede Razor-Komponente (ohne @page-Direktive) in mindestens einer
anderen Razor-Datei des Projekts eingebunden ist.

Ausnahmen (werden nicht geprüft):
  - Dateien mit @page-Direktive (Seiten sind Einstiegspunkte)
  - _Imports.razor (globale Imports)
  - App.razor, Routes.razor (App-Level-Einstiegspunkte)
  - Alle Dateien, deren Name mit _ beginnt
"""
import sys
import json
import os
import re

data = json.load(sys.stdin)
file = (
    data.get("tool_input", {}).get("file_path")
    or data.get("tool_response", {}).get("filePath")
    or ""
)
if not file.endswith(".razor") or not os.path.isfile(file):
    sys.exit(0)


def find_project_root(start_path):
    """Geht vom Dateiverzeichnis aufwärts, bis eine .csproj- oder .sln-Datei gefunden wird."""
    current = os.path.dirname(os.path.abspath(start_path))
    while True:
        for name in os.listdir(current):
            if name.endswith(".csproj") or name.endswith(".sln"):
                return current
        parent = os.path.dirname(current)
        if parent == current:
            # Kein Projektstamm gefunden – Verzeichnis der Datei als Fallback
            return os.path.dirname(os.path.abspath(start_path))
        current = parent


project_root = find_project_root(file)

# Alle .razor-Dateien im Projekt sammeln (obj/ und bin/ ignorieren)
razor_files = []
for dirpath, dirnames, filenames in os.walk(project_root):
    dirnames[:] = [
        d for d in dirnames
        if d not in ("obj", "bin", ".git", "node_modules", ".vs")
    ]
    for fname in filenames:
        if fname.endswith(".razor"):
            razor_files.append(os.path.join(dirpath, fname))

# Inhalte aller Razor-Dateien einlesen
file_contents = {}
for f in razor_files:
    try:
        with open(f, encoding="utf-8") as fh:
            file_contents[f] = fh.read()
    except Exception:
        pass

# Namen, die grundsätzlich von der Prüfung ausgenommen sind
SKIP_NAMES = {"App", "Routes"}


def is_entry_point(path, content):
    """True, wenn die Datei ein Einstiegspunkt ist und nicht eingebunden werden muss."""
    name = os.path.splitext(os.path.basename(path))[0]
    # Dateien, deren Name mit _ beginnt (_Imports, _Host, ...)
    if name.startswith("_"):
        return True
    # Bekannte App-Level-Dateien
    if name in SKIP_NAMES:
        return True
    # Seiten mit @page-Direktive
    if re.search(r"^\s*@page\s+", content, re.MULTILINE):
        return True
    return False


def is_used_in_project(component_name, component_path, all_contents):
    """Prüft, ob component_name in irgendeiner anderen Datei als Tag oder @layout referenziert wird."""
    # <ComponentName> / <ComponentName /> / <ComponentName attribut=...>
    tag_pattern = re.compile(r"<" + re.escape(component_name) + r"[\s/>@]")
    # @layout ComponentName
    layout_pattern = re.compile(r"@layout\s+" + re.escape(component_name) + r"\b")
    # typeof(ComponentName) — used e.g. for DefaultLayout="typeof(MainLayout)"
    typeof_pattern = re.compile(r"typeof\s*\(\s*" + re.escape(component_name) + r"\s*\)")

    for path, content in all_contents.items():
        if path == component_path:
            continue
        if tag_pattern.search(content) or layout_pattern.search(content) or typeof_pattern.search(content):
            return True
    return False


unused = []
for f, content in file_contents.items():
    if is_entry_point(f, content):
        continue
    component_name = os.path.splitext(os.path.basename(f))[0]
    # Razor-Komponenten beginnen per Konvention mit Großbuchstaben
    if not component_name[0].isupper():
        continue
    if not is_used_in_project(component_name, f, file_contents):
        rel = os.path.relpath(f, project_root)
        unused.append(rel)

if unused:
    context = "[Razor Usage Check] Nicht eingebundene Komponenten:\n{}".format(
        "\n".join("  ✗ " + u for u in sorted(unused))
    )
    print(
        json.dumps(
            {
                "hookSpecificOutput": {
                    "hookEventName": "PostToolUse",
                    "additionalContext": context,
                }
            }
        )
    )

"""
Enum-Coverage-Check für C#-Quellcode.
Prüft für jeden public/internal enum in der Solution, ob alle Werte in Testdateien vorkommen.

- Wird keine Testdatei gefunden, die den enum-Typ referenziert:
  → Fehler: Keine Tests für diesen Enum-Typ gefunden.
- Wird eine Testdatei gefunden:
  → Alle Enum-Werte müssen in mindestens einer Testdatei vorkommen.

Testprojekte werden anhand des Verzeichnisnamens erkannt (Suffix "Test" oder "Tests").
Private Enums werden nicht geprüft.
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
if not file.endswith(".cs") or not os.path.isfile(file):
    sys.exit(0)

SKIP_DIRS = {"obj", "bin", ".git", "node_modules", ".vs", ".idea"}


def find_solution_root(start_path):
    current = os.path.dirname(os.path.abspath(start_path))
    while True:
        for name in os.listdir(current):
            if name.endswith(".sln"):
                return current
        parent = os.path.dirname(current)
        if parent == current:
            return os.path.dirname(os.path.abspath(start_path))
        current = parent


def is_test_path(path, root):
    rel = os.path.relpath(path, root)
    parts = rel.replace("\\", "/").split("/")
    return any(
        p.lower().endswith("test") or p.lower().endswith("tests")
        for p in parts
    )


def collect_cs_files(root):
    source_files = []
    test_files = []
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        test_dir = is_test_path(dirpath, root)
        for fname in filenames:
            if not fname.endswith(".cs"):
                continue
            full_path = os.path.join(dirpath, fname)
            if test_dir:
                test_files.append(full_path)
            else:
                source_files.append(full_path)
    return source_files, test_files


def parse_enums(filepath):
    """Returns list of (enum_name, [values]) — only public/internal enums."""
    try:
        with open(filepath, encoding="utf-8", errors="replace") as f:
            content = f.read()
    except OSError:
        return []

    # Strip comments before parsing
    content = re.sub(r"//[^\n]*", "", content)
    content = re.sub(r"/\*.*?\*/", "", content, flags=re.DOTALL)

    enums = []
    # Only public or internal enums; skip private
    pattern = re.compile(
        r'\b(?:public|internal)(?:\s+\w+)*\s+enum\s+(\w+)\s*(?::\s*[\w.]+)?\s*\{([^}]*)\}',
        re.DOTALL,
    )
    for match in pattern.finditer(content):
        enum_name = match.group(1)
        body = match.group(2)
        values = []
        for part in body.split(","):
            part = re.sub(r"\[.*?\]", "", part).strip()  # strip [Attribute]
            value_match = re.match(r"^(\w+)", part)
            if value_match:
                values.append(value_match.group(1))
        if values:
            enums.append((enum_name, values))
    return enums


solution_root = find_solution_root(file)
source_files, test_files = collect_cs_files(solution_root)

# Nothing to check if the solution has no test projects yet
if not test_files:
    sys.exit(0)

# Read all test file contents once
test_contents = {}
for tf in test_files:
    try:
        with open(tf, encoding="utf-8", errors="replace") as f:
            test_contents[tf] = f.read()
    except OSError:
        pass

# Collect all enums from source files
all_enums = []
for src_file in source_files:
    for enum_name, values in parse_enums(src_file):
        all_enums.append((enum_name, values, src_file))

errors = []
for enum_name, values, src_file in all_enums:
    relevant_tests = [
        tf for tf, content in test_contents.items()
        if enum_name in content
    ]

    if not relevant_tests:
        rel_src = os.path.relpath(src_file, solution_root)
        errors.append(
            "  ✗ Keine Tests für {} gefunden (definiert in {})".format(
                enum_name, rel_src
            )
        )
        continue

    missing = [v for v in values if not any(v in test_contents[tf] for tf in relevant_tests)]
    if missing:
        rel_src = os.path.relpath(src_file, solution_root)
        errors.append(
            "  ✗ {}: Enum-Werte nicht in Tests abgedeckt: {} ({})".format(
                enum_name, ", ".join(missing), rel_src
            )
        )

if errors:
    msg = (
        "[Enum-Coverage-Check] Unvollständige Enum-Abdeckung:\n{}\n"
        "  Alle public/internal Enum-Werte müssen in mindestens einer Testdatei vorkommen."
    ).format("\n".join(errors))
    print(msg, file=sys.stderr)
    sys.exit(2)

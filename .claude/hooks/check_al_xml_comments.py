"""
AL XML Comment Check
Prüft, ob alle procedure-Deklarationen in einer .al-Datei einen /// <summary>-Kommentar haben.
EventSubscriber-Attribute vor der Deklaration werden berücksichtigt.
"""
import sys
import json
import re
import os

data = json.load(sys.stdin)
file = (
    data.get("tool_input", {}).get("file_path")
    or data.get("tool_response", {}).get("filePath")
    or ""
)
if not file.endswith(".al") or not os.path.isfile(file):
    sys.exit(0)

with open(file, encoding="utf-8") as f:
    lines = f.readlines()

PROC_PATTERN = re.compile(
    r"^\s*(local\s+|internal\s+|protected\s+)?(procedure)\s+\w+",
    re.IGNORECASE,
)


def has_summary_before(lines, proc_index):
    """
    Sucht rückwärts ab proc_index nach einem /// <summary>-Kommentar.
    Übersprungen werden: Leerzeilen, Attributzeilen [...] und andere ///-Zeilen.
    """
    j = proc_index - 1
    while j >= 0:
        stripped = lines[j].strip()
        if stripped == "":
            j -= 1
            continue
        if stripped.startswith("["):
            j -= 1
            continue
        if stripped.startswith("///"):
            if "<summary>" in stripped:
                return True
            j -= 1
            continue
        # Erste nicht passende Zeile: kein Kommentar gefunden
        break
    return False


hits = []
for i, line in enumerate(lines):
    stripped = line.strip()
    # Auskommentierte Zeilen ignorieren
    if stripped.startswith("//"):
        continue
    if not PROC_PATTERN.match(line):
        continue
    if has_summary_before(lines, i):
        continue
    name_match = re.search(r"procedure\s+(\w+)", line, re.IGNORECASE)
    name = name_match.group(1) if name_match else "?"
    hits.append("  Line {}: {}".format(i + 1, name))

if hits:
    context = "[AL XML Comment Check] Missing <summary> in {}:\n{}".format(
        os.path.basename(file), "\n".join(hits)
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

"""
EF Core EntityState Assignment Check
Prüft, ob in C#-Dateien EntityState manuell gesetzt wird.

Flaggt: .State = EntityState.<anything>
Erlaubt: Lesezugriffe (==, !=, switch, is) und Kommentare.

Hintergrund: Manuelles Setzen von EntityState umgeht EF-Change-Tracking
und führt zu schwer nachvollziehbaren Fehlern. Stattdessen den
Load-and-Patch-Ansatz verwenden (siehe EF-Skill).
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
if not file.endswith(".cs") or not os.path.isfile(file):
    sys.exit(0)

with open(file, encoding="utf-8") as f:
    lines = f.readlines()

# Zuweisung: .State = EntityState.Xyz
# Erlaubt sind Vergleiche (==, !=) und andere Nicht-Zuweisungen
assign_pattern = re.compile(r"\.State\s*=[^=].*EntityState\.|EntityState\.\w+.*\.State\s*=[^=]")

hits = []
for i, line in enumerate(lines, 1):
    stripped = line.strip()
    if stripped.startswith("//") or stripped.startswith("*"):
        continue
    # Inline-Kommentar entfernen, bevor geprüft wird
    code = re.sub(r"//.*$", "", line)
    if assign_pattern.search(code):
        hits.append("  Line {}: {}".format(i, stripped[:120]))

if hits:
    context = (
        "[EF EntityState Check] Manuelles Setzen von EntityState in {}:\n{}\n"
        "  → Stattdessen Load-and-Patch verwenden: Entität laden, Eigenschaften setzen, SaveChangesAsync aufrufen."
    ).format(file, "\n".join(hits))
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

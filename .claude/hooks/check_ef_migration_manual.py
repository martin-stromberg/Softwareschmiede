"""
EF Core Migration Manual-Creation Check
Prüft, ob eine EF-Migrationsdatei per Write-Tool handschriftlich angelegt
wurde, statt sie über die EF-CLI zu generieren.

Nur der Write-Tool-Aufruf wird geprüft (komplette Neuerstellung einer
Datei) - Edits an bereits vorhandenen, per `dotnet ef migrations add`
generierten Migrationen (z.B. Anpassung von Up/Down für Datensicherheit)
sind weiterhin erlaubt.

Hintergrund: Handschriftlich erstellte Migrationen erzeugen fast nie die
zugehörige .Designer.cs-Datei und aktualisieren nicht den ModelSnapshot.
Das führt zu inkonsistentem Migrationsverlauf und Laufzeitfehlern beim
Anwenden der Migration. Migrationen müssen daher immer über
`dotnet ef migrations add` generiert werden (siehe EF-Skill).
"""
import sys
import json
import os
import re

data = json.load(sys.stdin)

if data.get("tool_name") != "Write":
    sys.exit(0)

file = (
    data.get("tool_input", {}).get("file_path")
    or data.get("tool_response", {}).get("filePath")
    or ""
)
if not file.endswith(".cs") or not os.path.isfile(file):
    sys.exit(0)

filename = os.path.basename(file)

# Designer- und Snapshot-Dateien werden von der EF-CLI mitgeneriert;
# nur die eigentliche Migrationsdatei interessiert hier.
if filename.endswith(".Designer.cs") or filename.endswith("ModelSnapshot.cs"):
    sys.exit(0)

normalized = file.replace("\\", "/")
if "/Migrations/" not in normalized and not normalized.startswith("Migrations/"):
    sys.exit(0)

# Namensschema von `dotnet ef migrations add`: <14-stelliger Timestamp>_<Name>.cs
if not re.match(r"^\d{14}_.+\.cs$", filename):
    sys.exit(0)

with open(file, encoding="utf-8", errors="replace") as f:
    content = f.read()

if not re.search(r":\s*Migration\b", content):
    sys.exit(0)

base = filename[:-3]
designer = os.path.join(os.path.dirname(file), base + ".Designer.cs")

msg = (
    "[EF Migration Check] Migrationsdatei wurde per Write-Tool handschriftlich "
    "erstellt statt über die EF-CLI generiert: {}\n"
    "  → Dabei fehlt fast immer die zugehörige .Designer.cs-Datei "
    "(hier: {}) und der ModelSnapshot wird nicht aktualisiert.\n"
    "  → Diese Datei löschen und stattdessen ausführen:\n"
    "      dotnet ef migrations add <Name> --project <Infrastructure-Projekt> "
    "--startup-project <Startup-Projekt>\n"
    "  → Das generierte Up/Down darf danach per Edit für Datensicherheit "
    "angepasst werden."
).format(file, "fehlt" if not os.path.isfile(designer) else "vorhanden")
print(msg, file=sys.stderr)
sys.exit(2)

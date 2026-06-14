"""
XML-Dokumentations-Check für .csproj- und .cs-Dateien.
.csproj: Prüft GenerateDocumentationFile, WarningsAsErrors, NoWarn usw.
.cs:     Verbietet #pragma warning disable für XML-Doc-Warncodes.
"""
import sys
import json
import os
import re
import xml.etree.ElementTree as ET

# Alle C#-Warncodes für XML-Dokumentation
XML_DOC_CODES = {
    "CS1591",  # Missing XML comment for publicly visible type or member
    "CS1572",  # XML comment has a param tag for a parameter that does not exist
    "CS1573",  # Parameter has no matching param tag in the XML comment
    "CS1574",  # XML comment has a cref attribute that could not be resolved
    "CS1580",  # Invalid type for parameter in XML comment cref attribute
    "CS1581",  # Invalid return type in XML comment cref attribute
    "CS1584",  # XML comment has syntactically incorrect cref attribute
    "CS1587",  # XML comment is not placed on a valid language element
    "CS1589",  # Unable to include XML fragment
    "CS1590",  # Invalid XML include element
    "CS1592",  # Badly formed XML in included comments
    "CS1598",  # XML comment file could not be opened
}


def parse_codes(text):
    """Zerlegt einen semikolon- oder kommaseparierten Warncode-String in ein Set."""
    if not text:
        return set()
    return {c.strip().upper() for c in text.replace(";", ",").split(",") if c.strip()}


data = json.load(sys.stdin)
file = (
    data.get("tool_input", {}).get("file_path")
    or data.get("tool_response", {}).get("filePath")
    or ""
)
if not os.path.isfile(file):
    sys.exit(0)
is_csproj = file.endswith(".csproj")
is_cs = file.endswith(".cs")
if not is_csproj and not is_cs:
    sys.exit(0)

# ── .cs-Prüfung: #pragma warning disable für XML-Doc-Codes verboten ──────────
if is_cs:
    PRAGMA_RE = re.compile(r"#\s*pragma\s+warning\s+disable\b(.+)", re.IGNORECASE)
    violations = []
    try:
        with open(file, encoding="utf-8", errors="replace") as f:
            for lineno, line in enumerate(f, 1):
                m = PRAGMA_RE.search(line)
                if not m:
                    continue
                for raw in re.split(r"[,\s]+", m.group(1).strip()):
                    if raw.upper().startswith("CS"):
                        candidate = raw.upper()
                    elif raw.strip().isdigit():
                        candidate = "CS" + raw.strip()
                    else:
                        candidate = raw.upper()
                    if candidate in XML_DOC_CODES:
                        violations.append((lineno, line.rstrip(), candidate))
    except OSError:
        sys.exit(0)

    if violations:
        lines = "\n".join(
            "  ✗ Zeile {}: {} ({})".format(ln, code, src)
            for ln, src, code in violations
        )
        context = (
            "[XML-Doc-Check] #pragma warning disable für XML-Dokumentationscodes "
            "ist in Codedateien verboten:\n{}\nEntferne die Pragma-Anweisung und "
            "ergänze stattdessen die fehlenden XML-Kommentare.".format(lines)
        )
        print(json.dumps({
            "hookSpecificOutput": {
                "hookEventName": "PostToolUse",
                "additionalContext": context,
            }
        }))
    sys.exit(0)

# ── .csproj-Prüfung ───────────────────────────────────────────────────────────
try:
    tree = ET.parse(file)
    root = tree.getroot()
except ET.ParseError:
    sys.exit(0)  # Ungültige XML-Datei – nicht blockieren

generate_doc = False
treat_all_as_errors = False
all_no_warn = set()
all_warnings_as_errors = set()
all_warnings_not_as_errors = set()

for pg in root.iter("PropertyGroup"):
    node = pg.find("GenerateDocumentationFile")
    if node is not None and (node.text or "").strip().lower() == "true":
        generate_doc = True

    node = pg.find("TreatWarningsAsErrors")
    if node is not None and (node.text or "").strip().lower() == "true":
        treat_all_as_errors = True

    node = pg.find("NoWarn")
    if node is not None:
        all_no_warn |= parse_codes(node.text)

    node = pg.find("WarningsAsErrors")
    if node is not None:
        all_warnings_as_errors |= parse_codes(node.text)

    node = pg.find("WarningsNotAsErrors")
    if node is not None:
        all_warnings_not_as_errors |= parse_codes(node.text)

problems = []

# 1. GenerateDocumentationFile muss true sein
if not generate_doc:
    problems.append(
        "<GenerateDocumentationFile>true</GenerateDocumentationFile> fehlt oder ist nicht auf true gesetzt"
    )

# 2. Kein XML-Doc-Code darf in NoWarn unterdrückt sein
suppressed_in_no_warn = XML_DOC_CODES & all_no_warn
if suppressed_in_no_warn:
    problems.append(
        "XML-Dokumentationswarnungen in <NoWarn> unterdrückt: "
        + ", ".join(sorted(suppressed_in_no_warn))
    )

# 3. Kein XML-Doc-Code darf in WarningsNotAsErrors herabgestuft sein
downgraded = XML_DOC_CODES & all_warnings_not_as_errors
if downgraded:
    problems.append(
        "XML-Dokumentationswarnungen in <WarningsNotAsErrors> herabgestuft: "
        + ", ".join(sorted(downgraded))
    )

# 4. CS1591 muss als Fehler konfiguriert sein:
#    Entweder TreatWarningsAsErrors=true (und CS1591 nicht in WarningsNotAsErrors)
#    oder CS1591 explizit in WarningsAsErrors
cs1591_via_treat = treat_all_as_errors and "CS1591" not in all_warnings_not_as_errors
cs1591_explicit = "CS1591" in all_warnings_as_errors
if not cs1591_via_treat and not cs1591_explicit:
    problems.append(
        "CS1591 ist nicht als Fehler konfiguriert – "
        "<WarningsAsErrors>CS1591</WarningsAsErrors> oder "
        "<TreatWarningsAsErrors>true</TreatWarningsAsErrors> fehlt"
    )

if problems:
    context = "[csproj XML-Doc-Check] Probleme in {}:\n{}".format(
        os.path.basename(file),
        "\n".join("  ✗ " + p for p in problems),
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

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


def find_nearest_csproj(start_dir):
    """Sucht die nächstgelegene .csproj-Datei im Verzeichnisbaum aufwärts."""
    current = start_dir
    while True:
        for entry in os.scandir(current):
            if entry.name.endswith(".csproj") and entry.is_file():
                return entry.path
        parent = os.path.dirname(current)
        if parent == current:
            return None
        current = parent


def check_csproj_for_xmldoc(csproj_path):
    """Gibt eine Liste von Problemen zurück (leer = alles OK)."""
    try:
        tree = ET.parse(csproj_path)
        root = tree.getroot()
    except ET.ParseError:
        return []

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
    if not generate_doc:
        problems.append(
            "<GenerateDocumentationFile>true</GenerateDocumentationFile> fehlt oder ist nicht auf true gesetzt"
        )
    suppressed_in_no_warn = XML_DOC_CODES & all_no_warn
    if suppressed_in_no_warn:
        problems.append(
            "XML-Dokumentationswarnungen in <NoWarn> unterdrückt: "
            + ", ".join(sorted(suppressed_in_no_warn))
        )
    downgraded = XML_DOC_CODES & all_warnings_not_as_errors
    if downgraded:
        problems.append(
            "XML-Dokumentationswarnungen in <WarningsNotAsErrors> herabgestuft: "
            + ", ".join(sorted(downgraded))
        )
    cs1591_via_treat = treat_all_as_errors and "CS1591" not in all_warnings_not_as_errors
    cs1591_explicit = "CS1591" in all_warnings_as_errors
    if not cs1591_via_treat and not cs1591_explicit:
        problems.append(
            "CS1591 ist nicht als Fehler konfiguriert – "
            "<WarningsAsErrors>CS1591</WarningsAsErrors> oder "
            "<TreatWarningsAsErrors>true</TreatWarningsAsErrors> fehlt"
        )
    return problems


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

    csproj_problems = []
    csproj_path = find_nearest_csproj(os.path.dirname(os.path.abspath(file)))
    if csproj_path:
        csproj_problems = check_csproj_for_xmldoc(csproj_path)

    messages = []
    if violations:
        pragma_lines = "\n".join(
            "  ✗ Zeile {}: {} ({})".format(ln, code, src)
            for ln, src, code in violations
        )
        messages.append(
            "[XML-Doc-Check] #pragma warning disable für XML-Dokumentationscodes "
            "ist in Codedateien verboten:\n{}\nEntferne die Pragma-Anweisung und "
            "ergänze stattdessen die fehlenden XML-Kommentare.".format(pragma_lines)
        )
    if csproj_problems:
        proj_name = os.path.basename(csproj_path) if csproj_path else "unbekannt"
        messages.append(
            "[csproj XML-Doc-Check] Zugehörige Projektdatei {} hat Probleme:\n{}".format(
                proj_name,
                "\n".join("  ✗ " + p for p in csproj_problems),
            )
        )

    if messages:
        print(json.dumps({
            "hookSpecificOutput": {
                "hookEventName": "PostToolUse",
                "additionalContext": "\n\n".join(messages),
            }
        }))
    sys.exit(0)

# ── .csproj-Prüfung ───────────────────────────────────────────────────────────
problems = check_csproj_for_xmldoc(file)
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

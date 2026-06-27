"""
XML-Dokumentations-Check für .csproj- und .cs-Dateien.
.csproj: Prüft GenerateDocumentationFile, WarningsAsErrors, NoWarn usw.
.cs:     Verbietet #pragma warning disable für XML-Doc-Warncodes.
         Prüft Vollständigkeit: <param>, <typeparam>, <returns>, <response>.
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

DOC_PARAM_RE = re.compile(r'<param\s+name=["\'](\w+)["\']')
DOC_TYPEPARAM_RE = re.compile(r'<typeparam\s+name=["\'](\w+)["\']')
DOC_RETURNS_RE = re.compile(r'<(?:returns|value)\b')
DOC_RESPONSE_RE = re.compile(r'<response\s+code=["\'](\d+)["\']')
HTTP_METHOD_ATTR_RE = re.compile(
    r'\[(?:Http(?:Get|Post|Put|Delete|Patch|Head|Options)|Route)\b', re.IGNORECASE
)
PRODUCES_RESPONSE_ATTR_RE = re.compile(r'\[ProducesResponseType\b', re.IGNORECASE)
MODIFIER_RE = re.compile(
    r'\b(?:public|private|protected|internal|static|virtual|override|'
    r'abstract|async|sealed|extern|partial|new|readonly)\s+'
)


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


# ── Hilfsfunktionen für Vollständigkeitsprüfung ───────────────────────────────

def _simplify_generics(text):
    """Ersetzt verschachtelte <...> iterativ durch <> für einfacheres Parsing."""
    prev = None
    while prev != text:
        prev = text
        text = re.sub(r'<[^<>]*>', '<>', text)
    return text


def extract_param_names(decl):
    """Extrahiert Parameternamen aus einer Methoden- oder Konstruktordeklaration."""
    simplified = _simplify_generics(decl)
    m = re.search(r'\((.+)\)', simplified, re.DOTALL)
    if not m:
        return []
    params_str = m.group(1)
    # Entferne Attribute wie [FromBody], [FromRoute]
    params_str = re.sub(r'\[[^\]]*\]', '', params_str)
    params_str = _simplify_generics(params_str)

    names = []
    for part in params_str.split(','):
        part = part.strip()
        if not part:
            continue
        # Standardwert abschneiden
        part = re.split(r'\s*=\s*', part)[0].strip()
        # ref/out/in/params entfernen
        part = re.sub(r'\b(?:ref|out|in|params)\s+', '', part).strip()
        words = part.split()
        if words:
            name = words[-1].strip('*&')
            if name and re.match(r'^@?[a-zA-Z_]\w*$', name):
                names.append(name.lstrip('@'))
    return names


def extract_type_param_names(decl):
    """
    Extrahiert generische Typparameternamen (T, TResult usw.) aus der Deklaration.
    Nur eigene Typparameter des Members (MethodName<T>), nicht Typargumente im Rückgabetyp.
    """
    # Nur den Teil vor der Parameterliste betrachten
    before_paren = decl.split('(')[0] if '(' in decl else decl
    stripped = MODIFIER_RE.sub('', before_paren).strip()
    # Typparameter stehen direkt am Methodennamen, am Ende des Teils vor (:
    #   Task<IActionResult> GetUser<TFilter>  →  nur TFilter gehört zum Member
    m = re.search(r'\w+\s*<([^<>]+)>\s*$', stripped)
    if not m:
        return []
    names = []
    for tp in m.group(1).split(','):
        tp = tp.strip()
        if tp and re.match(r'^[A-Z]\w*$', tp):
            names.append(tp)
    return names


def get_return_type(decl):
    """
    Gibt den Rückgabetyp zurück oder None wenn nicht ermittelbar
    (z.B. Konstruktor oder keine erkennbaren zwei Token vor der Parameterliste).
    """
    stripped = MODIFIER_RE.sub('', decl).strip()
    simplified = _simplify_generics(stripped)
    before_paren = simplified.split('(')[0].strip() if '(' in simplified else ''
    if not before_paren:
        return None
    tokens = before_paren.split()
    if len(tokens) < 2:
        return None  # Konstruktor oder einzelner Token
    # Letztes Token = Methodenname, alles davor = Rückgabetyp
    return ' '.join(tokens[:-1])


def is_non_void_return(decl):
    """True wenn die Methode einen dokumentationswürdigen Rückgabewert hat."""
    ret = get_return_type(decl)
    if not ret:
        return False
    ret = ret.strip()
    # void und bare Task/ValueTask haben keinen dokumentationswürdigen Rückgabewert
    return ret not in ('void', 'Task', 'ValueTask')


def member_display_name(decl):
    """Kurzer lesbarer Name des Members aus der Deklaration."""
    m = re.search(r'(\w+)\s*(?:<[^>]*>)?\s*\(', decl)
    if m:
        return m.group(1)
    words = decl.split()
    return words[-1] if words else decl[:40]


def extract_produces_response_codes(attr_lines):
    """Extrahiert HTTP-Statuscodes aus [ProducesResponseType(...)]-Attributen."""
    codes = set()
    for attr in attr_lines:
        if not PRODUCES_RESPONSE_ATTR_RE.search(attr):
            continue
        # StatusCodes.Status200OK → 200
        for m in re.finditer(r'Status(\d{3})\w*', attr):
            codes.add(m.group(1))
        # Bare 3-stellige Zahl: (200) oder , 404)
        for m in re.finditer(r'(?<!\d)(\d{3})(?!\d)', attr):
            codes.add(m.group(1))
    return codes


def parse_documented_members(content):
    """
    Parst .cs-Datei und gibt eine Liste von Dicts zurück:
    {'doc': str, 'attrs': [str], 'decl': str, 'line': int, 'name': str}
    """
    lines = content.splitlines()
    members = []
    i = 0
    n = len(lines)

    while i < n:
        if not lines[i].strip().startswith('///'):
            i += 1
            continue

        doc_start = i + 1  # 1-basiert
        doc_lines = []
        while i < n and lines[i].strip().startswith('///'):
            doc_lines.append(lines[i].strip())
            i += 1

        # Leerzeilen überspringen
        while i < n and not lines[i].strip():
            i += 1

        # Attributzeilen sammeln (beginnen mit [)
        attr_lines = []
        while i < n:
            s = lines[i].strip()
            if not s:
                i += 1
                continue
            if s.startswith('['):
                attr_lines.append(s)
                i += 1
            else:
                break

        # Deklaration sammeln (ggf. mehrzeilig bei vielen Parametern)
        decl_lines = []
        paren_depth = 0
        found_paren = False
        limit = min(i + 15, n)
        j = i
        while j < limit:
            line = lines[j].strip()
            if not line or line.startswith('//'):
                break
            decl_lines.append(line)
            open_p = line.count('(')
            close_p = line.count(')')
            paren_depth += open_p - close_p
            if open_p > 0:
                found_paren = True
            j += 1
            if found_paren and paren_depth <= 0:
                break
            if not found_paren and ('{' in line or ';' in line or '=>' in line):
                break
        i = j

        if doc_lines and decl_lines:
            decl_text = ' '.join(decl_lines)
            members.append({
                'doc': '\n'.join(doc_lines),
                'attrs': attr_lines,
                'decl': decl_text,
                'line': doc_start,
                'name': member_display_name(decl_text),
            })

    return members


def check_cs_xmldoc_completeness(file_path):
    """
    Prüft Vollständigkeit der XML-Kommentare in einer .cs-Datei.
    Gibt eine Liste von Problembeschreibungen zurück.
    """
    try:
        with open(file_path, encoding='utf-8', errors='replace') as f:
            content = f.read()
    except OSError:
        return []

    members = parse_documented_members(content)
    issues = []

    for member in members:
        doc = member['doc']
        attrs = member['attrs']
        decl = member['decl']
        line = member['line']
        label = f"Zeile {line} ({member['name']})"

        # Nur Member mit <summary> prüfen — ohne Summary greift CS1591 ohnehin
        if '<summary' not in doc:
            continue

        # ── 1. <param>-Tags ────────────────────────────────────────────────
        param_names = extract_param_names(decl)
        documented_params = set(DOC_PARAM_RE.findall(doc))
        missing_params = [p for p in param_names if p not in documented_params]
        if missing_params:
            issues.append(
                f"  {label}: fehlende <param>-Tags für: {', '.join(missing_params)}"
            )

        # ── 2. <typeparam>-Tags ────────────────────────────────────────────
        type_params = extract_type_param_names(decl)
        documented_type_params = set(DOC_TYPEPARAM_RE.findall(doc))
        missing_type_params = [tp for tp in type_params if tp not in documented_type_params]
        if missing_type_params:
            issues.append(
                f"  {label}: fehlende <typeparam>-Tags für: {', '.join(missing_type_params)}"
            )

        # ── 3. <returns> für Methoden mit Rückgabewert ─────────────────────
        if '(' in decl and is_non_void_return(decl) and not DOC_RETURNS_RE.search(doc):
            issues.append(f"  {label}: fehlendes <returns>-Tag")

        # ── 4. <response>-Tags für HTTP-Controller-Aktionen ────────────────
        is_http_action = any(HTTP_METHOD_ATTR_RE.search(a) for a in attrs)
        if is_http_action:
            expected_codes = extract_produces_response_codes(attrs)
            documented_codes = set(DOC_RESPONSE_RE.findall(doc))
            missing_codes = expected_codes - documented_codes
            if missing_codes:
                issues.append(
                    f"  {label}: fehlende <response code=\"...\">-Tags für "
                    f"HTTP-Statuscodes: {', '.join(sorted(missing_codes))}"
                )

    return issues


# ── Hauptlogik ────────────────────────────────────────────────────────────────

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

# ── .cs-Prüfung ───────────────────────────────────────────────────────────────
if is_cs:
    # 1. #pragma warning disable für XML-Doc-Codes verboten
    PRAGMA_RE = re.compile(r"#\s*pragma\s+warning\s+disable\b(.+)", re.IGNORECASE)
    pragma_violations = []
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
                        pragma_violations.append((lineno, line.rstrip(), candidate))
    except OSError:
        sys.exit(0)

    # 2. Vollständigkeit der XML-Kommentare
    completeness_issues = check_cs_xmldoc_completeness(file)

    # 3. Zugehörige .csproj prüfen
    csproj_problems = []
    csproj_path = find_nearest_csproj(os.path.dirname(os.path.abspath(file)))
    if csproj_path:
        csproj_problems = check_csproj_for_xmldoc(csproj_path)

    messages = []
    if pragma_violations:
        pragma_lines = "\n".join(
            "  ✗ Zeile {}: {} ({})".format(ln, code, src)
            for ln, src, code in pragma_violations
        )
        messages.append(
            "[XML-Doc-Check] #pragma warning disable für XML-Dokumentationscodes "
            "ist in Codedateien verboten:\n{}\nEntferne die Pragma-Anweisung und "
            "ergänze stattdessen die fehlenden XML-Kommentare.".format(pragma_lines)
        )
    if completeness_issues:
        messages.append(
            "[XML-Doc-Check] Unvollständige XML-Dokumentation in {}:\n"
            "Jeder dokumentierte Member braucht vollständige Tags "
            "(<param> für alle Parameter, <returns> bei Rückgabewert, "
            "<typeparam> für Typparameter, <response> für HTTP-Statuscodes).\n"
            "Fehlende Tags:\n{}".format(
                os.path.basename(file),
                "\n".join("  ✗ " + issue.strip() for issue in completeness_issues),
            )
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

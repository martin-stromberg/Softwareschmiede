"""
NotImplementedException-Check für C#-Dateien.

Verbietet:
  1. NotImplementedException an beliebiger Stelle im Code.
  2. Methoden, Konstruktoren, Property-Getter/-Setter, deren gesamter Body
     nur aus einem einzigen throw-Statement besteht (Platzhalter-Stubs),
     unabhängig vom geworfenen Exception-Typ.

Hintergrund: Solche Stubs bleiben oft unbemerkt liegen und führen zu
Laufzeitfehlern statt Compile-Zeit-Fehlern. Methoden sollen entweder
vollständig implementiert oder gar nicht erst angelegt werden.
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

with open(file, encoding="utf-8", errors="replace") as f:
    content = f.read()


def strip_comments_and_strings(text):
    """Ersetzt Kommentare und String-/Char-Literale durch Leerzeichen,
    behält Zeilenumbrüche bei, damit Zeilennummern erhalten bleiben."""
    out = []
    i = 0
    n = len(text)
    while i < n:
        two = text[i:i + 2]
        if two == "//":
            j = text.find("\n", i)
            if j == -1:
                j = n
            out.append(" " * (j - i))
            i = j
        elif two == "/*":
            j = text.find("*/", i + 2)
            j = n if j == -1 else j + 2
            out.append(re.sub(r"[^\n]", " ", text[i:j]))
            i = j
        elif text[i] in ('"', "'"):
            quote = text[i]
            j = i + 1
            while j < n and text[j] != quote:
                j += 2 if text[j] == "\\" and j + 1 < n else 1
            j = min(j + 1, n)
            out.append(re.sub(r"[^\n]", " ", text[i:j]))
            i = j
        else:
            out.append(text[i])
            i += 1
    return "".join(out)


clean = strip_comments_and_strings(content)
source_lines = content.splitlines()


def line_of(pos):
    return clean.count("\n", 0, pos) + 1


def source_line_text(ln):
    return source_lines[ln - 1].strip() if 0 < ln <= len(source_lines) else ""


found = {}  # line -> message, dedupe

# 1. NotImplementedException überall verboten
for m in re.finditer(r"\bNotImplementedException\b", clean):
    ln = line_of(m.start())
    found[ln] = f"  Zeile {ln}: NotImplementedException verwendet — {source_line_text(ln)[:120]}"

MODIFIER = (
    r"(?:public|private|protected|internal|static|virtual|override|"
    r"abstract|async|sealed|extern|new|readonly)"
)

# 2a. Methoden/Konstruktoren mit Block-Body, der nur aus einem throw besteht
# ("=" bleibt zugelassen, damit Default-Parameter (z.B. "int x = 5") die
# Signatur nicht von der Prüfung ausschließen; ";", "{", "}" markieren
# weiterhin die Statement-Grenzen und verhindern ein Überschreiten in
# unabhängigen Code.
stub_block_re = re.compile(
    rf"{MODIFIER}[^{{}};]*\)\s*\{{\s*throw\s+new\s+\w+\s*\([^;]*\)\s*;\s*\}}"
)
for m in stub_block_re.finditer(clean):
    ln = line_of(m.start())
    found.setdefault(ln, f"  Zeile {ln}: Methode besteht nur aus einem throw-Statement (Stub)")

# 2b. Expression-bodied Methoden/Properties, die nur werfen
stub_expr_re = re.compile(
    rf"{MODIFIER}[^{{}};]*=>\s*throw\s+new\s+\w+\s*\([^;]*\)\s*;"
)
for m in stub_expr_re.finditer(clean):
    ln = line_of(m.start())
    found.setdefault(ln, f"  Zeile {ln}: Expression-bodied Member besteht nur aus einem throw-Statement (Stub)")

# 2c. get/set/init-Accessoren, deren Body nur aus einem throw besteht
accessor_expr_re = re.compile(r"\b(?:get|set|init)\s*=>\s*throw\s+new\s+\w+\s*\([^;]*\)\s*;")
accessor_block_re = re.compile(r"\b(?:get|set|init)\s*\{\s*throw\s+new\s+\w+\s*\([^;]*\)\s*;\s*\}")
for pattern in (accessor_expr_re, accessor_block_re):
    for m in pattern.finditer(clean):
        ln = line_of(m.start())
        found.setdefault(ln, f"  Zeile {ln}: Accessor besteht nur aus einem throw-Statement (Stub)")

if found:
    ordered = [found[ln] for ln in sorted(found)]
    msg = (
        "[NotImplementedException-Check] Verbotene Platzhalter-Implementierung in {}:\n{}\n"
        "  → NotImplementedException ist nicht erlaubt, und Methoden/Properties/Accessoren\n"
        "    dürfen nicht nur aus einem throw-Statement bestehen. Vollständig implementieren\n"
        "    oder das Member noch nicht anlegen."
    ).format(os.path.relpath(file), "\n".join(ordered))
    print(msg, file=sys.stderr)
    sys.exit(2)

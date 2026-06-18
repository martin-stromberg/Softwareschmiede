#!/usr/bin/env python3
"""
PostToolUse-Hook: Zwei CSS-Prüfungen nach dem Schreiben/Bearbeiten einer Datei:

1. Sind alle sz-*-Klassen in geänderten .razor-Dateien in CSS definiert?
2. Sind alle globalen CSS-Dateien (wwwroot/, kein lib/) mindestens einmal
   per <link> referenziert? Sind alle .razor.css-Scoped-Dateien nicht verwaist?
"""
import sys
import json
import re
import os
import glob


def main():
    try:
        data = json.load(sys.stdin)
    except Exception:
        return

    file_path = data.get('tool_input', {}).get('file_path', '')

    project_root = find_project_root(file_path)
    if not project_root:
        return

    warnings = []

    # Prüfung 1: sz-*-Klassen in geänderten .razor-Dateien
    warnings += check_undefined_sz_classes(file_path, project_root)

    # Prüfung 2: Ungenutzte CSS-Dateien (nur bei CSS- oder Razor-Änderungen)
    normalized = file_path.replace('\\', '/')
    if normalized.endswith('.css') or normalized.endswith('.razor') or normalized.endswith('.html'):
        warnings += check_unused_css_files(project_root)

    if warnings:
        message = "\n\n".join(warnings)
        print(json.dumps({
            "hookSpecificOutput": {
                "hookEventName": "PostToolUse",
                "additionalContext": message
            }
        }))


# ---------------------------------------------------------------------------
# Prüfung 1: sz-*-Klassen definiert?
# ---------------------------------------------------------------------------

def check_undefined_sz_classes(file_path: str, project_root: str) -> list[str]:
    normalized = file_path.replace('\\', '/')

    if not normalized.endswith('.razor') or normalized.endswith('.razor.css'):
        return []
    if '/Components/' not in normalized and '/Pages/' not in normalized:
        return []

    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
    except Exception:
        return []

    sz_classes = set()
    for match in re.finditer(r'class="([^"]*)"', content):
        for cls in match.group(1).split():
            if re.match(r'^sz-[a-z]', cls):
                sz_classes.add(cls)

    if not sz_classes:
        return []

    css_content = load_all_css(project_root)
    missing = [
        cls for cls in sorted(sz_classes)
        if not re.search(r'\.' + re.escape(cls) + r'[\s{:,\[\.]', css_content)
    ]

    if not missing:
        return []

    file_name = os.path.basename(file_path)
    lines = [f"  • .{cls}" for cls in missing]
    return [
        f"⚠️  Fehlende CSS-Definitionen in {file_name}:\n"
        + "\n".join(lines)
        + "\n→ Bitte in app.css oder einer .razor.css-Datei ergänzen (siehe CLAUDE.md)."
    ]


# ---------------------------------------------------------------------------
# Prüfung 2: Ungenutzte CSS-Dateien
# ---------------------------------------------------------------------------

def check_unused_css_files(project_root: str) -> list[str]:
    warnings = []

    wwwroot = os.path.join(project_root, 'src', 'Schnittstellenzentrale', 'wwwroot')
    components = os.path.join(project_root, 'src', 'Schnittstellenzentrale', 'Components')

    # Alle Razor- und HTML-Inhalte für Referenzsuche zusammenfassen
    razor_content = _read_all(glob.glob(os.path.join(components, '**', '*.razor'), recursive=True))
    razor_content += _read_all(glob.glob(os.path.join(wwwroot, '**', '*.html'), recursive=True))

    # --- Globale CSS-Dateien in wwwroot/ (außer lib/) ---
    for css_file in glob.glob(os.path.join(wwwroot, '**', '*.css'), recursive=True):
        norm = css_file.replace('\\', '/')
        if '/lib/' in norm:
            continue                          # Bibliotheksdateien ignorieren

        rel = os.path.relpath(css_file, wwwroot).replace('\\', '/')
        basename = os.path.basename(css_file)

        # .styles.css wird von Blazor automatisch eingebunden – kein expliziter Link nötig
        if basename.endswith('.styles.css'):
            continue

        if rel not in razor_content and basename not in razor_content:
            warnings.append(
                f"🗑️  Ungenutzte CSS-Datei: wwwroot/{rel}\n"
                "   → Nicht per <link> referenziert. Datei entfernen oder in App.razor einbinden."
            )

    # --- Verwaiste .razor.css-Scoped-Dateien ---
    for razor_css in glob.glob(os.path.join(components, '**', '*.razor.css'), recursive=True):
        # Zugehörige .razor-Datei ermitteln (gleicher Pfad ohne .css)
        expected_razor = razor_css[:-4]  # entfernt .css → bleibt .razor
        if not os.path.exists(expected_razor):
            rel = os.path.relpath(razor_css, project_root).replace('\\', '/')
            warnings.append(
                f"🗑️  Verwaiste Scoped-CSS-Datei: {rel}\n"
                f"   → Keine zugehörige Razor-Komponente gefunden: {os.path.basename(expected_razor)}"
            )

    return warnings


# ---------------------------------------------------------------------------
# Hilfsfunktionen
# ---------------------------------------------------------------------------

def find_project_root(start_path: str) -> str | None:
    current = os.path.dirname(os.path.abspath(start_path)) if start_path else os.getcwd()
    while current and current != os.path.dirname(current):
        if os.path.exists(os.path.join(current, 'src', 'Schnittstellenzentrale', 'wwwroot', 'app.css')):
            return current
        current = os.path.dirname(current)
    return None


def load_all_css(project_root: str) -> str:
    parts = []
    app_css = os.path.join(project_root, 'src', 'Schnittstellenzentrale', 'wwwroot', 'app.css')
    if os.path.exists(app_css):
        with open(app_css, 'r', encoding='utf-8') as f:
            parts.append(f.read())
    pattern = os.path.join(project_root, 'src', 'Schnittstellenzentrale', 'Components', '**', '*.razor.css')
    for css_file in glob.glob(pattern, recursive=True):
        try:
            with open(css_file, 'r', encoding='utf-8') as f:
                parts.append(f.read())
        except Exception:
            pass
    return '\n'.join(parts)


def _read_all(paths: list[str]) -> str:
    parts = []
    for p in paths:
        try:
            with open(p, 'r', encoding='utf-8') as f:
                parts.append(f.read())
        except Exception:
            pass
    return '\n'.join(parts)


if __name__ == '__main__':
    main()

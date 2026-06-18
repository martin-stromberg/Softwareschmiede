#!/usr/bin/env python3
"""Check for hardcoded UI strings in Razor files that should be localized."""
import sys
import json
import re

LOCALIZABLE_ATTRS = {'title', 'placeholder', 'alt', 'aria-label', 'label', 'tooltip'}

def is_code_like(text):
    text = text.strip()
    if not text or text.isdigit():
        return True
    if text.startswith(('http', '/', '#', '.', '../', 'sz-', 'bi-')):
        return True
    # Looks like a CSS class list, identifier, or enum value (no spaces, no German umlauts)
    if re.match(r'^[a-z][a-zA-Z0-9_\-]*$', text):
        return True
    # Only symbols/punctuation
    if not re.search(r'[a-zA-ZäöüÄÖÜß]', text):
        return True
    return False

def check_file(filepath):
    if not filepath or not filepath.endswith('.razor'):
        return []

    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            content = f.read()
    except (OSError, IOError):
        return []

    findings = []
    lines = content.splitlines()
    in_code_block = False
    code_depth = 0

    for lineno, line in enumerate(lines, 1):
        stripped = line.strip()

        # Track @code { ... } blocks — skip them entirely
        if re.match(r'@code\s*\{', stripped):
            in_code_block = True
            code_depth = 1
            continue
        if in_code_block:
            code_depth += stripped.count('{') - stripped.count('}')
            if code_depth <= 0:
                in_code_block = False
            continue

        # Skip Razor directives and comment lines
        if re.match(r'@(page|using|inject|inherits|namespace|typeparam|model|layout|addTagHelper)\b', stripped):
            continue
        if stripped.startswith(('//', '<!--', '*', '@*')):
            continue

        # --- Check localizable attributes ---
        # Matches: attr="value"  where value has no @ or { (= not already a Razor expression)
        for m in re.finditer(r'\b([\w-]+)="([^"@{][^"]*)"', line, re.IGNORECASE):
            attr_name = m.group(1).lower()
            attr_val = m.group(2)
            if attr_name not in LOCALIZABLE_ATTRS:
                continue
            if is_code_like(attr_val):
                continue
            if not re.search(r'[a-zA-ZäöüÄÖÜß]', attr_val):
                continue
            findings.append(f'  Zeile {lineno}: {attr_name}="{attr_val}"')

        # --- Check text nodes: >some text</ ---
        # Require spaces (multi-word) to reduce false positives on single technical tokens
        for m in re.finditer(r'>([^<>@{}]+)</', line):
            text = m.group(1).strip()
            if not text or ' ' not in text:
                continue
            if is_code_like(text):
                continue
            if not re.search(r'[a-zA-ZäöüÄÖÜß]', text):
                continue
            findings.append(f'  Zeile {lineno}: Textknoten "{text}"')

    return findings

def main():
    try:
        data = json.load(sys.stdin)
    except (json.JSONDecodeError, ValueError):
        sys.exit(0)

    filepath = (
        data.get('tool_input', {}).get('file_path') or
        data.get('tool_response', {}).get('filePath') or ''
    )

    findings = check_file(filepath)

    if findings:
        filename = re.split(r'[/\\]', filepath)[-1]
        msg = (
            f"Möglicherweise hartcodierte UI-Strings in {filename}:\n"
            + '\n'.join(findings)
            + "\n→ Durch @L[\"SchlüsselName\"]-Aufrufe ersetzen oder bestätigen, dass kein Lokalisierungsbedarf besteht."
        )
        print(json.dumps({
            "hookSpecificOutput": {
                "hookEventName": "PostToolUse",
                "additionalContext": msg
            }
        }))

    sys.exit(0)

if __name__ == '__main__':
    main()

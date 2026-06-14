import sys
import json
import re
import os

data = json.load(sys.stdin)
file = data.get('tool_input', {}).get('file_path') or data.get('tool_response', {}).get('filePath') or ''
if not file.endswith('.al') or not os.path.isfile(file):
    sys.exit(0)

with open(file, encoding='utf-8') as f:
    lines = f.readlines()

pattern = re.compile(r"(Error|Message|Warning|FieldError|Confirm)\s*\([^)']*'[^']+'", re.IGNORECASE)

hits = []
for i, line in enumerate(lines, 1):
    stripped = line.strip()
    if stripped.startswith('//'):
        continue
    if pattern.search(line):
        hits.append('  Line {}: {}'.format(i, stripped[:100]))

if hits:
    context = '[AL Label Check] Hardcoded strings in {}:\n{}'.format(file, '\n'.join(hits))
    print(json.dumps({'hookSpecificOutput': {'hookEventName': 'PostToolUse', 'additionalContext': context}}))

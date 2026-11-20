#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Token-aware renamer for C# and XAML files.

Usage: python .tmp_rename.py <map.tsv> <root1> [root2 ...] [--apply]

map.tsv lines: OLD\tNEW   (exact identifiers, word-boundary matched)
In .cs files: replaces identifiers in CODE context only (never inside plain
string literals or comments), EXCEPT inside interpolation holes { } and inside
cref="/name="/paramref="/paramref" attribute values of /// doc comments.
In .xaml files: replaces only inside {Binding ...} / {x:Static ...} spans and
ConverterParameter="..." attribute values.
Without --apply, prints a diff summary of what would change.
"""
import re, sys, os

def load_map(path):
    pairs = []
    with open(path, encoding='utf-8') as f:
        for line in f:
            line = line.rstrip('\n')
            if not line.strip() or line.startswith('#'):
                continue
            old, new = line.split('\t')
            pairs.append((old, new))
    # longest-first so 'AufgabeId' beats 'Aufgabe'
    pairs.sort(key=lambda p: -len(p[0]))
    return pairs

def make_re(pairs):
    pat = '|'.join(re.escape(o) for o, _ in pairs)
    return re.compile(r'\b(' + pat + r')\b')

def sub_code(text, rx, m):
    return rx.sub(lambda mm: m[mm.group(1)], text)

# ---------- C# tokenizer ----------

def process_cs(text, rx, m):
    out = []
    i, n = 0, len(text)
    # state stack entries: ('string', interpolated, dollar_count, verbatim_raw)
    # or ('interp', depth_brace)
    stack = [('code',)]
    interp_depth = 0  # braces inside interpolation hole counting nested {}
    while i < n:
        st = stack[-1][0]
        ch = text[i]
        nxt = text[i+1] if i+1 < n else ''

        if st in ('code', 'interp'):
            # comments
            if ch == '/' and nxt == '/':
                j = i
                while j < n and text[j] != '\n':
                    j += 1
                seg = text[i:j]
                if seg.startswith('///'):
                    # process only inside quoted attr values of doc tags
                    def attrs(mm):
                        return mm.group(1) + sub_code(mm.group(2), rx, m) + mm.group(3)
                    seg = re.sub(r'(cref="|name="|paramref name=")([^"]*)(")', attrs, seg)
                out.append(seg); i = j; continue
            if ch == '/' and nxt == '*':
                j = text.find('*/', i+2)
                j = n if j < 0 else j+2
                out.append(text[i:j]); i = j; continue
            # char literal
            if ch == "'":
                j = i+1
                if j < n and text[j] == '\\':
                    j += 2
                while j < n and text[j] != "'":
                    j += 1
                out.append(text[i:j+1]); i = j+1; continue
            # string starts: optional @/$ prefixes, optional raw """
            k = i
            verbatim = False; dollars = 0
            if text[k:k+1] == '@':
                verbatim = True; k += 1
            while text[k:k+1] == '$':
                dollars += 1; k += 1
            if text[k:k+1] == '@' and not verbatim:
                verbatim = True; k += 1
                while text[k:k+1] == '$':
                    dollars += 1; k += 1
            if text[k:k+1] == '"':
                raw = 0
                while text[k+raw:k+raw+1] == '"':
                    raw += 1
                if raw >= 3:
                    stack.append(('raw', dollars, raw, verbatim))
                    out.append(text[i:k+raw]); i = k+raw; continue
                stack.append(('string', dollars, 1, verbatim))
                out.append(text[i:k+1]); i = k+1; continue
            if dollars or verbatim:
                out.append(text[i:k]); i = k; continue
            # interpolation hole end (we are inside a hole)
            if st == 'interp' and ch == '}':
                if interp_depth == 0:
                    stack.pop()
                    out.append(ch); i += 1; continue
                interp_depth -= 1
                out.append(ch); i += 1; continue
            if st == 'interp' and ch == '{':
                interp_depth += 1
                out.append(ch); i += 1; continue
            # normal code char: accumulate a code run
            j = i
            while j < n:
                c2 = text[j]
                if c2 == '"' or c2 == "'" or (c2 == '/' and j+1 < n and text[j+1] in '/*'):
                    break
                if c2 == '@' or c2 == '$':
                    # potential string prefix: stop if followed by "@ or $ or "
                    rest = text[j:]
                    if re.match(r'[@$]{0,3}"', rest):
                        break
                if st == 'interp' and c2 in '{}':
                    break
                j += 1
            out.append(sub_code(text[i:j], rx, m)); i = j; continue

        elif st == 'string':
            _, dollars, _, verbatim = stack[-1]
            if dollars and ch == '{':
                # count consecutive {, need 'dollars' braces to open a hole
                c = 0
                while text[i+c:i+c+1] == '{':
                    c += 1
                if c >= dollars and dollars > 0:
                    stack.append(('interp',))
                    interp_depth = 0
                    out.append('{'*dollars); i += dollars
                    # remaining braces are literal
                    out.append('{'*(c-dollars)); i += (c-dollars)
                    continue
                out.append('{'*c); i += c; continue
            if dollars and ch == '}':
                c = 0
                while text[i+c:i+c+1] == '}':
                    c += 1
                out.append('}'*c); i += c; continue
            if verbatim and ch == '"':
                if nxt == '"':
                    out.append('""'); i += 2; continue
                stack.pop(); out.append(ch); i += 1; continue
            if not verbatim and ch == '\\':
                out.append(text[i:i+2]); i += 2; continue
            if not verbatim and ch == '"':
                stack.pop(); out.append(ch); i += 1; continue
            out.append(ch); i += 1; continue

        elif st == 'raw':
            _, dollars, qcount, _ = stack[-1]
            if dollars and ch == '{':
                c = 0
                while text[i+c:i+c+1] == '{':
                    c += 1
                if c >= dollars:
                    stack.append(('interp',))
                    interp_depth = 0
                    out.append('{'*c); i += c; continue
                out.append('{'*c); i += c; continue
            if ch == '"':
                c = 0
                while text[i+c:i+c+1] == '"':
                    c += 1
                if c >= qcount:
                    stack.pop()
                    out.append('"'*c); i += c; continue
                out.append('"'*c); i += c; continue
            out.append(ch); i += 1; continue

    return ''.join(out)

# ---------- XAML processor ----------

BIND_RX = re.compile(r'\{Binding[^}]*\}|\{x:Static[^}]*\}|ConverterParameter="[^"]*"')

def process_xaml(text, rx, m):
    def rep(mm):
        seg = mm.group(0)
        if seg.startswith('ConverterParameter'):
            inner = seg[len('ConverterParameter="'):-1]
            return 'ConverterParameter="' + sub_code(inner, rx, m) + '"'
        return sub_code(seg, rx, m)
    return BIND_RX.sub(rep, text)

# ---------- driver ----------

def main():
    apply = '--apply' in sys.argv
    args = [a for a in sys.argv[1:] if a != '--apply']
    mapfile = args[0]
    roots = args[1:]
    pairs = load_map(mapfile)
    rx = make_re(pairs)
    m = dict(pairs)
    changed = []
    for root in roots:
        for dirpath, dirs, files in os.walk(root):
            dirs[:] = [d for d in dirs if d not in ('bin','obj','.git','node_modules')]
            for fn in files:
                p = os.path.join(dirpath, fn)
                if '/Migrations/' in p.replace('\\','/'):
                    continue
                if fn.endswith('.cs'):
                    proc = process_cs
                elif fn.endswith(('.xaml', '.axaml')):
                    proc = process_xaml
                else:
                    continue
                try:
                    text = open(p, encoding='utf-8').read()
                except Exception:
                    continue
                new = proc(text, rx, m)
                if new != text:
                    cnt = sum(1 for _ in re.finditer(rx, text))  # approx
                    changed.append(p)
                    if apply:
                        with open(p, 'w', encoding='utf-8', newline='') as f:
                            f.write(new)
    for p in changed:
        print('CHANGED' if apply else 'WOULD-CHANGE', p)
    print(f'{len(changed)} files', 'modified' if apply else 'would change')

if __name__ == '__main__':
    main()

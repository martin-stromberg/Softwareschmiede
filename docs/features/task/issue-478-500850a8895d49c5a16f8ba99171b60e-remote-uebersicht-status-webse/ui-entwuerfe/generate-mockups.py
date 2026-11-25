"""Erzeugt HTML-Mockups der Statusseite und rendert sie per Headless-Chrome zu PNG."""
import pathlib
import subprocess
import sys

BASE = pathlib.Path(__file__).parent
OUT = pathlib.Path(sys.argv[1])
OUT.mkdir(parents=True, exist_ok=True)

CHROME = r"C:\Program Files\Google\Chrome\Application\chrome.exe"

CSS = """
* { box-sizing: border-box; margin: 0; }
body { font-family: "Segoe UI", system-ui, sans-serif; background: #f4f5f7; color: #1f2430; }
.page { max-width: 1080px; margin: 0 auto; padding: 24px 20px 60px; }
header.top { display: flex; align-items: center; gap: 14px; flex-wrap: wrap;
  background: #1f2937; color: #f8fafc; padding: 16px 24px; border-radius: 10px; }
header.top h1 { font-size: 20px; font-weight: 600; flex: 1; }
.badge { display: inline-flex; align-items: center; gap: 7px; padding: 4px 12px;
  border-radius: 999px; font-size: 13px; font-weight: 600; }
.badge .dot { width: 9px; height: 9px; border-radius: 50%; }
.badge.online { background: #065f46; color: #d1fae5; } .badge.online .dot { background: #34d399; }
.badge.offline { background: #7f1d1d; color: #fee2e2; } .badge.offline .dot { background: #f87171; }
.badge.stale { background: #78350f; color: #fef3c7; } .badge.stale .dot { background: #fbbf24; }
.meta-line { margin: 10px 4px 22px; font-size: 12.5px; color: #6b7280; }
section { background: #fff; border: 1px solid #e5e7eb; border-radius: 10px;
  padding: 16px 20px; margin-bottom: 16px; }
section h2 { font-size: 15px; font-weight: 600; margin-bottom: 10px;
  display: flex; align-items: baseline; gap: 10px; }
.src { font-size: 11px; font-weight: 500; color: #fff; border-radius: 4px; padding: 1px 7px; }
.src.app { background: #4f46e5; } .src.gh { background: #0d9488; }
.count { color: #6b7280; font-weight: 500; }
table { width: 100%; border-collapse: collapse; font-size: 13.5px; }
td, th { text-align: left; padding: 7px 8px; border-top: 1px solid #f0f1f3; }
tr:first-child td { border-top: 0; }
td.num { width: 70px; font-family: Consolas, monospace; color: #4f46e5; }
td.right, th.right { text-align: right; }
td.muted { color: #6b7280; font-size: 12.5px; }
.pill { font-size: 11px; font-weight: 600; padding: 1px 8px; border-radius: 999px; white-space: nowrap; }
.pill.run { background: #e0e7ff; color: #3730a3; }
.pill.wait { background: #fef3c7; color: #92400e; }
.pill.done { background: #d1fae5; color: #065f46; }
.pill.err { background: #fee2e2; color: #991b1b; }
.pill.pause { background: #e5e7eb; color: #4b5563; }
.checks.ok { color: #047857; font-weight: 600; } .checks.fail { color: #b91c1c; font-weight: 600; }
a { color: #4f46e5; text-decoration: none; }
button.act { font-size: 11.5px; font-weight: 600; padding: 3px 10px; border-radius: 6px;
  border: 1px solid #c7d2fe; background: #eef2ff; color: #3730a3; cursor: pointer; }
.inst { font-size: 12px; color: #6b7280; font-weight: 500; }
.stale-note { display: flex; align-items: center; gap: 8px; background: #fef3c7;
  border: 1px solid #f59e0b55; color: #92400e; font-size: 12.5px;
  border-radius: 8px; padding: 8px 14px; margin-bottom: 14px; }
.dim td { color: #9ca3af; }
.dim .pill { opacity: .55; }
.empty { color: #6b7280; font-size: 13px; padding: 10px 4px; }
footer { margin-top: 26px; font-size: 11.5px; color: #9ca3af; text-align: center; }
"""

def page(title, body):
    return f"""<!doctype html><html lang="de"><head><meta charset="utf-8">
<meta http-equiv="refresh" content="15">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>{title}</title><style>{CSS}</style></head><body><div class="page">{body}
<footer>softwareschmiede-status · server-gerendert · Aktualisierung alle 15 s (meta-refresh) · GET /status.json für maschinelle Abfrage</footer>
</div></body></html>"""

def header(badge, inst="Instanz „Martin-PC“ · b7f3a2c1…"):
    return f"""<header class="top"><h1>Softwareschmiede-Status</h1>{badge}
<span class="inst" style="color:#cbd5e1">{inst}</span></header>"""

ONLINE = header('<span class="badge online"><span class="dot"></span>online</span>') + """
<div class="meta-line">Stand 18:32:14 · letzter App-Snapshot vor 12 s · GitHub-Abruf 18:32:02</div>

<section><h2>Laufende Aufgaben <span class="count">(2)</span> <span class="src app">App · live</span></h2>
<table>
<tr><td class="num">#479</td><td>XAML-Validierung für Fenster-Ressourcen</td>
    <td><span class="pill run">Running</span></td>
    <td class="muted">Session aktiv · Heartbeat 18:32</td><td class="right muted">Issue ↗</td></tr>
<tr><td class="num">#481</td><td>Release-Notes für 2.4.0 generieren</td>
    <td><span class="pill run">Started</span></td>
    <td class="muted">Heartbeat 18:31</td><td class="right muted">Issue ↗</td></tr>
</table></section>

<section><h2>Wartet auf Anwender <span class="count">(2)</span> <span class="src app">App · live</span></h2>
<table>
<tr><td class="num">#478</td><td>Remote-Übersicht: Status-Webseite (Relay)</td>
    <td><span class="pill wait">WaitingForInput</span></td>
    <td class="muted">„Rückfrage: Zugriffsschutz?" · seit 17:04</td>
    <td class="right"><button class="act">passt so</button> <button class="act">weiter machen</button></td></tr>
<tr><td class="num">#470</td><td>Bitbucket: OAuth-Refresh prüfen</td>
    <td><span class="pill pause">pausiert</span></td>
    <td class="muted">bis morgen 08:00</td><td class="right muted"></td></tr>
</table></section>

<section><h2>Letzte SCM-Ereignisse <span class="count">(5)</span> <span class="src app">App · live</span></h2>
<table>
<tr><td class="muted">18:29</td><td>IssueComment #478</td><td><span class="pill done">verarbeitet</span></td>
    <td class="muted">Aufgabe 500850a8</td></tr>
<tr><td class="muted">18:11</td><td>PullRequest #482</td><td><span class="pill err">abgelehnt</span></td>
    <td class="muted">„Eigene Antwort"</td></tr>
<tr><td class="muted">17:58</td><td>IssueComment #479</td><td><span class="pill done">verarbeitet</span></td>
    <td class="muted">Run gestartet</td></tr>
</table></section>

<section><h2>Offene Pull Requests <span class="count">(3)</span> <span class="src gh">GitHub · 18:32</span></h2>
<table>
<tr><td class="num">#482</td><td>Backmerge staging → main</td><td class="checks fail">Checks: ✗ Blocked</td>
    <td class="right"><button class="act">Review anfordern</button></td></tr>
<tr><td class="num">#480</td><td>Fix: Token-Rotation CredentialStore</td><td class="checks ok">Checks: ✓</td>
    <td class="right muted"></td></tr>
<tr><td class="num">#477</td><td>Doku: SCM-Monitoring-Einrichtung</td><td class="checks ok">Checks: ✓</td>
    <td class="right muted"></td></tr>
</table></section>

<section><h2>Letzte Releases <span class="count">(3)</span> <span class="src gh">GitHub · 18:32</span></h2>
<table>
<tr><td class="num">v2.3.1</td><td>Hotfix SCM-Checkpoint</td><td class="muted">22.11.2026</td></tr>
<tr><td class="num">v2.3.0</td><td>PR-Watchlist + ETag-Cache</td><td class="muted">15.11.2026</td></tr>
<tr><td class="num">v2.2.4</td><td>Stabilität ConPTY</td><td class="muted">02.11.2026</td></tr>
</table></section>"""

OFFLINE = header('<span class="badge offline"><span class="dot"></span>offline</span>') + """
<div class="meta-line">Stand 18:47:30 · App-Verbindung getrennt · GitHub-Abruf 18:47:12</div>
<div class="stale-note">⚠ App-Instanz „Martin-PC" offline seit 18:01:22 — App-Sektionen zeigen den letzten bekannten Stand (vor 46 min), GitHub-Sektionen sind live.</div>

<section class="dim"><h2>Laufende Aufgaben <span class="count">(2)</span> <span class="src app">App · Stand 18:01</span></h2>
<table>
<tr><td class="num">#479</td><td>XAML-Validierung für Fenster-Ressourcen</td>
    <td><span class="pill run">Running</span></td><td class="muted">Heartbeat 18:01</td></tr>
<tr><td class="num">#481</td><td>Release-Notes für 2.4.0 generieren</td>
    <td><span class="pill run">Started</span></td><td class="muted">Heartbeat 18:00</td></tr>
</table></section>

<section class="dim"><h2>Wartet auf Anwender <span class="count">(1)</span> <span class="src app">App · Stand 18:01</span></h2>
<table>
<tr><td class="num">#478</td><td>Remote-Übersicht: Status-Webseite (Relay)</td>
    <td><span class="pill wait">WaitingForInput</span></td><td class="muted">seit 17:04</td></tr>
</table></section>

<section><h2>Offene Pull Requests <span class="count">(3)</span> <span class="src gh">GitHub · live</span></h2>
<table>
<tr><td class="num">#482</td><td>Backmerge staging → main</td><td class="checks fail">Checks: ✗ Blocked</td></tr>
<tr><td class="num">#480</td><td>Fix: Token-Rotation CredentialStore</td><td class="checks ok">Checks: ✓</td></tr>
</table></section>"""

NODATA = header('<span class="badge stale"><span class="dot"></span>keine App-Daten</span>', inst="keine Instanz verbunden") + """
<div class="meta-line">Stand 09:12:44 · noch kein App-Snapshot empfangen · GitHub-Abruf 09:12:30</div>
<div class="stale-note">ℹ Es hat sich noch keine App-Instanz am Relay registriert. Sobald in den App-Einstellungen die Remote-Übersicht aktiviert ist, erscheinen hier die App-Sektionen.</div>

<section><h2>Laufende Aufgaben <span class="src app">App</span></h2>
<div class="empty">Keine Daten — App war noch nie verbunden.</div></section>
<section><h2>Wartet auf Anwender <span class="src app">App</span></h2>
<div class="empty">Keine Daten — App war noch nie verbunden.</div></section>

<section><h2>Offene Pull Requests <span class="count">(3)</span> <span class="src gh">GitHub · live</span></h2>
<table>
<tr><td class="num">#482</td><td>Backmerge staging → main</td><td class="checks fail">Checks: ✗ Blocked</td></tr>
<tr><td class="num">#480</td><td>Fix: Token-Rotation CredentialStore</td><td class="checks ok">Checks: ✓</td></tr>
<tr><td class="num">#477</td><td>Doku: SCM-Monitoring-Einrichtung</td><td class="checks ok">Checks: ✓</td></tr>
</table></section>"""

MOBILE = header('<span class="badge online"><span class="dot"></span>online</span>', inst="Martin-PC") + """
<div class="meta-line">Stand 18:32 · Snapshot vor 12 s · GitHub 18:32</div>
<section><h2>Laufende Aufgaben <span class="count">(2)</span> <span class="src app">App</span></h2>
<table>
<tr><td class="num">#479</td><td>XAML-Validierung<br><span class="muted">Heartbeat 18:32</span></td>
    <td><span class="pill run">Running</span></td></tr>
<tr><td class="num">#481</td><td>Release-Notes 2.4.0<br><span class="muted">Heartbeat 18:31</span></td>
    <td><span class="pill run">Started</span></td></tr>
</table></section>
<section><h2>Wartet auf Anwender <span class="count">(1)</span> <span class="src app">App</span></h2>
<table>
<tr><td class="num">#478</td><td>Remote-Übersicht (Relay)<br><span class="muted">seit 17:04</span></td>
    <td><span class="pill wait">Waiting</span></td></tr>
</table></section>
<section><h2>Offene PRs <span class="count">(2)</span> <span class="src gh">GitHub</span></h2>
<table>
<tr><td class="num">#482</td><td>Backmerge → main</td><td class="checks fail">✗</td></tr>
<tr><td class="num">#480</td><td>Token-Rotation</td><td class="checks ok">✓</td></tr>
</table></section>"""

PAGES = [
    ("01-entwurf-online", ONLINE, 1280, 1150),
    ("02-entwurf-offline", OFFLINE, 1280, 900),
    ("03-entwurf-keine-daten", NODATA, 1280, 800),
    ("04-entwurf-mobil", MOBILE, 430, 950),
]

for name, body, w, h in PAGES:
    html_file = BASE / f"{name}.html"
    html_file.write_text(page(name, body), encoding="utf-8")
    png = OUT / f"{name}.png"
    r = subprocess.run(
        [CHROME, "--headless=new", "--disable-gpu", "--hide-scrollbars",
         f"--window-size={w},{h}", f"--screenshot={png}", html_file.as_uri()],
        capture_output=True, text=True, timeout=90)
    print(name, "->", png.name, "rc=", r.returncode, r.stderr.strip()[:200])

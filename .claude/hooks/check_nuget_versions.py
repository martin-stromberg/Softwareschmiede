"""
NuGet-Versions-Check: Meldet veraltete NuGet-Pakete mit verfügbaren kompatiblen Updates.
Berücksichtigt das Ziel-Framework des Projekts — Paketversionen, die ein neueres
.NET erfordern als das Projekt nutzt, werden bei der Suche übersprungen.
"""
import sys
import json
import os
import re
import urllib.request
import xml.etree.ElementTree as ET

# ── TFM-Parsing und Kompatibilitätsprüfung ────────────────────────────────────

_NET5_RE = re.compile(r'^net(\d+)\.(\d+)$')
_NETSTANDARD_RE = re.compile(r'^netstandard(\d+)\.(\d+)$')
_NETCOREAPP_RE = re.compile(r'^netcoreapp(\d+)\.(\d+)$')
_NETFX_SHORT_RE = re.compile(r'^net(\d{3,4})$')
_NETFX_LONG_RE = re.compile(r'^netframework(\d+)(?:\.(\d+))?(?:\.(\d+))?$')


def normalize_nuget_tfm(tfm):
    """Normalisiert NuGet-Registrierungs-TFMs in kanonisches Kleinbuchstaben-TFM-Format."""
    if not tfm:
        return ''
    s = tfm.lower().strip().lstrip('.')
    # ".netframework4.6.1" → "net461"
    m = _NETFX_LONG_RE.match(s)
    if m:
        parts = [p for p in m.groups() if p]
        return 'net' + ''.join(parts)
    # Sonst bereits in TFM-Format (netstandard2.0, netcoreapp3.1, net8.0 usw.)
    return s


def is_tfm_compatible(pkg_tfm_raw, proj_tfm):
    """
    Prüft ob ein Paket-TFM mit dem Projekt-TFM kompatibel ist.
    Vereinfachte NuGet-Kompatibilitätsregeln.
    """
    pkg_tfm = normalize_nuget_tfm(pkg_tfm_raw)

    if not pkg_tfm or pkg_tfm in ('', 'any', 'agnostic'):
        return True  # Universelles Paket

    # Projekt: net5.0+ (modernes .NET)
    m = _NET5_RE.match(proj_tfm)
    if m:
        proj_ver = (int(m.group(1)), int(m.group(2)))
        pkg_net = _NET5_RE.match(pkg_tfm)
        if pkg_net:
            return (int(pkg_net.group(1)), int(pkg_net.group(2))) <= proj_ver
        if _NETSTANDARD_RE.match(pkg_tfm):
            return True
        if _NETCOREAPP_RE.match(pkg_tfm):
            return True
        if _NETFX_SHORT_RE.match(pkg_tfm):
            return False  # .NET Framework nicht kompatibel mit modernem .NET
        return False

    # Projekt: netcoreapp
    m = _NETCOREAPP_RE.match(proj_tfm)
    if m:
        proj_ver = (int(m.group(1)), int(m.group(2)))
        pkg_ca = _NETCOREAPP_RE.match(pkg_tfm)
        if pkg_ca:
            return (int(pkg_ca.group(1)), int(pkg_ca.group(2))) <= proj_ver
        if _NETSTANDARD_RE.match(pkg_tfm):
            return True
        return False

    # Projekt: netstandard
    m = _NETSTANDARD_RE.match(proj_tfm)
    if m:
        proj_ver = (int(m.group(1)), int(m.group(2)))
        pkg_ns = _NETSTANDARD_RE.match(pkg_tfm)
        if pkg_ns:
            return (int(pkg_ns.group(1)), int(pkg_ns.group(2))) <= proj_ver
        return False

    # Projekt: .NET Framework (net461, net48 usw.)
    m = _NETFX_SHORT_RE.match(proj_tfm)
    if m:
        proj_ver = int(m.group(1))
        pkg_fx = _NETFX_SHORT_RE.match(pkg_tfm)
        if pkg_fx:
            return int(pkg_fx.group(1)) <= proj_ver
        if _NETSTANDARD_RE.match(pkg_tfm):
            return True
        return False

    return False


def pkg_version_compat(dep_groups, proj_tfm):
    """True wenn mindestens eine dep-group mit dem Projekt-TFM kompatibel ist."""
    if not dep_groups:
        return True  # Kein dep-group = universell kompatibel
    return any(
        is_tfm_compatible(g.get("targetFramework") or "", proj_tfm)
        for g in dep_groups
    )


# ── NuGet HTTP API ────────────────────────────────────────────────────────────

def fetch_json(url, timeout=8):
    try:
        req = urllib.request.Request(url, headers={'User-Agent': 'ClaudeHook/1.0'})
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return json.loads(r.read())
    except Exception:
        return None


def get_all_versions(pkg_id):
    """Alle verfügbaren Versionen eines Pakets (NuGet flat-container API)."""
    data = fetch_json(
        f"https://api.nuget.org/v3-flatcontainer/{pkg_id.lower()}/index.json"
    )
    return data.get("versions", []) if data else []


def get_dep_groups(pkg_id, version):
    """dependencyGroups für eine spezifische Paketversion (NuGet registration API)."""
    data = fetch_json(
        f"https://api.nuget.org/v3/registration5/{pkg_id.lower()}/{version.lower()}.json"
    )
    if not data:
        return None
    entry = data.get("catalogEntry", data)
    return entry.get("dependencyGroups")


def is_prerelease(version):
    return bool(re.search(r'[a-zA-Z]', version.split('+')[0]))


def semver_tuple(version):
    clean = re.split(r'[-+]', version)[0]
    try:
        return tuple(int(x) for x in clean.split('.')[:3])
    except ValueError:
        return (0, 0, 0)


def find_latest_compatible(pkg_id, current_version, proj_tfm):
    """
    Gibt die neueste stabile, kompatible Version zurück (oder None wenn keine existiert).
    Iteriert von neuester zu ältester Kandidatenversion. Bricht nach 10 inkompatiblen
    Versionen ab um Timeouts bei sehr neuen Paketen zu vermeiden.
    """
    all_versions = get_all_versions(pkg_id)
    if not all_versions:
        return None

    current_sv = semver_tuple(current_version)
    allow_prerelease = is_prerelease(current_version)

    candidates = [
        v for v in all_versions
        if semver_tuple(v) > current_sv
        and (allow_prerelease or not is_prerelease(v))
    ]
    if not candidates:
        return None

    skipped = 0
    for v in reversed(candidates):
        dep_groups = get_dep_groups(pkg_id, v)
        if dep_groups is None:
            continue  # API-Fehler, Version überspringen
        if pkg_version_compat(dep_groups, proj_tfm):
            return v
        skipped += 1
        if skipped >= 10:
            break  # Zu viele inkompatible Versionen in Folge — Suche abbrechen

    return None


# ── .csproj lesen ─────────────────────────────────────────────────────────────

def read_csproj(csproj_path):
    """Gibt (proj_tfm, [(name, version)]) zurück."""
    try:
        root = ET.parse(csproj_path).getroot()
    except ET.ParseError:
        return None, []

    proj_tfm = ''
    for node in root.iter("TargetFramework"):
        proj_tfm = (node.text or "").strip().lower()
        break
    if not proj_tfm:
        for node in root.iter("TargetFrameworks"):
            tfms = [t.strip().lower() for t in (node.text or "").split(";") if t.strip()]
            proj_tfm = tfms[0] if tfms else ''
            break

    packages = []
    for ref in root.iter("PackageReference"):
        name = ref.get("Include") or ref.get("Update") or ""
        version = (ref.get("Version") or "").strip()
        if not version:
            v_node = ref.find("Version")
            if v_node is not None:
                version = (v_node.text or "").strip()
        # Versionsvariablen wie $(SomeVersion) überspringen
        if name and version and not version.startswith("$"):
            packages.append((name, version))

    return proj_tfm, packages


# ── Hauptlogik ────────────────────────────────────────────────────────────────

data = json.load(sys.stdin)
file = (
    data.get("tool_input", {}).get("file_path")
    or data.get("tool_response", {}).get("filePath")
    or ""
)
if not file.endswith(".csproj") or not os.path.isfile(file):
    sys.exit(0)

proj_tfm, packages = read_csproj(file)
if not proj_tfm or not packages:
    sys.exit(0)

updates = []
for name, current in packages:
    latest = find_latest_compatible(name, current, proj_tfm)
    if latest:
        updates.append((name, current, latest))

if updates:
    lines = "\n".join(
        f"  • {name}: {current} → {latest}"
        for name, current, latest in updates
    )
    print(json.dumps({
        "hookSpecificOutput": {
            "hookEventName": "PostToolUse",
            "additionalContext": (
                f"[NuGet-Versions-Check] In {os.path.basename(file)} ({proj_tfm}) "
                f"gibt es neuere kompatible Paketversionen:\n{lines}\n"
                "Aktualisiere die Versionen in der .csproj oder via "
                "'dotnet add package <name> --version <version>'."
            ),
        }
    }))

"""
Token-Usage-Logging fuer /lifecycle
Protokolliert den Token-Verbrauch von Unteragenten (SubagentStop) und des
Orchestrators selbst (Stop) in eine repo-weite CSV-Datei, damit der Verbrauch
je Lifecycle-Schritt nachtraeglich ausgewertet werden kann.

Bekannter Haken (nicht fatal): Der Schrittname wird aus dem im Unteragenten-
Prompt woertlich enthaltenen Kommandonamen (z. B. "/plan") erraten, da Claude
Code Hooks die Task-Beschreibung nicht mitliefern. Trifft kein bekannter
Kommandoname zu, wird der agent_type bzw. "orchestrator" verwendet.
"""
import sys
import json
import os
import re
import csv
import subprocess
from datetime import datetime, timezone

KNOWN_STEPS = [
    "translate-requirements",
    "inventory",
    "plan",
    "implement",
    "review-plan",
    "review-code",
    "run-tests",
    "update-docs",
    "update-readme",
    "lifecycle",
]

LOG_FILE = os.path.join("docs", "token-usage-log.csv")
FIELDS = [
    "timestamp", "branch", "step", "agent_type", "agent_id",
    "input_tokens", "output_tokens", "cache_creation_input_tokens",
    "cache_read_input_tokens", "total_tokens",
]


def current_branch():
    try:
        result = subprocess.run(
            ["git", "branch", "--show-current"],
            capture_output=True, text=True, check=False,
        )
        return result.stdout.strip() or "unknown"
    except Exception:
        return "unknown"


def guess_step(transcript_text, agent_type, hook_event_name):
    if hook_event_name == "Stop":
        return "orchestrator"
    for name in KNOWN_STEPS:
        if "/" + name in transcript_text:
            return name
    return agent_type or "unknown"


def sum_usage(obj, totals):
    if isinstance(obj, dict):
        if "input_tokens" in obj or "output_tokens" in obj:
            totals["input"] += obj.get("input_tokens", 0) or 0
            totals["output"] += obj.get("output_tokens", 0) or 0
            totals["cache_creation"] += obj.get("cache_creation_input_tokens", 0) or 0
            totals["cache_read"] += obj.get("cache_read_input_tokens", 0) or 0
        for value in obj.values():
            sum_usage(value, totals)
    elif isinstance(obj, list):
        for item in obj:
            sum_usage(item, totals)


def read_transcript(path):
    try:
        with open(path, "r", encoding="utf-8") as f:
            return f.read()
    except Exception:
        return ""


def main():
    try:
        data = json.load(sys.stdin)
    except Exception:
        return

    hook_event_name = data.get("hook_event_name", "")
    transcript_path = data.get("transcript_path", "")
    agent_type = data.get("agent_type", "")
    agent_id = data.get("agent_id", "")

    raw_text = read_transcript(transcript_path)
    if not raw_text.strip():
        return

    totals = {"input": 0, "output": 0, "cache_creation": 0, "cache_read": 0}
    for line in raw_text.splitlines():
        line = line.strip()
        if not line:
            continue
        try:
            entry = json.loads(line)
        except Exception:
            continue
        sum_usage(entry, totals)

    total_tokens = totals["input"] + totals["output"] + totals["cache_creation"] + totals["cache_read"]
    if total_tokens == 0:
        return

    step = guess_step(raw_text, agent_type, hook_event_name)
    branch = current_branch()

    row = {
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "branch": branch,
        "step": step,
        "agent_type": agent_type or "-",
        "agent_id": agent_id or "-",
        "input_tokens": totals["input"],
        "output_tokens": totals["output"],
        "cache_creation_input_tokens": totals["cache_creation"],
        "cache_read_input_tokens": totals["cache_read"],
        "total_tokens": total_tokens,
    }

    os.makedirs(os.path.dirname(LOG_FILE), exist_ok=True)
    file_exists = os.path.isfile(LOG_FILE)
    with open(LOG_FILE, "a", encoding="utf-8", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=FIELDS)
        if not file_exists:
            writer.writeheader()
        writer.writerow(row)


if __name__ == "__main__":
    try:
        main()
    except Exception:
        # Logging darf den eigentlichen Ablauf nie blockieren.
        pass

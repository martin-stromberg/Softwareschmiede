"""
PostToolUse-Hook: gibt den Build-Lock frei, der von build_before_test.py
(PreToolUse vor "dotnet test") bewusst offen gelassen wurde.

Matcht auf denselben "dotnet test"-Bash-Befehl wie der PreToolUse-Hook und
feuert erst, wenn der eigentliche Testlauf abgeschlossen ist. Damit ist der
Lock ueber die volle Testlaufzeit gehalten, nicht nur ueber den kurzen
Pre-Build - siehe build_before_test.py fuer die ausfuehrliche Begruendung.

Gibt den Lock nur frei, wenn die "tool_use_id" aus dem PostToolUse-JSON mit
dem beim Erwerb hinterlegten Owner-Token uebereinstimmt (siehe
dotnet_lock._is_owner) - sonst koennte ein fremdes Lock geloescht werden,
das gerade ein anderer Prozess haelt (z.B. der Stop-Hook nach einem
Lock-Timeout).

Reine Aufraeumaktion: schlaegt nie fehl, blockiert "dotnet test" nie.
"""
import json
import sys

import dotnet_lock


def main():
    try:
        data = json.load(sys.stdin)
    except Exception:
        data = {}
    dotnet_lock.release(data.get("tool_use_id"))


if __name__ == "__main__":
    main()

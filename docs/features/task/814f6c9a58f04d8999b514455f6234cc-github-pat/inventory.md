# Bestandsaufnahme: Git-Branch-Erstellung ohne Upstream-Tracking

Diese Bestandsaufnahme dokumentiert die aktuelle Implementierung der `CreateBranchAsync`-Methode in der Git-Plugin-Infrastruktur und deren Überreitungsimplementierungen in spezialisierter Plugins.

## Zusammenfassung

**Betroffener Code:**
- Basismethode in `GitPluginBase.CreateBranchAsync` (Zeilen 112–123)
- Keine Überreitungen in GitHubPlugin und BitBucketPlugin
- Eine Überreitungsimplementierung in LocalDirectoryPlugin (die `base.CreateBranchAsync()` aufruft)

**Aktuelle Probleme:**
1. Die Basismethode setzt bei Angabe von `sourceBranchName` kein `--no-track`-Flag, wodurch Git automatisch Upstream-Tracking einrichtet
2. Bestehende Tests prüfen nur den Fall ohne `sourceBranchName`
3. Tests auf exakte Argument-Sequenzen sind teilweise zu ungenau (GitHubPluginTests)

**Warum relevant:**
Die fehlende `--no-track`-Flag führt dazu, dass externe Git-Operationen (z. B. `git push` ohne Zielangabe) direkt in den Basis-Branch pushen statt in den neuen Task-Branch.

## Details

- [Logik-Klassen](inventory/logic.md) – Implementierungen und Überreitungen der Methode
- [Tests](inventory/tests.md) – Bestehende Test-Abdeckung und Lücken

## Architektur-Übersicht

```
GitPluginBase (Abstraktion)
    ├── CreateBranchAsync() [virtual] ← zu fixende Basismethode
    │
    ├── GitHubPlugin (konkrete Implementierung)
    │   └── nutzt CreateBranchAsync() aus Basis
    │
    ├── BitBucketPlugin (konkrete Implementierung)
    │   └── nutzt CreateBranchAsync() aus Basis
    │
    └── LocalDirectoryPlugin (konkrete Implementierung)
        └── CreateBranchAsync() [override]
            └── ruft base.CreateBranchAsync() auf → Fix wird automatisch vererbt
```

## Änderungsfolgen

Eine Anpassung der Basismethode `GitPluginBase.CreateBranchAsync` wird automatisch von allen spezialisierter Plugins übernommen, insbesondere durch `LocalDirectoryPlugin.CreateBranchAsync`, das explizit `base.CreateBranchAsync()` aufruft.

Die Plugins GitHubPlugin und BitBucketPlugin erben das neue Verhalten unmittelbar ohne weitere Änderungen nötig zu sein.

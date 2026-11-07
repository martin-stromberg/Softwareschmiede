# Bestandsaufnahme: Devin-CLI-Versionsbereich der SCM-Ereigniszustellung

**Hinweis:** Retrospektiv erstellt (nach der Umsetzung); beschreibt den Stand vor der Änderung.

## Ergebnis

Die CLI-Erkennung für die SCM-Ereigniszustellung ist zentral im Promptformat-Katalog `ScmCliPromptProfile` definiert: Banner-Anker, Versions-Regex und halboffene Versionsbereiche `[Min, MaxExclusive)` je Plugin-Präfix. Für `Softwareschmiede.Devin` war der Bereich `[3000.11.0, 3000.12.0)` hinterlegt — ein Minor-Update der Devin CLI auf `v3000.12.x` ließ den Erkennungszustand auf `Unsupported` kippen; das Ereignis blieb wartend mit „wird nicht unterstützt".

## Betroffene Bereiche

| Bereich | Bestand | Relevanz |
|---|---|---|
| Versionskatalog | `src/Softwareschmiede/Application/Services/ScmCliPromptProfile.cs` | `ScmCliVersionRange` (halboffen), Devin-Eintrag mit `SupportedRanges` und `SupportedVersionsText` `"v3000.11.x"` |
| Erkennungs-/Wartegrund | `ScmCliRecognition`, `ScmCliWaitReason`, `ScmPromptSessionAdapter` | `Describe` formt den Wartegrund aus `CliName` + `Version` + `SupportedVersionsText`; kein eigener Versionsabgleich |
| Zugriffsprüfung | `ScmCliAccessEvaluator` | Bewertet ausschließlich Startvertrag/Berechtigungen, keine Version — unberührt |
| Unit-Tests | `ScmCliPromptProfileTests`, `ScmEventOrchestratorTests` | Grenzfälle `v3000.12.0`/`v3000.12.7` als „nicht unterstützt" codiert; Wartegrundtexte mit `v3000.11.x` |
| Test-Fixture | `ScmDispatchFixture` (`ScmKiProfile.Devin`) | Default-Banner `v3000.11.92`, Kommentar „unterstützter Bereich v3000.11.x" |
| Integrationstests | `ScmEventDispatchTests_Devin`, `ScmEventCliExchangeAccessTests(.PendingProbe/.RestrictedProcess)` | `v3000.12.x`-Banner als Unsupported-Fälle; Supported-Fälle nur `v3000.11.x` |
| Hilfedoku | `docs/help/plugins/github-ereignisse/*` (6 Dateien) | `v3000.11.x` + Erläuterung „jeweils gleiches Major.Minor, beliebiger Patch" |

## Festgestellte Lücken

1. Der Devin-Bereich war auf ein einzelnes Major.Minor-Paar eingeschränkt, obwohl fachlich die ganze Major-Version `3000` toleriert werden soll.
2. Sämtliche Grenzfall-Tests und Dokutexte spiegelten den engen Bereich und mussten mitgezogen werden.

## Randbedingungen

- Der Versionsvergleich ist numerisch (`Version`-Vergleich), kein Stringvergleich — ein Bereich `[3000.0.0, 3001.0.0)` deckt `v3000.*.*` exakt ab; `v2999.x` und `v3001.x` bleiben außen.
- `SupportedVersionsText` ist reiner Anzeigetext im Wartegrund; die wirksame Grenze liegt ausschließlich in `SupportedRanges`.
- Die Dateitool-Kanalprobe bleibt unabhängig von der Versionserkennung die eigentliche Sicherheitsgrenze; die Bereichserweiterung schwächt keine Zugriffsprüfung auf.

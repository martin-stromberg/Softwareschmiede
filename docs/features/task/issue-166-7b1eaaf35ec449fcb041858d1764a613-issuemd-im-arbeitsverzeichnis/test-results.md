# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

## Fehlgeschlagene Tests

Keine Tests fehlgeschlagen (stabile Testlane, `Category!=OsInterface`).

## Zusammenfassung

- Gesamt: 982
- Bestanden: 981
- Fehlgeschlagen: 0
- Übersprungen: 1

## Hinweis

Ein vorheriger Testlauf durch einen Unteragenten umfasste versehentlich auch `Category=OsInterface`-Tests (Clipboard, FlaUI-E2E) ohne die für diese Sandbox erforderlichen Umgebungseinstellungen (`SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS`, Clipboard-Zugriff). Das führte zu 2 gemeldeten Fehlschlägen (`OnPreviewKeyDown_CtrlV_SetsHandledTrue`, `AufgabeStarten_KlontRepositoryUndStartetCli_E2E`), die keine Regression durch die Codeänderung darstellen, sondern bekannte Sandbox-Einschränkungen sind (siehe CLAUDE.md). Verifiziert durch erneuten Lauf der stabilen Testlane (`--filter "Category!=OsInterface"`, `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`): 981 bestanden, 0 Fehler, 1 übersprungen.

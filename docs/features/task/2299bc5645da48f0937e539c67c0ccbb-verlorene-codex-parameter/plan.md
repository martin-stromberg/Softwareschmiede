# Umsetzungsplan: Verlorene Codex-Parameter (Credential-Store-Isolation der E2E-Tests)

## Übersicht

E2E-Tests schreiben Test-Werte in den OS-weiten Windows Credential Store (`ICredentialStore` / `WindowsCredentialStore`), ohne die vorhandenen produktiven Werte vorher zu sichern und garantiert wiederherzustellen. Dadurch gehen produktiv konfigurierte Werte — insbesondere `Softwareschmiede.Codex.CommandLineParameters` — verloren oder werden überschrieben, vor allem wenn ein Test vor seinem manuellen `DeleteCredential`-Aufruf abbricht. Umgesetzt wird eine zentrale Backup/Restore-Mechanik (Memento) in der E2E-Basisklasse `WpfTestBase`, die den Zustand aller von Tests berührten Credential-Schlüssel vor dem Test sichert und in `Dispose()` — auch im Fehlerfall — wiederherstellt. Betroffen ist ausschließlich die Test-Infrastruktur; Produktivcode ändert sich nicht.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Test-Isolation des Credential Stores | **Option A: zentrales Backup/Restore (Memento) in `WpfTestBase`** statt Option B (Mock) oder Option C (Test-Namespace). | Die E2E-Tests starten `Softwareschmiede.App.exe` als **separaten Prozess**, der den realen OS-Credential-Store liest. Ein in den Testprozess injizierter Mock (Option B) hätte darauf keinen Einfluss — Option B ist für diese prozessübergreifenden Tests technisch nicht umsetzbar. Ein Test-Namespace (Option C) würde vom Produktivcode nicht gelesen und die Tests damit wirkungslos machen. Nur ein echtes Sichern/Wiederherstellen der produktiven Werte schützt die Konfiguration zuverlässig. |
| Kapselung der Backup/Restore-Logik | Eigene, klein gehaltene Klasse `CredentialStoreSnapshot` (Memento / Value Object) im Testprojekt statt Inline-Dictionary in `WpfTestBase`. | Hält `WpfTestBase` fokussiert und macht die Sicherungs-/Wiederherstellungssemantik isoliert unit-testbar (mit einem In-Memory-`ICredentialStore`-Fake, für den es im Testprojekt bereits Vorlagen gibt) — ohne den realen OS-Store zu berühren. |
| Zeitpunkt von Backup und Restore | Backup im Konstruktor von `WpfTestBase`, Restore in `Dispose()`. | xUnit erzeugt pro Testmethode eine neue Instanz (Konstruktor läuft vor dem Testkörper) und ruft `Dispose()` garantiert auch nach einem geworfenen Assert/Exception auf. Das liefert die geforderte Fehlerresistenz ohne zusätzliches `try/finally` in jedem Test. `[Collection("E2E")]` verhindert Parallelität und damit Interferenz auf dem prozessweiten Store. |
| Restore-Semantik pro Schlüssel | War der Schlüssel vor dem Test vorhanden → mit ursprünglichem Wert via `SetCredential` wiederherstellen; war er nicht vorhanden (`GetCredential` == `null`) → via `DeleteCredential` entfernen. | Stellt exakt den Ausgangszustand wieder her, statt (wie bisher) produktive Werte pauschal zu löschen. |

## Programmabläufe

### Sicherung und Wiederherstellung der Credential-Werte pro E2E-Test

1. xUnit instanziiert eine Testklasse, die von `WpfTestBase` erbt → der Konstruktor von `WpfTestBase` läuft.
2. Der Konstruktor erzeugt eine `CredentialStoreSnapshot`-Instanz über einem `WindowsCredentialStore` und der festen Liste der verwalteten Schlüssel. Deren Konstruktor ruft für jeden Schlüssel `ICredentialStore.GetCredential(target)` auf und merkt sich den Wert (inkl. `null` für „nicht vorhanden").
3. Der Testkörper läuft und schreibt beliebige dieser Schlüssel — direkt (`SetCredential`), über Helfer (`ConfirmLocalDirectoryGitInitInSourceDirectory`, `SetLocalDirectoryWorkspaceMode`) oder indirekt über die UI der gestarteten App (`ConfigureLocalDirectoryPlugin`, Einstellungen speichern).
4. Nach dem Test (auch bei Assertion-Fehler oder Exception) ruft xUnit `WpfTestBase.Dispose()` auf.
5. `Dispose()` ruft `CredentialStoreSnapshot.Restore()` auf: Für jeden gemerkten Schlüssel wird der ursprüngliche Wert per `SetCredential` zurückgeschrieben bzw. per `DeleteCredential` entfernt, falls er ursprünglich nicht existierte.
6. Der produktive Ausgangszustand des Credential Stores ist wiederhergestellt; kein Test-Wert bleibt zurück.

Beteiligte Klassen/Komponenten: `WpfTestBase`, `CredentialStoreSnapshot`, `WindowsCredentialStore`, `ICredentialStore`.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `CredentialStoreSnapshot` | Klasse (Memento / Value Object, Testinfrastruktur in `src/Softwareschmiede.Tests/E2E/`) | Sichert im Konstruktor die aktuellen Werte einer vorgegebenen Menge von Credential-Schlüsseln über einem `ICredentialStore` und stellt sie via `Restore()` exakt wieder her (Wert zurückschreiben bzw. löschen, falls ursprünglich nicht vorhanden). |

## Änderungen an bestehenden Klassen

### `WpfTestBase` (abstrakte Testbasisklasse)

- **Neue Felder:** `_credentialSnapshot` (`CredentialStoreSnapshot`) — im Konstruktor angelegter Zustands-Snapshot der verwalteten Credential-Schlüssel; eine konstante/statische Liste der verwalteten Schlüssel-Namen (`Softwareschmiede.Codex.CommandLineParameters`, `LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory`, `LocalDirectoryPlugin.WorkspaceMode`, `LocalDirectoryPlugin.SourceDirectory`, `Softwareschmiede.Codex.ExecutablePath`).
- **Geänderter Konstruktor:** legt zusätzlich zum bestehenden DB-Pfad den `CredentialStoreSnapshot` über einem `WindowsCredentialStore` und der Schlüsselliste an.
- **Geänderte Methode `Dispose()`:** Der Aufruf `DeleteLocalDirectoryPluginCredentials()` wird durch `_credentialSnapshot.Restore()` ersetzt (weiterhin in einem `try/catch` mit `Debug.WriteLine`, wie bisher, damit ein Restore-Fehler den übrigen Teardown nicht abbricht).
- **Entfernte Methode:** `DeleteLocalDirectoryPluginCredentials()` — wird durch die Restore-Mechanik überflüssig (das pauschale Löschen produktiver Werte entfällt).
- **Unverändert bleiben:** die Helfer `ConfirmLocalDirectoryGitInitInSourceDirectory()` und `SetLocalDirectoryWorkspaceMode(string)` (sie setzen Werte; das Aufräumen übernimmt jetzt der Restore).

### `E2E_TaskExecutionCommandLineParameters` (E2E-Testklasse)

- **Geänderte Methode `AufgabeStarten_MitCodexCommandLineParametersImStore_KiSimulatorStartetKorrekt_E2E`:** Die abschließende Zeile `new WindowsCredentialStore().DeleteCredential("Softwareschmiede.Codex.CommandLineParameters")` entfällt — das Aufräumen erfolgt jetzt zentral über den Restore in `Dispose()`. Das anfängliche `SetCredential(...)` bleibt (setzt den Testwert).

### `E2E_SettingsCommandLineParameters` (E2E-Testklasse)

- **Geänderte Methode `Einstellungen_SpeichertUndLaeadtCommandLineParameters_E2E`:** Die abschließende Zeile `new WindowsCredentialStore().DeleteCredential("Softwareschmiede.Codex.CommandLineParameters")` entfällt (Restore übernimmt das Aufräumen des via UI gespeicherten Werts).

### `E2E_AufgabeStarten` (E2E-Testklasse)

- **Geänderte Methode `AufgabeStarten_KlontRepositoryUndStartetCli_E2E`:** Keine Signatur-/Ablaufänderung nötig; das bisher fehlende Cleanup für `LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory` wird nun durch den zentralen Restore in `Dispose()` abgedeckt. Der `SetCredential`-Aufruf (Mid-Test-Korrektur) bleibt.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

Keine. Die Liste der verwalteten Credential-Schlüssel ist eine Test-interne Konstante in `WpfTestBase`, kein `appsettings`-Eintrag.

## Seiteneffekte und Risiken

- **Alle E2E-Tests (`[Collection("E2E")]`):** Erben von `WpfTestBase` und profitieren automatisch von Backup/Restore. Kein Test muss angepasst werden, außer dem Entfernen der nun redundanten manuellen `DeleteCredential`-Aufrufe. Verhalten bleibt identisch, nur der Store-Zustand nach dem Testlauf ist sauber.
- **Bisheriges pauschales Löschen entfällt:** `DeleteLocalDirectoryPluginCredentials()` löschte produktive `LocalDirectoryPlugin.*`- und `Codex.ExecutablePath`-Werte bedingungslos. Nach der Änderung werden diese Werte, falls produktiv gesetzt, korrekt wiederhergestellt statt gelöscht — eine Verbesserung, aber ein bewusst geänderter Nebeneffekt.
- **Vollständigkeit der Schlüsselliste:** Schreibt künftig ein neuer E2E-Test einen weiteren Credential-Schlüssel, muss dieser der verwalteten Liste hinzugefügt werden, sonst wird er nicht gesichert. Risiko wird durch die zentrale, gut sichtbare Konstante minimiert.
- **Kein Produktivcode betroffen:** `WindowsCredentialStore`, `PluginSettingsService`, `CodexPlugin`, `LocalDirectoryPlugin` bleiben unverändert.

## Umsetzungsreihenfolge

1. **`CredentialStoreSnapshot` anlegen**
   - Voraussetzungen: `ICredentialStore` (vorhanden in `Softwareschmiede.Plugin.Contracts`), `WindowsCredentialStore` (vorhanden). Testprojekt referenziert beide bereits.
   - Beschreibung: Neue Klasse in `src/Softwareschmiede.Tests/E2E/CredentialStoreSnapshot.cs`, die im Konstruktor über einer Schlüsselmenge `GetCredential` aufruft und die Werte merkt, und die eine `Restore()`-Methode bereitstellt (Wert zurückschreiben bzw. löschen, falls ursprünglich `null`).

2. **`WpfTestBase` auf Snapshot/Restore umstellen**
   - Voraussetzungen: `CredentialStoreSnapshot` aus Schritt 1.
   - Beschreibung: Verwaltete Schlüsselliste als Konstante hinzufügen; im Konstruktor `_credentialSnapshot` erzeugen; in `Dispose()` `DeleteLocalDirectoryPluginCredentials()` durch `_credentialSnapshot.Restore()` (im bestehenden `try/catch`) ersetzen; `DeleteLocalDirectoryPluginCredentials()` entfernen.

3. **Redundante manuelle Cleanups in den E2E-Tests entfernen**
   - Voraussetzungen: Schritt 2 (Restore greift zentral).
   - Beschreibung: `DeleteCredential`-Abschlusszeile in `E2E_TaskExecutionCommandLineParameters` und `E2E_SettingsCommandLineParameters` entfernen. `E2E_AufgabeStarten` benötigt keine Codeänderung (fehlendes Cleanup ist nun abgedeckt).

4. **Unit-Test für `CredentialStoreSnapshot` schreiben**
   - Voraussetzungen: `CredentialStoreSnapshot` aus Schritt 1; In-Memory-`ICredentialStore`-Fake (Muster vorhanden in `PluginSettingsServiceIntegrationTests`/`SettingsViewModelTests`).
   - Beschreibung: Test, der beweist, dass ein vorhandener Wert nach Änderung + `Restore()` wiederhergestellt und ein ursprünglich fehlender Wert nach Änderung + `Restore()` wieder gelöscht wird.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `Restore_StelltVorhandenenWertWiederHer` | `CredentialStoreSnapshotTests` (neu) | Ein vor dem Snapshot vorhandener Wert wird nach Überschreiben durch `Restore()` auf den Originalwert zurückgesetzt. |
| `Restore_LoeschtUrspruenglichFehlendenWert` | `CredentialStoreSnapshotTests` (neu) | Ein beim Snapshot nicht vorhandener Schlüssel, der während des Tests gesetzt wurde, wird durch `Restore()` wieder entfernt (`GetCredential` == `null`). |
| In-Memory-`ICredentialStore`-Fake (Hilfsklasse im Test) | `CredentialStoreSnapshotTests` (neu) | Dictionary-basierte `ICredentialStore`-Implementierung für Snapshot-Tests ohne Zugriff auf den realen OS-Store. |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `E2E_TaskExecutionCommandLineParameters` | Entfernen der redundanten `DeleteCredential`-Abschlusszeile (Cleanup jetzt zentral). |
| `E2E_SettingsCommandLineParameters` | Entfernen der redundanten `DeleteCredential`-Abschlusszeile. |
| `WpfTestBase` (Basisklasse aller E2E-Tests) | Konstruktor/`Dispose()` umgestellt; wirkt auf alle abgeleiteten E2E-Testklassen (Verhalten unverändert, nur sauberer Store-Zustand). |

### E2E-Tests (Pflicht)

Es gibt keine neue oder geänderte **Benutzerinteraktion** — die Änderung betrifft ausschließlich die Test-Isolation der Infrastruktur, kein Produktivverhalten. Die bestehenden E2E-Tests bleiben die Regressions-Abdeckung; sie belegen weiterhin den jeweiligen Happy Path und laufen nach der Änderung ohne Verunreinigung des produktiven Credential Stores.

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Gespeicherte Codex-`CommandLineParameters` beeinträchtigen den KI-Simulator-Start nicht — und der produktive Wert bleibt nach dem Test erhalten. | `E2E_TaskExecutionCommandLineParameters` (bestehend, Cleanup jetzt via Restore) | Produktive Codex-Parameter gehen durch den Testlauf nicht verloren. |
| CommandLineParameters werden über die Einstellungen gespeichert und wieder geladen — ohne dauerhafte Verunreinigung des Stores. | `E2E_SettingsCommandLineParameters` (bestehend) | Test-Werte bleiben nach dem Lauf nicht im produktiven Store zurück. |

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `E2E_TaskExecutionCommandLineParameters`, `E2E_SettingsCommandLineParameters` | Redundante `DeleteCredential`-Zeile entfernen (siehe oben). |

## Offene Punkte

Keine.

# Test-Infrastruktur: E2E-Test-Isolation durch Credential-Store-Cleanup

## Problembeschreibung

E2E-Tests (z. B. `E2E_TaskExecutionCommandLineParameters`, `E2E_SettingsCommandLineParameters`) speichern Plugin-Parameter wie `Softwareschmiede.Codex.CommandLineParameters` direkt im OS-weiten Windows Credential Store (`ICredentialStore` / `WindowsCredentialStore`). Diese Tests schließen mit einem manuellen `DeleteCredential()`-Aufruf, um den Test-Zustand aufzuräumen.

**Problem:** Wenn ein Test vor dem Cleanup abbricht — z. B. bei Assertion-Fehler, `TimeoutException` oder unerwarteter Prozess-Beendigung — bleibt der Test-Wert permanent gespeichert. Produktiv konfigurierte Parameter werden dadurch überschrieben oder gehen verloren.

## Lösung: Zentrale Backup/Restore-Mechanik

Implementiert nach dem **Memento-Pattern**: Eine neue Klasse `CredentialStoreSnapshot` sichert die aktuellen Werte aller verwalteten Credential-Schlüssel im Konstruktor und stellt sie garantiert wieder her — auch bei Test-Fehler.

### Komponenten

#### 1. `CredentialStoreSnapshot`
**Datei:** `src/Softwareschmiede.Tests/Infrastructure/Services/CredentialStoreSnapshot.cs`

Unveränderliche, fokussierte Klasse:
- **Konstruktor:** Empfängt `ICredentialStore` und eine Schlüsselliste; ruft `GetCredential(key)` für jeden Schlüssel auf und speichert Werte (inkl. `null`) in einem internen `Dictionary`.
- **`Restore()`-Methode:** Iteriert über alle gemerkten Schlüssel und stellt jeden Wert wieder her:
  - War der Schlüssel ursprünglich vorhanden → via `SetCredential(key, originalValue)` setzen
  - War der Schlüssel ursprünglich nicht vorhanden (`null`) → via `DeleteCredential(key)` entfernen

#### 2. Erweiterung von `WpfTestBase`

**Datei:** `src/Softwareschmiede.Tests/E2E/WpfTestBase.cs`

Neue konstante Liste `ManagedCredentialKeys` mit allen verwalteten Credential-Schlüsseln:
```csharp
private static readonly string[] ManagedCredentialKeys =
[
    "Softwareschmiede.Codex.CommandLineParameters",
    "LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory",
    "LocalDirectoryPlugin.WorkspaceMode",
    "LocalDirectoryPlugin.SourceDirectory",
    "Softwareschmiede.Codex.ExecutablePath",
];
```

Änderungen:
- **Konstruktor:** Erstellt `_credentialSnapshot = new CredentialStoreSnapshot(new WindowsCredentialStore(), ManagedCredentialKeys)` — der Snapshot wird sofort beim Test-Start angefertigt
- **`Dispose()`:** Ruft `_credentialSnapshot.Restore()` auf (im bestehenden `try/catch` mit `Debug.WriteLine`) — garantiert Wiederherstellung, auch bei Test-Fehler
- **Entfernte Methode:** `DeleteLocalDirectoryPluginCredentials()` — wurde durch zentrale Restore-Mechanik überflüssig

### Workflow pro E2E-Test

1. xUnit instanziiert Test-Klasse → `WpfTestBase`-Konstruktor läuft → `_credentialSnapshot` wird erzeugt
2. Snapshot ruft `GetCredential()` für jeden verwalteten Schlüssel auf → Werte (inkl. `null`) werden gespeichert
3. Test-Body läuft und setzt beliebige Credential-Werte (direkt oder über die gestartete App-UI)
4. Test endet (Erfolg oder Fehler) → xUnit ruft `Dispose()` auf
5. `Dispose()` ruft `_credentialSnapshot.Restore()` auf
6. Restore schreibt jeden ursprünglichen Wert zurück oder löscht Schlüssel (falls ursprünglich nicht vorhanden)
7. Produktiver Ausgangszustand des Credential Stores ist wiederhergestellt

## Einschränkungen und Erweiterbarkeit

### Vollständigkeit der Schlüsselliste
Die konstante `ManagedCredentialKeys` in `WpfTestBase` ist die single source of truth für alle verwalteten Schlüssel. Schreibt ein zukünftiger E2E-Test einen **neuen** Credential-Schlüssel:
- Der Schlüssel wird **nicht** automatisch überwacht, solange er nicht in die Liste aufgenommen wird
- Risiko: Test-Wert bleibt nach dem Test zurück

**Mitigation:** Die Konstante ist zentral, gut sichtbar und dokumentiert; Code-Review sollte sie beim Hinzufügen neuer E2E-Tests überprüfen.

### Parallelität
E2E-Tests laufen nicht parallel (Annotation `[Collection("E2E")]`), da jeder Test eine separate App-Instanz startet und die Testdatenbank prozessweit über eine Umgebungsvariable gepinnt ist. Der gemeinsame OS Credential Store wird dadurch nicht durch Parallelität gefährdet.

## Unit-Tests

**Datei:** `src/Softwareschmiede.Tests/Infrastructure/Services/CredentialStoreSnapshotTests.cs`

Tests gegen einen In-Memory-`ICredentialStore`-Fake (`InMemoryCredentialStoreForSnapshotTests`), ohne Zugriff auf den realen OS-Store:

1. **`Restore_StelltVorhandenenWertWiederHer`**
   - Setup: Store hat `"--produktiv-flag"`
   - Snapshot angefertigt
   - Test: Wert überschrieben mit `"--test-flag"`
   - Restore aufgerufen
   - Assertion: Wert ist wieder `"--produktiv-flag"`

2. **`Restore_LoeschtUrspruenglichFehlendenWert`**
   - Setup: Schlüssel existiert nicht
   - Snapshot angefertigt
   - Test: Wert gesetzt auf `"true"`
   - Restore aufgerufen
   - Assertion: Schlüssel ist wieder `null` (gelöscht)

## Auswirkungen auf E2E-Tests

### Betroffene Tests
- **`E2E_TaskExecutionCommandLineParameters`:** Entfernte Zeile `new WindowsCredentialStore().DeleteCredential("Softwareschmiede.Codex.CommandLineParameters")` — Cleanup erfolgt zentral
- **`E2E_SettingsCommandLineParameters`:** Kein manuelles Cleanup mehr nötig; Restore übernimmt Aufräumen des über UI gespeicherten Werts
- **`E2E_AufgabeStarten` und andere Tests:** Erben automatisch von der neuen Isolation; kein Change nötig

### Nebeneffekt: Besserung der Zustandswiederherstellung
**Bisheriges Verhalten:**
```csharp
protected void DeleteLocalDirectoryPluginCredentials()
{
    new WindowsCredentialStore().DeleteCredential("LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory");
    // ... weitere DeleteCredential-Aufrufe
}
```
Das Alte Verfahren **löschte** pauschal alle verwalteten Werte, auch wenn sie produktiv konfiguriert waren.

**Neues Verhalten:**
Restore stellt exakt den ursprünglichen Zustand wieder her — war ein Wert produktiv gespeichert, wird er wiederhergestellt statt gelöscht.

Beispiel:
- **Vorher:** Test setzt `Codex.CommandLineParameters = "--test"` → `Dispose()` löscht den Wert komplett
  - Wenn dieser Schlüssel vorher `"--produktiv"` war, ist er jetzt weg
- **Nachher:** Test setzt `Codex.CommandLineParameters = "--test"` → `Dispose()` schreibt `"--produktiv"` zurück
  - Produktive Konfiguration bleibt erhalten

## Sicherheitsüberlegungen

Die Credential-Store-Restore findet synchron in `Dispose()` statt — damit ist sie auch bei Thread-Abbruch oder GC-Finalization sicher. xUnit garantiert für jede Test-Instanz einen `Dispose()`-Aufruf, auch bei `Assert`-Fehler oder Exception.

Keine Authentifizierungsmechaniken betroffen — nur lokale Test-Cleanup-Logik.

## Zukünftige Verbesserungen

1. **Automatisches Hinzufügen neuer Schlüssel:** Ein Analyzer könnte vor neuen E2E-Tests warnen, die `SetCredential()` aufrufen, aber deren Schlüssel nicht in `ManagedCredentialKeys` sind.
2. **Audit-Logging:** Optional könnte jede Restore-Operation geloggt werden, um zu tracken, welche Tests welche Credential-Werte berühren.

---

*Reine Test-Infrastruktur-Verbesserung; kein Produktivcode betroffen.*

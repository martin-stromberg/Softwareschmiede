# Bestandsaufnahme: Verlorene Codex-Parameter (Credential-Store-Verschmutzung durch E2E-Tests)

Diese Analyse untersucht die bestehende Infrastruktur für die Anforderung „Credential-Store nicht verunreinigen — Test-Parameter isoliert speichern".

## Zusammenfassung

Die Anforderung betrifft **Test-Isolation** und **produktive Konfigurationssicherheit**. Folgende Komponenten sind relevant:

### Bestehende Komponenten ✓

1. **`ICredentialStore` Interface** — definiert die Verträge für `GetCredential()`, `SetCredential()`, `DeleteCredential()`
2. **`WindowsCredentialStore` Implementierung** — wraps Windows Credential Manager API via P/Invoke; speichert persistent mit `CredPersistLocalMachine`
3. **`PluginSettingsService`** — Service-Layer über ICredentialStore, entkoppelt Schlüsselformatierung
4. **Plugin-Integration** — `CodexPlugin` und `LocalDirectoryPlugin` nutzen ICredentialStore über DI um Einstellungen zu lesen
5. **E2E-Test-Infrastruktur** — Drei E2E-Tests (`E2E_TaskExecutionCommandLineParameters`, `E2E_SettingsCommandLineParameters`, `E2E_AufgabeStarten`) greifen direkt auf `WindowsCredentialStore` zu

### Probleme ✗

1. **Keine Backup/Restore in E2E_TaskExecutionCommandLineParameters** — setzt `Softwareschmiede.Codex.CommandLineParameters` ohne ursprünglichen Wert zu sichern
2. **Keine Backup/Restore in E2E_SettingsCommandLineParameters** — speichert via UI, löscht aber nur am Ende (fragile bei Test-Fehler)
3. **Kein Cleanup in E2E_AufgabeStarten** — setzt `LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory` ohne Delete (Test-Isolation verletzt)
4. **Keine zentrale Cleanup-Fixture** — `WpfTestBase` hat keine Credential-Store-Cleanup-Logik
5. **Keine Fehlerresistenz** — bei Test-Fehler vor Cleanup bleiben Test-Werte dauerhaft gespeichert

---

## Details

### Interfaces & Verträge
→ [Interfaces](inventory/interfaces.md)

- `ICredentialStore` — Drei öffentliche Methoden: GetCredential, SetCredential, DeleteCredential
- Keine eingebaute Rollback- oder Transaktions-Semantik
- Wird von Plugins über DI injiziert

### Logik-Komponenten
→ [Logik](inventory/logic.md)

- **`WindowsCredentialStore`** — P/Invoke-Wrapper auf advapi32.dll, speichert persistent
  - Keine Fehlerbehandlung bei DeleteCredential (stille Verwerfung)
  - SetCredential wirft Exception bei Fehler
- **`PluginSettingsService`** — Entkoppelt Schlüsselformatierung von Store-Zugriff
  - Keine Rollback-Mechanik
  - LogInformation bei Set/Delete (hilft bei Debugging)

### Test-Infrastruktur & Probleme
→ [Tests](inventory/tests.md)

**Drei problematische E2E-Tests:**
1. **`E2E_TaskExecutionCommandLineParameters`** — Setzt Test-Parameter für Codex, löscht nur am Ende (fragile)
2. **`E2E_SettingsCommandLineParameters`** — Speichert via UI, löscht aber nur am Ende (fragile)
3. **`E2E_AufgabeStarten`** — Setzt LocalDirectoryPlugin-Einstellung ohne **jedes** Cleanup (bricht Test-Isolation)

**Wunsch-Lösung laut Anforderung:**
- Option A (empfohlen): Backup-Restore vor/nach Test mit try/finally oder IAsyncLifetime
- Option B: Mock den ICredentialStore statt realen Zugriff
- Option C: Test-spezifischer Namespace (weniger robust)

---

## Verknüpfungen & Abhängigkeiten

```
PluginSettingsService
    └── injiziert ICredentialStore
        └── implementiert von WindowsCredentialStore
            └── genutzt von CodexPlugin, LocalDirectoryPlugin, E2E-Tests

E2E_TaskExecutionCommandLineParameters ─── direct instantiates ──→ WindowsCredentialStore
E2E_SettingsCommandLineParameters ────── direct instantiates ──→ WindowsCredentialStore
E2E_AufgabeStarten ────────────────── direct instantiates ──→ WindowsCredentialStore
```

---

## Offene Fragen (aus Anforderung)

1. **Wird `ICredentialStore` in `CodexPlugin` / `KiSimulatorPlugin` bereits via DI injiziert?** ✓
   - Ja: `CodexPlugin` wird mit `ICredentialStore credentialStore` injiziert (Zeile 57 in CodexPlugin.cs)
   - Ja: `LocalDirectoryPlugin` wird mit `ICredentialStore credentialStore` injiziert (Zeile 114 in LocalDirectoryPlugin.cs)

2. **Gibt es weitere E2E-Tests, die `WindowsCredentialStore.SetCredential` direkt aufrufen?** ✓
   - `E2E_SettingsCommandLineParameters.cs` (Line 58: DeleteCredential am Ende)
   - `E2E_AufgabeStarten.cs` (Line 45: SetCredential ohne Delete!)
   - Weitere Suche nach direktem Zugriff: Nur diese drei Tests instantiieren `new WindowsCredentialStore()`

3. **Sollten Credential-Manager-Einträge durch eine Fixture zentralisiert gereinigt werden?** (Design-Frage)
   - Technisch möglich: `IAsyncLifetime` im `WpfTestBase` oder separater Fixture
   - Würde Cleanup-Garantie bieten, reduziert Test-Code-Duplikation

4. **Ist es produktiv üblich, dass `Softwareschmiede.Codex.CommandLineParameters` bereits gespeichert ist?**
   - Anforderung sagt: „falls vorhanden", daher **muss restoriert** werden, falls vorher ein Wert gespeichert war


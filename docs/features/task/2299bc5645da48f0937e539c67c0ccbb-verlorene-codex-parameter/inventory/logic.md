# Logik-Komponenten

## `WindowsCredentialStore`
Datei: `src/Softwareschmiede/Infrastructure/Services/WindowsCredentialStore.cs`

Implementierung von `ICredentialStore` via Windows Credential Manager API (P/Invoke auf `advapi32.dll`). Speichert Credentials persistent im Windows Credential Store mit `CredPersistLocalMachine`.

### Öffentliche Methoden

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetCredential(string target)` | public | Ruft `CredReadW` via P/Invoke auf. Gibt `null` zurück, wenn Credential nicht existiert; `string.Empty` bei leerer Blob. |
| `SetCredential(string target, string value)` | public | Ruft `CredWriteW` auf, speichert den Wert mit `Persist = CredPersistLocalMachine`. Wirft `InvalidOperationException` bei Fehler. |
| `DeleteCredential(string target)` | public | Ruft `CredDeleteW` auf. Gibt keine Exception bei Fehler zurück (stille Fehlerverwerfung). |

### P/Invoke-Deklarationen

```csharp
[DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
private static extern bool CredRead(string target, int type, int flags, out IntPtr credential);

[DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
private static extern bool CredWrite([In] ref NativeCredential credential, uint flags);

[DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
private static extern bool CredDelete(string target, int type, int flags);

[DllImport("advapi32.dll", SetLastError = true)]
private static extern void CredFree(IntPtr buffer);
```

### Besonderheiten

- **Keine Rollback-Mechanik**: Wenn `SetCredential` erfolgreich ist, gibt es keine eingebaute Möglichkeit, zum vorherigen Zustand zurückzukehren.
- **Keine Transaktionen**: Mehrere SetCredential-Aufrufe sind nicht atomar.
- **Stille Fehler bei DeleteCredential**: Die Methode ignoriert Fehler (ruft nur `CredDelete` auf, prüft aber nicht auf Rückgabewert).
- **Persistent gespeichert**: Werte bleiben über Prozessgrenzen hinweg erhalten.

---

## `PluginSettingsService`
Datei: `src/Softwareschmiede/Application/Services/PluginSettingsService.cs`

Service-Layer für Plugin-Einstellungsverwaltung über den Credential Store. Entkoppelt die Schlüsselformatierung (`<PluginPrefix>.<FieldKey>`) von der Verwendung.

### Öffentliche Methoden

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetAllPlugins(IEnumerable<IGitPlugin>, IEnumerable<IKiPlugin>)` | public | Aggregiert Git- und KI-Plugins in einer `IReadOnlyList<IPlugin>`. |
| `GetValue(IPlugin plugin, PluginSettingField field)` | public | Liest einen Einstellungswert. Konstruiert Schlüssel als `<plugin.PluginPrefix>.<field.Key>`. |
| `SetValue(IPlugin plugin, PluginSettingField field, string value)` | public | Speichert einen Einstellungswert mit LogInformation. |
| `DeleteValue(IPlugin plugin, PluginSettingField field)` | public | Löscht einen Einstellungswert mit LogInformation. |
| `HasValue(IPlugin plugin, PluginSettingField field)` | public | Gibt an, ob für ein Feld bereits ein Wert (nicht empty/null) gespeichert ist. |
| `BuildKey(IPlugin plugin, PluginSettingField field)` | private | Hilfsmethode zum Konstruieren des Schlüssels. |

### Abhängigkeiten (via DI-Konstruktor)

- `ICredentialStore _credentialStore`: Der zugrunde liegende Credential Store (typischerweise `WindowsCredentialStore`)
- `ILogger<PluginSettingsService> _logger`: Logging für Set/Delete-Operationen (LogInformation)

### Besonderheiten

- **Kein Rollback**: Die Methoden haben keine Rollback- oder Transaktions-Mechanik.
- **Logging bei Änderungen**: SetValue und DeleteValue loggen ihre Operationen (hilft bei Debugging), aber GetValue loggt nur Debug-Level.
- **Keine Validierung**: Die Methoden prüfen nicht, ob der gespeicherte Wert gültig ist (z.B. ob CommandLineParameters syntaktisch korrekt ist).


# Tasks: Verlorene Codex-Parameter (Credential-Store-Isolation der E2E-Tests)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Testinfrastruktur | `CredentialStoreSnapshot` (Memento) in `src/Softwareschmiede.Tests/E2E/CredentialStoreSnapshot.cs` anlegen: Werte einer Schlüsselmenge via `GetCredential` sichern | Offen | — |
| 2 | Testinfrastruktur | `CredentialStoreSnapshot.Restore()` implementieren: Wert zurückschreiben bzw. löschen, falls ursprünglich `null` | Offen | — |
| 3 | Testinfrastruktur | `WpfTestBase`: Konstante Liste der verwalteten Credential-Schlüssel hinzufügen | Offen | — |
| 4 | Testinfrastruktur | `WpfTestBase`-Konstruktor: `_credentialSnapshot` über `WindowsCredentialStore` erzeugen | Offen | — |
| 5 | Testinfrastruktur | `WpfTestBase.Dispose()`: `DeleteLocalDirectoryPluginCredentials()` durch `_credentialSnapshot.Restore()` (im bestehenden `try/catch`) ersetzen | Offen | — |
| 6 | Testinfrastruktur | `WpfTestBase`: obsolete Methode `DeleteLocalDirectoryPluginCredentials()` entfernen | Offen | — |
| 7 | E2E-Tests | `E2E_TaskExecutionCommandLineParameters`: redundante `DeleteCredential`-Abschlusszeile entfernen | Offen | — |
| 8 | E2E-Tests | `E2E_SettingsCommandLineParameters`: redundante `DeleteCredential`-Abschlusszeile entfernen | Offen | — |
| 9 | Tests | In-Memory-`ICredentialStore`-Fake für Snapshot-Unit-Tests bereitstellen | Offen | — |
| 10 | Tests | `CredentialStoreSnapshotTests.Restore_StelltVorhandenenWertWiederHer` schreiben | Offen | — |
| 11 | Tests | `CredentialStoreSnapshotTests.Restore_LoeschtUrspruenglichFehlendenWert` schreiben | Offen | — |

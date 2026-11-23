# Bestandsaufnahme: Wöchentliche NuGet-Paketprüfung (Issue #469, 2026-KW48)

Analysiert wurde der komplette NuGet-Paketbestand der `Softwareschmiede.slnx`
(13 Projektdateien, direkte + transitive Pakete) sowie die mitbetroffenen
CI-/Dokumentationsartefakte — bezogen auf die Anforderung in `requirement.md`.

## Zusammenfassung

- **Direkte Pakete:** 32 `PackageReference`s in 4 von 13 Projektdateien
  (`Softwareschmiede`, `Softwareschmiede.App`, `Softwareschmiede.Tests`,
  `Softwareschmiede.IntegrationTests`). `Softwareschmiede.Plugin.Contracts` und alle
  8 Plugin-Projekte deklarieren **keine** eigenen Pakete — entgegen der Annahme in der
  Anforderung ziehen die Plugins auch **keine transitiven NuGet-Pakete** ein
  (`ILogger<T>` kommt aus dem .NET-11-Shared-Framework). Details: [packages.md](inventory/packages.md).
- **Updates (direkte Pakete):** nur zwei — `Serilog.Extensions.Hosting` 10.0.0 → 11.0.0
  (Major, App) und `Microsoft.Extensions.TimeProvider.Testing` 11.0.0 → 11.0.1
  (Patch, Tests). Alle anderen direkten Referenzen sind auf dem neuesten stabilen Stand.
- **Updates (transitiv):** `Serilog` 4.3.0 → 4.4.0 und `Serilog.Extensions.Logging`
  10.0.0 → 11.0.0 folgen dem `Serilog.Extensions.Hosting`-Update; `SQLite` 3.53.4 →
  3.54.1 ist nur über ein `SQLitePCLRaw.lib.e_sqlite3`-Update hebbar (kein stabiler
  Update verfügbar, nur Prerelease 3.53.4.1-pre1); übrige Transitiva sind
  Dev-Time-/Test-Transitiva (`Microsoft.CodeAnalysis.*`, `Microsoft.Testing.*` u. a.).
- **Schwachstellen:** keine — `dotnet list package --vulnerable --include-transitive`
  sauber über alle 13 Projekte (identisch zum CI-Gate `security-scan`).
- **Veraltete/abgekündigte Pakete:** keine — `dotnet list package --deprecated` sauber.
- **`THIRD_PARTY_LICENSES.md` nachweislich veraltet** (Stand 2026-07-16): führt u. a.
  EF-Core/`Microsoft.Extensions.*` 10.0.9 statt 11.0.0, `xunit` 2.9.3 statt `xunit.v3`
  4.0.3, `Xunit.SkippableFact` (gar nicht mehr referenziert) — siehe
  [infrastruktur.md](infrastruktur.md).
- **Kein CPM, keine `NuGet.config`** — Versionen stehen dezentral in den `*.csproj`;
  gekoppelte Gruppen (EF-Core, Test-Stack, `Microsoft.Extensions.*`, FlaUI,
  `SQLitePCLRaw.lib.e_sqlite3`) sind in [packages.md](inventory/packages.md#versionsgekoppelte-gruppen-müssen-konsistent-angehoben-werden)
  dokumentiert.

**Test-Ausgangszustand:** Voller Build sauber (0 Warnungen/0 Fehler); stabile Lane
`Category!=OsInterface` 2800/2803 bestanden (3 OS-bedingt übersprungen),
`Softwareschmiede.IntegrationTests` 436/436 bestanden — **keine bestehenden
Testfehler**. Die `OsInterface`-Lane (FlaUI-E2E/ConPTY) wurde in dieser Sandbox nicht
ausgeführt (kein Vorher-Nachweis). Nachweis: [tests.md](inventory/tests.md).

## Details

- [Paketbestand (direkte Referenzen, gekoppelte Gruppen, relevante Transitiva)](inventory/packages.md)
- [Audit-Ergebnis (Outdated / Vulnerable / Deprecated + nuget.org-Metadaten)](inventory/audit.md)
- [Infrastruktur-Artefakte (CI-Workflows, security-scan, THIRD_PARTY_LICENSES.md, Code-Berührpunkte)](inventory/infrastruktur.md)
- [Tests (Ausgangszustand, Läufe, Lücken)](inventory/tests.md)

Nachweise (Rohlogs): [inventory/test-results/](inventory/test-results/)

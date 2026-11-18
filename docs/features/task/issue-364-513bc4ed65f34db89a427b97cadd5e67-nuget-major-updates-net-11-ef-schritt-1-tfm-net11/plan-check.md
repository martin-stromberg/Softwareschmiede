# Plan-Check – Schritt 1 (TFM .NET 11 + CI-Toolchain)

**Status:** Plan vollständig

## Abgleich Anforderung ↔ Plan

| Anforderung | Planabdeckung |
|-------------|---------------|
| 13 csproj TFM-Umstellung | Änderung 1 (vollständige Dateiliste in inventory.md) |
| 8 Plugin-Copy-Pfade in App.csproj | Änderung 2 |
| TargetPlatformMinVersion unverändert | Änderung 2 (explizit) + erlaubter Resttreffer |
| 9× dotnet-version → 11.0.x | Änderung 3 (alle 5 Dateien benannt) |
| Schrittnamen/Kommentare | Änderung 3 |
| Build/Test/Format-Verifikation | Szenarien A–D |

## Vollständigkeit

- Jede Datei der Bestandsaufnahme ist genau einer Änderung zugeordnet (13 csproj → 1,
  App-Pfade → 2, 5 CI-Dateien → 3).
- Testbedarf: kein fachlicher Code geändert → Bestandstests reichen; E2E-/Abnahme-
  Einschränkung der Sandbox ist als solche deklariert (Szenario B + E2E-Hinweis).
- Keine ungeklärten offenen Punkte; keine Scope-Ausschlüsse.

## Hinweise

- Die Trennung Paket-Upgrade (Schritt 2) vs. TFM (Schritt 1) ist projektplankonform.

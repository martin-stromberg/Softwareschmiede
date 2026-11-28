# Enums

**Es existiert kein Enum für eine Theme-/Anzeige-Präferenz** — die Anforderung sieht ein neues Enum (z. B. `ThemePreference` mit `Auto`/`Light`/`Dark`) vor. Bestehende Enums im Projekt als Konventionsreferenz:

## `StatusServerUpdateChannel`

Datei: `server/Softwareschmiede.StatusServer/Update/StatusServerUpdateChannel.cs`

| Wert | Bedeutung |
|------|-----------|
| `Stable` | Nur stabile Releases (`vX.Y.Z`); persistiert als `stable` |
| `ReleaseCandidate` | Stabile + Vorabversionen; persistiert als `prerelease` |

## `StatusServerAutomationMode`

Datei: `server/Softwareschmiede.StatusServer/Update/StatusServerAutomationMode.cs`

| Wert | Bedeutung |
|------|-----------|
| `Off` | Keine Automatisierung; persistiert als `off` |
| `CheckOnly` | Nur prüfen/melden; persistiert als `check` |
| `CheckAndInstall` | Prüfen + installieren; persistiert als `install` |

Konvention: Beide Enums sind einfache Wert-Enums (erster Wert = `0` = fachlicher Default), die über einen Codec (`StatusServerUpdateSettingsCodec.TryParseChannel`/`FormatChannel` etc.) auf kurze String-Schlüssel gemappt und in `ServerSettings` persistiert werden. Eine `ApplicationUser`-Spalte für ein neues Enum würde EF-seitig standardmäßig als `INTEGER` (Ordinal) persistiert, sofern kein `EnumToStringConverter` konfiguriert wird — ein solcher ist in `StatusServerDbContext.OnModelCreating` aktuell nicht vorgesehen.

## `AutoUpdateState` (msTools.Updater, extern)

In `AdminPagesComposer.UpdateStateText` auf deutsche Texte gemappt — Bibliotheks-Enum, nicht teil des Repos.

Keine `enum`-artigen Konstrukte für Rollen: `StatusRoles` ist eine `static class` mit `const string`-Werten (`Administration`, `Ueberwacher`) plus `PageAccessPolicy`/`AdminOnlyPolicy`-Policy-Namen und `DisplayName`-Mapping.

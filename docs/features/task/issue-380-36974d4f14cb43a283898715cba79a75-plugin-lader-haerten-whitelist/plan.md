# Umsetzungsplan — Plugin-Lader härten (Issue #380)

## Entscheidung

Whitelist der bekannten Plugin-Assembly-Namen (hartkodiert, integrity-relevant — eine Konfigurationsdatei
im nutzerschreibbaren Installationsverzeichnis wäre genauso fälschbar wie die DLL selbst).
Strong-Name-Prüfung entfällt: keine Signierung im Repo, .NET Core verifiziert Strong-Names beim Laden
nicht; stattdessen wird der *echte* Assembly-Name (Metadaten, ohne Laden) gegen die Whitelist geprüft —
das verhindert triviale Maskerade per Datei-Umbenennung.

## Änderungen in `PluginManager.cs`

1. `AllowedPluginAssemblyNames` (`HashSet<string>`, OrdinalIgnoreCase): die 8 First-Party-Namen.
2. Kandidatenauswahl in `DiscoverPlugins()`:
   - Top-Level `plugins/*.dll`: alle Dateien sind Kandidaten (Legacy-Layout).
   - Unterordner `plugins/<dir>/`: nur `<dir>.dll` ist Kandidat; übrige DLLs sind Dependencies und
     werden nicht gescannt (verhindert Warn-Flut, reduziert Ladequell).
3. `IsAllowedPlugin(dllPath)` vor `LoadPluginsFromDll`:
   - Dateiname (ohne Ext.) in Whitelist? Nein → `LogWarning` „nicht auf der Plugin-Whitelist" + skip.
   - `AssemblyLoadContext.GetAssemblyName(path).Name` == Whitelist-Name? Nein (Maskerade/ungültig)
     → `LogWarning` + skip. `BadImageFormatException` → ebenfalls Warn-Log + skip.
4. Test-Modus-Filter bleibt als zusätzlicher Filter vor der Whitelist-Prüfung bestehen.

## Tests (`PluginManagerTests.cs`)

- Unbekannte DLL auf Top-Level (gültige Assembly unter fremdem Namen) → nicht geladen, Warn-Log verifiziert.
- Umbenannte Plugin-DLL (echte GitHub-Assembly als `Foo.dll` → Assembly-Name-Mismatch ist hier nicht
  der Fall — stattdessen: fremde Datei `Foo.dll` mit fremdem Assembly-Namen, z. B. kopierte
  `Moq.dll`) → nicht geladen + Warn.
- Maskerade: fremde Assembly als `Softwareschmiede.Plugin.GitHub.dll` → Dateiname passt,
  Assembly-Name nicht → Warn + skip.
- Unterordner: `plugins/Softwareschmiede.Plugin.ClaudeCli/Softwareschmiede.Plugin.ClaudeCli.dll` lädt;
  `<Ordner>/<fremd>.dll` und unbekannter Ordner `<Foo>/<Foo>.dll` laden nicht (letzteres mit Warn).

## Dokumentation

- `docs/help/plugins/beschreibung.md` bzw. `ablauf-technisch.md`: Abschnitt „Sicherheit/Whitelist" —
  nur First-Party-Plugin-Assemblies werden geladen; unbekannte DLLs → Warn-Log, kein Laden;
  Dependency-Hijacking-Grenze kurz benennen.
- `docs/help/plugins/business-rules.md`: Regel „Nur whitelisted Plugin-Assemblies werden geladen".
- README: kein Eintrag zum Plugin-Lader vorhanden → nur prüfen, ggf. kein Change nötig.

## Offene Punkte

(keine)

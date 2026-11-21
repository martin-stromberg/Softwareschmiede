# Planprüfung – Codesprache finalisieren (Durchlauf 2)

## Ergebnis

**Status:** Projektplan vollständig

Alle in Durchlauf 1 beanstandeten Lücken sind im aktualisierten Plan konkret und verifizierbar geschlossen — die Schließungen wurden jeweils gegen den tatsächlichen Codebestand gegengeprüft (z. B. 44 `*.Designer.cs` unter `src/Softwareschmiede/Migrations/`, `TerminalSessionOptions.AufzeichnungByteBudget` als einzige deutsche konfigurationsgebundene Property, vollständige Settings-Key-Konstantenliste in `AppEinstellungService.cs:25–121`).

## Nachprüfung Befunde Durchlauf 1

| Befund | Status | Nachweis im Plan |
|---|---|---|
| `SoftwareschmiededDbContext`↔`Migrations/**`-Widerspruch (~40 `*.Designer.cs` mit `[DbContext(typeof(SoftwareschmiededDbContext))]`) | geschlossen | Entscheidung 5: mechanische Referenzkorrektur des Kontexttyps „samt using" explizit deklariert; Migrationsklassennamen, `[Migration]`-IDs und Up/Down-Rümpfe bleiben eingefroren (stringbasierte Entity-Namen in `BuildTargetModel` sind korrekterweise nicht betroffen — kein Compile-Bezug) |
| `ModelSnapshot`-Update als Tooling-Ausnahme | geschlossen | Entscheidung 2 + 5: `dotnet ef`-generiertes Snapshot-Update als „legitime Ausnahme unter `Migrations/`" deklariert; leere Migration als Schema-Stabilitätsnachweis |
| `DiffCache.CachedData`-Entscheidung | geschlossen | Entscheidung 2: begründeter Verzicht auf Pinning (TTL-Cache, regenerierbar, tolerant als Miss behandeln) |
| `updates.pfad`-Werte + deutsche Settings-Keys | geschlossen | Entscheidung 2: `"Produktivversionen"`/`"Vorabversionen"` gepinnt; Key-Literale bleiben, nur C#-Konstantennamen werden englisch (Liste gegen `AppEinstellungService.cs` verifiziert vollständig; numerisch persistierte Enums `ScmBackmergeModus`/`ScmKontoModus`/`ScmTaskCompletionPruefmodus`/`UpdateMode` sind rename-sicher) |
| `ScmIncomingEvent.Status`-Literalmenge (~9 Werte) | geschlossen | Entscheidung 2: vollständige Aufzählung aller 9 Literale, identisch zur verifizierten Codemenge |
| `PluginPrefix`-Literal `"Softwareschmiede.KiSimulator"` | geschlossen | Entscheidung 2: als zu pinnendes Legacy-Literal benannt (persistiert in `Aufgabe.KiPluginPrefix`, `GitRepository.PluginTyp`, `PluginKonfiguration.PluginTyp`, Settings-Keys) |
| `Terminal:AufzeichnungByteBudget`-Konfigurationsschlüssel | geschlossen | Entscheidung 2: `[ConfigurationKeyName]`-Pinning für `TerminalSessionOptions` — verifiziert als einzige deutsche Options-Property (`DirectoryStructureOptions`/`ScmEventOptions` bereits englisch) |
| `ScmKontoModus`/`ScmResponseKind` numerische Serialisierung | geschlossen | Entscheidung 2: Property-Level-`JsonPropertyName` genügt, Member-Attribute wirkungslos; gepinnt wird der emittierte Wire-Name (`aufgabeId`) |
| `HasFilter("[Rolle] = 'ReviewSource'")` + `HasDatabaseName`-Indexnamen | geschlossen | Entscheidung 2: explizit gepinnt, „Rename-Skripte dürfen solche Literale nicht anfassen" |
| XAML `ConverterParameter`-Enum-Namen (`Enum.Parse` in `AppConverters.cs`) | geschlossen | Schritt 4: alle sechs Werte und der Parse-Mechanismus benannt, gleiche Commit-Einheit |
| `README.md`/`CLAUDE.md`-Bezeichner-Synchronisation | geschlossen | Schritt 6: `README.md` (~50 Verweise konkret benannt), `CLAUDE.md`, zusätzlich `.github/copilot-instructions.md` mit aufgenommen |

## Verbleibende Lücken

- Keine.

## Hinweise

- **Deutsche `x:Key`-Ressourcenschlüssel** sollten in Schritt 4 ergänzend benannt werden (nicht blockierend): `AufgabenKachelTemplate`/`AufgabenKachelInhaltTemplate` (`Controls/ActiveTasksListControl.xaml:6,73`), `StatusNeuColor`, `StatusArbeitsverzeichnisColor`, `StatusGestartetColor`, `StatusInArbeitColor`, `StatusWartendColor`, `StatusBeendetColor`, `StatusArchiviertColor` (`Themes/LightTheme.xaml:25–31`, `Themes/DarkTheme.xaml:25–31`), `FileExplorerBaumItemContainerStyle`/`StandardBaumItemContainerStyle` (`Views/FileExplorerView.xaml:34,37`). Sie sind string-keyed via `{StaticResource}`/`{DynamicResource}` referenziert — ein skriptgestütztes C#-Rename übersieht sie typischerweise. Faktisch abgesichert durch das Schritt-4-Akzeptanzkriterium und den Rest-Sweep in Schritt 5; eine fehlgeschlagene StaticResource-Auflösung ist zudem laut (XamlParseException), nicht still.
- Übrige Prüfläufe ohne Befund: `.github/workflows/*.yml` nur deutsche Prosa; `scripts/*.ps1`, `publish.ps1`, `start.ps1`, `.githooks/*`, `tools/track_usage.sh` ohne deutsche Bezeichner; keine `.resx`/`.razor`; `site/**` ohne Code-Bezeichner; Plugin-Setting-Keys und weitere Plugin-Prefixe bereits englisch; `version.json`/`tagName` via `JsonPropertyName` gepinnt; `IssueReferenz.LabelsJson` serialisiert nur String-Listen.
- `Enum.TryParse`-Persistenz in `PullRequestMonitoringService.cs:273–274` (`PullRequestCompletionStrategy`, `PullRequestMergeMethod`) unproblematisch — bereits englisch.

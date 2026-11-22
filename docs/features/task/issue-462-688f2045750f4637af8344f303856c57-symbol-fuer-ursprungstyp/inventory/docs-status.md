# Dokumentations-Ist-Stand — Symbol für Auftragsart (Issue #462)

Bestehende `docs/help/`-Abdeckung der Auftragsart und der Symbolzeile; Grundlage für die in der Anforderung genannten Doc-Updates.

## `docs/help/plugins/github-ereignisse/business-rules.md`

- Abschnitt **„Auftragsart der Aufgabe"** (Z. 97–99): beschreibt `WorkItem.Kind` (persistiert als `Aufgaben.Auftragsart`) mit den fünf fachlichen Werten — `Manuell`, `Anweisung`, `Issue-Aufbereitung`, `Pull Request`, `Alert`. Erwähnt die Typ-Markierung auf den Kacheln der aktiven Aufgabenliste und des Dashboards sowie die Gruppierung im Modus „Art" („Ohne Auftragsart" am Ende); erläutert die Trennung von `Purpose` und den Typwechsel Aufbereitung → `Anweisung` sowie die Bestands-Migration. **Enthält kein Symbol-Mapping** — hier wäre die Symboltabelle zu ergänzen.

## `docs/help/aufgaben/ablauf-technisch.md`

- Abschnitt **„Seitenleisten-Rendering (MainWindow.xaml / ActiveTasksListControl)"** (Z. 820–843): beschreibt die Symbolzeile (Z. 840) mit SCM-Glyph, KI-Glyph, Status-Glyph und To-do-Button — **das Auftragsart-Badge fehlt dort bereits heute** (Doc-Lücke aus der Vorarbeit, nicht erst durch dieses Issue). Ebenso nennt „Anzeigemodus und Gruppierung" (Z. 845–864) nur drei Modi (Datum/Repository/KI) — der „Art"-Modus ist dort nicht dokumentiert.
- Abschnitt **„Plugin-Symbol-Auflösung und Mapping"** (Z. 883–908): beschreibt `IPlugin.PluginSymbol`, `IPlugin.FallbackSymbol`, `ResolvePlugin` und `MapActiveWorkItemPanelItem` — Vorbild-Abschnitt für eine Symbol-Mapping-Beschreibung. Die Aufzählung der gemappten Felder (Z. 896–900) enthält weder `Kind`/`KindGroupName`/`KindLabel` noch ein Symbol.

## `docs/help/plugins/api.md`

- Z. 12–13: `PluginSymbol`-/`FallbackSymbol`-Tabelleneintrag — das etablierte Muster für Unicode-Glyphen auf der Kachel (Default-Interface-Member, Binärkompatibilität).

## `docs/help/plugins/github-ereignisse/ereignisablaeufe.md`

- Z. 41, 67, 87, 129: erwähnt die Auftragsart im Ereignisablauf (Anlage mit `Anweisung`/`Pull Request`, Aufbereitungsaufgabe, getrennte Aufgaben unterschiedlicher Art).

## `docs/help/aufgaben/api.md`

- Z. 575: `IPlugin.PluginSymbol`-Beschreibung (Default-Member → `FallbackSymbol = "◻"`, `virtual` an `GitPluginBase`/`CliAiPluginBase`, alle Repo-Plugins überschreiben).

## `docs/help/aufgaben/ablauf-anwender.md` / `beschreibung.md` / `business-rules.md`

- Keine Erwähnung des Auftragsart-Badges; nur das Ribbon-To-do-Badge ist beschrieben.

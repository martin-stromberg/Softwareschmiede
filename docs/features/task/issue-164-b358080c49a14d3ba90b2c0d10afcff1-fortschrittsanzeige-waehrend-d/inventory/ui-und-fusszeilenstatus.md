# UI und Fusszeilenstatus

## Vorhandene Fusszeile

Die Aufgabendetailansicht besitzt eine eigene Statusleiste in `TaskDetailView.xaml`, Zeilen 423-454. Sie besteht aus:

- links: `StatusIndicatorControl` mit `AufgabeStatus` und `AufgabeBranchName`,
- Mitte: `TextBlock` mit `Text="{Binding CliStatusText}"`,
- rechts: aktiver CLI-Name ueber `AktiverCliName`.

Der zentrale Akzeptanzpunkt der Anforderung passt am besten in den mittleren TextBlock, weil dieser bereits als laufender Aktivitaets-/Statusbereich genutzt wird.

## Aktuelle Statusquelle

`TaskDetailViewModel` haelt den mittleren Fusszeilentext in `_cliStatusText`, initial `CLI inaktiv` (`TaskDetailViewModel.cs`, Zeile 61). Die oeffentliche Property `CliStatusText` ist private set und triggert PropertyChanged (`TaskDetailViewModel.cs`, Zeilen 188-193).

Der Text wird aktuell ausschliesslich aus dem CLI-Runtime-Status berechnet:

- `AttachCliStatusSession(...)` verbindet oder trennt die PseudoConsole-Session (`TaskDetailViewModel.cs`, Zeilen 1453-1475).
- `OnCliRuntimeStatusChanged(...)` aktualisiert den Text per Dispatcher (`TaskDetailViewModel.cs`, Zeilen 1477-1479).
- `UpdateCliStatusText(...)` mappt Runtime-Status auf `CLI-Status: Ausfuehrung laeuft`, `CLI-Status: Wartet auf Eingabe`, `CLI inaktiv` oder unbekannt (`TaskDetailViewModel.cs`, Zeilen 1482-1490).

## Bestehende Ladezustandslogik

`TaskDetailViewModel` besitzt `IsLoading`, aber dieser Zustand steht fuer das Laden der Aufgabe selbst, nicht fuer die Repository-Vorbereitung. Beim Laden werden Aufgabe, IDE-Fallback, CLI-Running-State und PseudoConsole-Session aktualisiert (`TaskDetailViewModel.cs`, Zeilen 564-590). `IsLoading` ist daher semantisch nicht geeignet, um den Klonstatus direkt anzuzeigen.

## UI-Verhalten waehrend des Starts

Der Start-Command wartet asynchron auf den gesamten kombinierten Ablauf. Waehrend dieser Zeit:

- Die Aufgabe hat weiterhin Status `Neu`, bis `FinalizeStartAsync()` `AufgabeService.StartenAsync(...)` aufruft.
- Es existiert noch keine PseudoConsole-Session, weil die CLI erst nach dem Repository-Setup startet.
- `CliStatusText` bleibt ohne Aenderung bei `CLI inaktiv`, wodurch fuer laengere Klons der Eindruck eines Stillstands entstehen kann.

## Umsetzungsspielraum

Moegliche UI-Ansaetze:

- Minimal: `CliStatusText` vor dem Repository-Start auf `Bereit Repository vor...` setzen und danach die bestehende CLI-Statusberechnung wiederherstellen.
- Sauberer: eine allgemeinere Property wie `FooterStatusText` einfuehren und den XAML-TextBlock darauf binden; `CliStatusText` kann dann entweder bleiben oder in Tests kontrolliert abgeloest werden.
- Service-basiert: ein eigener globaler Arbeitsstatusdienst, falls der Status auch ausserhalb der TaskDetailView sichtbar sein soll. In der aktuellen Codebasis wurde kein solcher zentraler Dienst gefunden.

Der Minimalansatz erfuellt die Akzeptanzkriterien fuer die vorhandene Fusszeile mit kleinem Testaufwand, traegt aber den historisch engen Namen `CliStatusText` weiter.

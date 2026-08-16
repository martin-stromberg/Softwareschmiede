# Tests und E2E-Abdeckung

## Vorhandene Tests

Relevante Unit- und Integrationstests liegen unter `src/Softwareschmiede.Tests/App/ViewModels`, `src/Softwareschmiede.Tests/Application/Services`, `src/Softwareschmiede.Tests/E2E` und `src/Softwareschmiede.IntegrationTests/Services`.

- `TaskDetailViewModelTests*` pruefen Laden, Status-/Branch-Anzeige, CLI-Startpfade, Stop-/Neustartverhalten und verschiedene UI-Aktionen.
- `ProjectDetailViewModelTests*` pruefen Projekt- und Aufgabenlisten sowie Aufgabenanlage in der Projektansicht.
- `AufgabeServiceTests*` und `AufgabeServiceTests_AktiverLauf` decken Erzeugung, Status, Lauf-ID, Heartbeat und Laufstatus ab.
- `KiAusfuehrungsServiceTests*` pruefen Prozessstart, Stoppen und Exit-Behandlung.
- `AufgabeRecoveryServiceTests` prueft Recovery-Kandidaten und manuelle Recovery fuer Gesamtstatus `Gestartet`/`Wartend`.
- `E2E_CreateNewTaskNavigation` erwartet aktuell nach dem Speichern die Rueckkehr zur Projektansicht und muss fuer die neue Navigation angepasst werden.
- `E2E_ConPtyLifecycle` prueft Start, sichtbaren Stoppen-Button und dass Stoppen `IsCliRunning` entfernt, waehrend der Gesamtstatus `Gestartet` bleibt.
- Weitere E2E-Szenarien wie `E2E_FileExplorer` starten Aufgaben und wechseln zwischen Info-, Datei- und CLI-Ansicht.

## Abdeckungsluecken zur Anforderung

Es ist kein Test erkennbar, der einen eigenstaendigen persistenten Ausfuehrungsstatus mit `nicht gestartet`, `aktiv` und `beendet` prueft. Ebenso fehlen gezielte Szenarien fuer:

- Speichern einer neuen Aufgabe mit geoeffnet bleibender TaskDetailView.
- Wechsel zu einer anderen Aufgabe und Rueckkehr bei aktivem Lauf mit Wiederanbindung derselben CLI.
- Programmneustart beziehungsweise erneutes Laden einer Aufgabe mit aktivem Lauf.
- Wechsel und erneutes Laden nach beendetem KI-Lauf ohne Autostart.
- Erneutes explizites Starten eines beendeten KI-Laufs.
- Verbot des Startens bei Gesamtstatus `Beendet`.
- Persistenz- und Migrations-Backfill des neuen Status.
- Abgrenzung von Stoppen gegen endgueltiges Beenden inklusive Klonloeschung.

## Vorgabe fuer diesen Schritt

Es wurden keine Tests ausgefuehrt. Die Liste beschreibt nur die statisch vorhandene Abdeckung und die daraus abzuleitenden Testfaelle.

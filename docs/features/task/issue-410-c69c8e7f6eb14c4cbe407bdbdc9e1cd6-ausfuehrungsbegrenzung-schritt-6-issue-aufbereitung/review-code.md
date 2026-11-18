# Code-Review – Schritt 6: Issue-Aufbereitung ausführen

Geprüfter Stand: Arbeitsbaum des Feature-Branches (uncommitted Diff,
~700 geänderte Zeilen über 17 Dateien).

## Kernpfad `ScmEventOrchestrator.cs`

- **Reihenfolge korrekt:** Der `Zweck`-Guard (Z. 137) verzweigt vor der
  generischen Trigger-Validierung — der rohe Issue-Body kann nicht als
  generischer Prompt enden.
- **Spiegelbildliche Struktur** zum generischen `DispatchAsync`:
  Host-Outbox-Nachzug (nur nicht-`ai:`-Aktionen) → früher `Verarbeitet`-Exit →
  Versuchs-Reconcile → Fälligkeits-/Blockierungsprüfung → Schalter → Provider-
  Checks. Erprobte Invarianten (Kollisionserkennung, `ZustellungUngewiss`,
  `VerarbeitungUngeklärt`, Timeouts) gelten unverändert.
- **Atomare Limit-Reservation** (`PruefeUndReserviereStartPlatzAsync`, Z. 698):
  Reservation deckt genau den CLI-Start ab und wird per `using` freigegeben —
  kein Slot-Leck bei Startfehlern, kein Start ohne Platz.
- **Plugin-Fallback ehrlich:** `IgnoredPluginPrefixes` werden in Wartegrund und
  Protokoll benannt; ohne ausführbares Plugin wird kein CLI-Prozess gestartet.
- **Kein Remote-Schreiben auf Fehlerpfaden:** Fehlerzweige enden in
  `WaitAsync`/`VerarbeitungUngeklärt` mit `SystemMeldung` + Log; es wird
  nirgends ein Fehler-Kommentar am Issue erzeugt.
- **Idempotenz:** Prompt ist deterministisch (`ScmIssueAufbereitungPrompt.Build`
  aus persistierten Daten), `ScmOutgoingActions`-Schlüssel `ai:{actionId}` und
  Request-Vergleich verhindern Doppelausführung bei Reconcile; bestehender
  `DeliveryAttemptId`-Pfad wird ohne neuen Prompt weitergeführt.
- **Kommentar-Schalter hostseitig erzwungen** (Z. 853–866): gelieferte
  Kommentaraktionen werden bei „aus" verworfen und per `WriteResultAsync`
  ehrlich quittiert — die CLI sieht das Ergebnis, das Issue bleibt unverändert.
- **Abschlusskriterium** (Z. 914–936): `complete` + alle deklarierten Aktionen
  `Succeeded` + ≥1 `IssueBodyUpdate`; bei aktivem Kommentar-Schalter zusätzlich
  ≥1 `Comment`. `NoRemoteAction` bleibt dokumentierter Sonderabschluss.
- Konstruktor-Logging (`ILogger<ScmEventOrchestrator>`) ergänzt — Fehler sind
  im App-Log auffindbar; Protokolleinträge sind nach Reason dedupliziert,
  sodass der Poll-Zyklus nicht spammt.

## `ScmIssueAufbereitungPrompt.cs`

- Fester, deutschsprachiger Analyse-Prompt deckt alle geforderten Kriterien ab
  (Verständlichkeit, Sprachqualität, Vollständigkeit, fehlende Angaben, offene
  Fragen, technische/fachliche Risiken, Zielkonflikte, Alternativen,
  Fehlinterpretationen) und definiert den Ausgabevertrag
  (`issueBodyUpdate` = vollständiger Ersatztext, `comment` = Dokumentation).
- Kein Zustand, keine Uhr, keine IDs → bitgleich rekonstruierbar.

## `ScmIssueAufbereitungsService.cs`

- Entparken korrekt minimal: `NextAttemptUtc = long.MaxValue` entfernt,
  Kommentar verweist auf Ausführungszeitpunkt der Schalter/Limits.
- EventKey-Dedup unverändert → keine Doppelaufträge.

## `ScmEventExchangeProtocol.cs`

- Schema-Hinweis für `issueBodyUpdate` ergänzt; keine Protokoll-Break-Changes.

## `ProjektService.cs` / `SettingsViewModel.cs` / `SettingsView.xaml`

- `SetIssueAufbereitungAktivAsync` persistiert das Repository-Flag gezielt.
- ViewModel: vier gebundene Einstellungen, `null`-Prefix = „Globale
  Standard-CLI“, Felder via `IsIssueAufbereitungFelderEnabled` bei deaktivierter
  Funktion gesperrt, Statuszeile im Expander-Header.
- XAML: UIA-Namen sprechend; Repository-Checkboxen binden den Anzeigenamen
  (keine internen IDs als UIA-Name — für E2E und Screenreader gleichermaßen
  sinnvoll).

## Tests

- Integrationstests decken Erfolg, Titel-Unveränderlichkeit, Outbox-Pfad,
  Kommentar an/aus, No-Plugin-/Limit-Warten, Fallback-Kette, Fehler ohne
  Fehlerkommentar, Reconcile und `NoRemoteAction` ab.
- Alter „bleibt geparkt"-Test korrekt zum „sofort fällig"-Test umgebaut.
- FlaUI-E2E im konsolidierten Settings-Lifecycle (kein zusätzlicher App-Start).

## Befunde

- Keine kritischen Befunde. 
- Beobachtung: `DispatchAufbereitungAsync` liest bei deaktiviertem
  Repository-Flag oder Feature den Anbieter nicht — korrekt, vermeidet
  unnötige Remote-Calls.
- `catch`-Filter spiegeln den generischen Pfad (kein automatischer Retry bei
  Start-/Konfigurationsfehlern; `ScmProviderException` mit
  `RetryAt`/`long.MaxValue` je nach `FailureKind`).

## Ergebnis

**Freigegeben.** Konsistent mit den Mustern von #410/#411, keine Duplikate,
keine versteckten Remote-Nebenwirkungen, ehrliche Wartegründe überall.

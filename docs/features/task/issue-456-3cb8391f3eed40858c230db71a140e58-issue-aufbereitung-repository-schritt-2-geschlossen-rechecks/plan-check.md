# Planprüfung – Schritt 2: Re-Checks bei geschlossenem Issue

**Status:** Plan vollständig

- Alle requirement.md-Punkte (finaler Pre-Zustell-Check, Abgleich-Check,
  unlesbar=warten, Status-Semantik, TOCTOU-Doku) sind je einem Planelement
  zugeordnet; alle Akzeptanzszenarien haben eine Testnummer.
- Status-Wahl `Verarbeitet`+Protokoll entspricht der bestehenden Konvention des
  Vor-Dispatch-Checks und der Nicht-Ziel-Vorgabe (kein neuer Status, kein
  `Abgebrochen` für Nicht-Benutzerabbruch); `InBearbeitung` für unlesbar ist
  der ehrliche „später erneut prüfen"-Zustand ohne Terminalverlust.
- Scope eingehalten: Checks nur im Aufbereitungspfad (Dispatch + Reconcile),
  keine Outbox-weite Prüfung, keine CLI-Terminierung.
- Keine offenen Punkte.

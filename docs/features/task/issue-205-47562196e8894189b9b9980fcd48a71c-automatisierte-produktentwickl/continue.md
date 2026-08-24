# Continue: Rückmeldungen zu "Autonome Aufgabe / Projektleiter-Modus"

Dieses Feature (`issue-205-47562196e8894189b9b9980fcd48a71c-automatisierte-produktentwickl`) war
zum Zeitpunkt dieser Notiz bereits über den lifecycle-Abschluss hinaus (Verzeichnis wurde nach
Abschluss des vorherigen Zyklus gelöscht, siehe Commit
`d787c1a fix: Plugin-Auflösung für autonome Aufgaben korrigieren (aufgabenspezifisches statt Default-Plugin)`).
Dieses Verzeichnis wird hier ausschließlich wieder angelegt, um eine neue, davon unabhängige
Anforderung festzuhalten. **Es ist noch keine Analyse oder Umsetzung erfolgt** — nur Erfassung der
Anforderung, wie vom Anwender angefordert.

## Offene Punkte

- [ ] **Eigenes Fenster für die autonome Aufgabe soll in das Hauptfenster integriert werden.**

  **Vom Anwender gewünschtes Verhalten (wörtlich erfasst):** „Die Inhalte des eigenen Fensters für
  die autonome Aufgabe sollen in das Hauptfenster integriert werden. Neben den Bereichen 'Info',
  'CLI', 'PR', 'Todos', etc. soll es dann den weiteren Bereich 'Automatisierung' geben. Darüber
  wird dann der Inhaltsbereich mit den Registerkarten sichtbar. Die Aktionsbuttons 'Start', 'Stop',
  'Resume' sollen in das Ribbon-Menü in der Gruppe 'Autonome Aufgabe' integriert werden."

  **Zusammengefasst:**
  - Der Inhalt des bisher eigenständigen Fensters für autonome Aufgaben (vermutlich
    `AutonomAufgabeDetailDialog`/`AutonomAufgabeDetailView`, siehe
    `src/Softwareschmiede.App/Views/AutonomAufgabeDetailDialog.xaml` bzw. `AutonomAufgabeDetailView.xaml`)
    soll nicht mehr als eigenes Fenster/Dialog angezeigt werden, sondern in das Hauptfenster
    integriert werden.
  - Im Hauptfenster (Registerkarten-Bereich der Aufgaben-Detailansicht, neben den bestehenden
    Bereichen „Info", „CLI", „PR", „Todos" etc.) soll eine neue Registerkarte „Automatisierung"
    entstehen, über die der bisherige Fensterinhalt (mit seinen eigenen Registerkarten) sichtbar wird.
  - Die bisherigen Aktionsbuttons „Start", „Stop", „Resume" (vermutlich aktuell im eigenen Fenster
    oder im Detailpanel, siehe frühere Erwähnung „Der Aufgaben-Detailpanel zeigt jetzt den
    'Start'-Button" in `docs/help/aufgaben/autonome-aufgaben/ablauf-anwender.md`) sollen stattdessen
    in das Ribbon-Menü verschoben werden, dort in einer neuen/bestehenden Gruppe „Autonome Aufgabe".

  **Status:** Nur als Anforderung erfasst. Anwender hat ausdrücklich um Erfassung gebeten und noch
  keine Umsetzung angefordert — **auf weitere Anweisung warten**, bevor Code-Analyse, Planung oder
  Umsetzung begonnen wird.

## Fehlgeschlagene Tests

_(keine — dieser Eintrag betrifft eine neue UI-Integrationsanforderung, keine automatisierte Test-Suite)_

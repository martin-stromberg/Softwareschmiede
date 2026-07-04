# Stabilität & Fehlerbehandlung — Fehlerbehebung

## Wo finde ich die Fehlerprotokolle der Anwendung?

**Symptom:** Die Anwendung verhält sich unerwartet (z. B. eine Aufgabe aktualisiert sich nicht mehr, ein CLI-Prozess reagiert nicht), es ist aber kein Absturz sichtbar.

**Ursache:** Fehler werden seit der Einführung der globalen Exception-Handler und von `SafeFireAndForget` in den meisten Fällen abgefangen und nur protokolliert, statt die Anwendung zu beenden oder eine sichtbare Meldung anzuzeigen.

**Lösung:**
1. Öffne das Verzeichnis `logs` im Installationsverzeichnis der Anwendung (`AppContext.BaseDirectory\logs`).
2. Öffne die aktuelle Datei `softwareschmiede-YYYYMMDD.log` (täglich rollierend, 14 Tage Aufbewahrung).
3. Suche nach `[ERR]`- oder `[WRN]`-Einträgen nahe dem Zeitpunkt des beobachteten Problems.

> **Hinweis:** Da viele Fehler nur geloggt, aber nicht dem Anwender angezeigt werden, ist die Logdatei die primäre Diagnosequelle für „stille" Fehler.

---

## Im Log erscheint „Unbehandelte Exception im UI-Thread."

**Symptom:** Log-Eintrag auf Error-Ebene mit der Meldung „Unbehandelte Exception im UI-Thread." und einem Stacktrace.

**Ursache:** Eine Exception ist im WPF-UI-Thread aufgetreten und wurde von keinem umgebenden try-catch abgefangen. Der globale `DispatcherUnhandledException`-Handler in `App.xaml.cs` hat sie aufgefangen.

**Lösung:**
1. Den mitgeloggten Stacktrace auswerten, um die auslösende Codestelle zu identifizieren.
2. Prüfen, ob die Anwendung nach dem Fehler noch konsistent funktioniert (z. B. betroffene Ansicht neu laden/Aufgabe neu öffnen).
3. Bei wiederholtem Auftreten: gezieltes try-catch an der auslösenden Stelle ergänzen, statt sich auf den globalen Handler zu verlassen.

> **Hinweis:** Der Handler setzt `e.Handled = true` für **jede** Exception, unabhängig vom Typ. Die Anwendung wird also nicht beendet, kann aber nach einem schwerwiegenden Fehler in einem inkonsistenten Zustand weiterlaufen.

---

## Im Log erscheint „Unbehandelte Exception außerhalb des UI-Threads." und die Anwendung wird trotzdem beendet

**Symptom:** Log-Eintrag mit dieser Meldung, direkt gefolgt vom Beenden des Anwendungsprozesses.

**Ursache:** Eine Exception ist auf einem Hintergrund-Thread (z. B. ThreadPool-Thread) aufgetreten. `AppDomain.CurrentDomain.UnhandledException` kann geloggt werden, aber den Prozessabbruch in der Regel **nicht** verhindern (`IsTerminating` ist bei .NET meist `true`).

**Lösung:**
1. Den Stacktrace im letzten Log-Eintrag vor dem Beenden auswerten.
2. Prüfen, ob der auslösende Code über einen Fire-and-Forget-Aufruf ohne `SafeFireAndForget` gestartet wurde — falls ja, dort `SafeFireAndForget` ergänzen, damit künftige Fehler an dieser Stelle nur geloggt statt den Prozess beendet werden.

---

## Im Log erscheint „Unbeobachtete Task-Exception."

**Symptom:** Log-Eintrag auf Error-Ebene mit dieser Meldung, ohne dass die Anwendung sichtbar reagiert.

**Ursache:** Ein Task ist fehlgeschlagen, dessen Exception nie abgefragt wurde (kein `await`, kein `SafeFireAndForget`). Der `TaskScheduler.UnobservedTaskException`-Handler fängt dies typischerweise beim nächsten Garbage-Collector-Lauf ab.

**Lösung:**
1. Den Stacktrace auswerten und die Codestelle identifizieren, die den Task ohne Beobachtung gestartet hat.
2. Den Aufruf auf `SafeFireAndForget(logger, "Bezeichnung")` umstellen, damit der Fehler sofort statt erst beim GC-Lauf geloggt wird.

---

## „CliProcessManager konnte nicht initialisiert werden. Die Anwendung läuft ohne CLI-Funktionalität weiter."

**Symptom:** Diese Meldung erscheint beim Anwendungsstart im Log; CLI-Funktionen (Aufgaben starten, Terminal) funktionieren nicht.

**Ursache:** `GetRequiredService<CliProcessManager>()` ist beim Start fehlgeschlagen, üblicherweise wegen eines Fehlers in der DI-Konfiguration einer seiner Abhängigkeiten.

**Lösung:**
1. Den Stacktrace direkt unterhalb dieser Meldung im Log auswerten — er zeigt die tatsächliche Ursache (z. B. fehlende Konfiguration eines Plugins).
2. Anwendung neu starten, nachdem die Ursache behoben wurde.

> **Hinweis:** Die Anwendung startet in diesem Fall trotzdem — alle nicht-CLI-bezogenen Funktionen (Projekte, Einstellungen) bleiben nutzbar.

---

## `ObjectDisposedException` beim Beenden eines ConPTY-Prozesses im Log

**Symptom:** Log-Eintrag „Fehler im Exited-Handler (ConPTY) für Aufgabe {AufgabeId}." mit einer `ObjectDisposedException`.

**Ursache:** `PseudoConsoleSession.Dispose()` wurde für dieselbe Session mehrfach aufgerufen (z. B. durch gleichzeitiges manuelles Stoppen und automatisches Prozessende). Dies wird von `HandleProcessExited` abgefangen und geloggt, statt die Anwendung zu beeinträchtigen.

**Lösung:**
1. In der Regel ist keine Aktion notwendig — der Prozess wurde korrekt als beendet erkannt und alle Handles wurden freigegeben.
2. Tritt dieser Fehler sehr häufig auf, kann dies auf ein Timing-Problem beim gleichzeitigen Stoppen/Beenden hindeuten und sollte mit dem Entwicklungsteam abgeklärt werden.

---

## Terminal zeigt keine weitere Ausgabe mehr, obwohl der CLI-Prozess noch läuft

**Symptom:** Das Terminal-Fenster friert ein, aktualisiert sich aber nicht mehr; der zugehörige Prozess läuft laut Task-Manager weiter.

**Ursache:** `TerminalControl.ReadLoopAsync` hat eine unerwartete Exception geworfen (z. B. beim Parsen einer ANSI-Sequenz) und die Leseschleife wurde beendet. Der Fehler wird als „Unerwarteter Fehler in Terminal-Lesevorgang" geloggt.

**Lösung:**
1. Im Log nach „Unerwarteter Fehler in Terminal-Lesevorgang" bzw. „Fehler beim Lesen aus dem Terminal-Output-Stream" suchen.
2. Aufgabendetailansicht schließen und erneut öffnen — dies startet einen neuen `ReadLoopAsync`-Task für die bestehende Session.
3. Bei wiederholtem Auftreten den Stacktrace an das Entwicklungsteam melden.

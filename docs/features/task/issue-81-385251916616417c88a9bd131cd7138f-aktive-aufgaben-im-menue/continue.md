# Offene Aufgaben

Erstellt am: 2026-07-02
Abbruchgrund: Kein Fortschritt zwischen den letzten zwei Iterationen (Iteration 1: 3 offene Punkte, Iteration 2: 3 offene Punkte)

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine — der Plan gilt laut `review.md` als vollständig umgesetzt.

## Rückmeldung vom Kunden

- [x] Die Aufgabenliste im Menü ist falsch platziert. Sie gehört unterhalb aller Menüpunkte.
- [x] Auf dem Dashboard üssen sich die Kacheln der Aufgaben verhalen wie auch alle anderen Kacheln, die im Inhaltsbereich angezeigt werden. Der Buttonmit dem Pfeil muss dort weg. und ein Klick auf eine Kachel (egel wo) öffnet die Aufgabe.
- [x] Die Aufgabenkachel muss den Projektnamen enthalten.
- [ ] Ist eine Aufgabe geöffnet und wählt der Anwender eine andere Aufgabe aus der Aufgabenliste im Menü aus, so bleibt die geöffnete Aufgabe geöffnet und die neue Aufgabe wird nicht angezeigt. Lediglich in der Fußzeile ändert die Ansicht auf den Namen der zweiten Aufgabe. Die geöffnete CLI ist aber weiterhin diejenige, der zuvor geöffneten Aufgabe. Das ist falsch. Es muss die neue Aufgabe angezeigt werden. Stelle mit einem E2E-test sicher, dass die geöffnete Aufgabe korrekt gewechselt wird, wenn der Anwender eine andere Aufgabe aus der Aufgabenliste im Menü auswählt. Erstelle den Test zuerst, bevor du die Implementierung vornimmst. Der Test muss fehlschlagen, bevor du die Implementierung vornimmst. Danach implementierst du die Funktionalität und führst den Test erneut aus. Der Test muss nun erfolgreich sein.

## Code-Review-Befunde

- [ ] AufgabeService.cs / AufgabeRecoveryService.cs: Die fachliche Regel „Aufgabe ist aktiv oder wartend" (Status `Gestartet` oder `Wartend`) ist an drei Stellen unabhängig voneinander implementiert (`AufgabeService.IstAktivOderWartendPredicate`, Inline-Check in `AufgabeService.DeleteAsync`, `AufgabeRecoveryService.IstRecoveryStatus`). Eine einzige Quelle der Wahrheit einführen (z. B. `AufgabeStatus.IstAktivOderWartend(...)`) und an allen drei Stellen referenzieren.
- [ ] MainWindowViewModel.cs: `AktiveAufgabenAktualisierenAsync` fängt `catch (Exception ex)` pauschal ab, ohne `OperationCanceledException` vorher auszunehmen — inkonsistent zu `DashboardViewModel.LadenAsync`, das Abbrüche korrekt weiterwirft. Analogen `catch (OperationCanceledException) { throw; }`-Block ergänzen.
- [ ] MainWindowViewModel.cs (`NavigateToDashboard`): Verdrahtung von `DashboardViewModel` über direktes Setzen der öffentlichen, extern beschreibbaren Properties `AktiveAufgabenListe` und `NavigateZuAufgabeAction` nach der Konstruktion — order-abhängig und bricht Kapselung. Über eine dedizierte Initialisierungsmethode (z. B. `Initialize(...)`) oder Konstruktor-Injektion kapseln.

## Fehlgeschlagene Tests

Keine — 708/708 Unit- und Integrationstests bestanden (siehe `test-results.md`).

- [x] E2E Tests funktionieren, aber merke dir: Bei E2E-tests für WPF dürfen während der Testausführugn keine Änderungen am Code durchgeführt werden und vor dem Ausführen der Tests muss die Anwendung komplett kompiliert sein. Ansonsten kann es vorkommen, dass der Test fehlschlägt, weil angeblich die .NET Desktop Runtime fehlt. — Behoben an der Quelle: `/run-tests`-Kommandodefinition (`~/.claude/commands/run-tests.md` und `.devin/skills/lifecycle/run-tests.md`) forderte bislang `dotnet test --no-build` ohne vorausgehenden Build; jetzt auf `dotnet build && dotnet test --no-build` inkl. Warnhinweis korrigiert.

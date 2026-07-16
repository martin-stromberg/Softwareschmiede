# Folgeaufgaben

Diese Anforderung wurde vollständig umgesetzt (siehe `review.md`, `review-code.md`, `test-results.md`). Die folgende Ergänzung wurde vom Anwender nachträglich gewünscht und ist als redaktioneller Folgeschritt an `SECURITY.md` festzuhalten:

## SECURITY.md – Hinweis auf eingebettete Dritt-CLIs ergänzen

**Kontext:** Softwareschmiede ist ein Verwaltungsprogramm, das CLI-Tools anderer Anbieter (z. B. KI-Agenten-CLIs) eingebettet ausführt. Eine gemeldete Sicherheitslücke kann sich daher auf das Verhalten einer eingebetteten Dritt-CLI beziehen statt auf die Softwareschmiede-Anwendung selbst.

**Zu ergänzen:** In `SECURITY.md` soll ein Hinweis aufgenommen werden, dass eine Rückmeldung auf eine gemeldete Sicherheitslücke ggf. auf den jeweiligen Anbieter der eingebetteten CLI verweist, sofern sich herausstellt, dass das Problem nicht in der Softwareschmiede-Anwendung selbst liegt, sondern in der Ausführung/dem Verhalten der eingebetteten Dritt-CLI. Betroffener Meldeweg bleibt unverändert (GitHub Private Security Advisories); lediglich die mögliche Art der Rückmeldung wird transparent gemacht.

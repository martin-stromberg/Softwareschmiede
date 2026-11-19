# GitHub-Ereignisüberwachung

Die Softwareschmiede beobachtet Texte in lokal verknüpften GitHub-Issues und Pull Requests — wahlweise repository-weit unter dem Benutzerkonto (Trigger-Keyword) oder über die GitHub-Benachrichtigungen eines separaten Kontos (`@`-Erwähnung) — und kann passende Anfragen an eine Aufgabe und deren CLI weiterleiten. Die Verarbeitung und ihr aktueller Stand sind auf der Seite **SCM-Ereignisse** nachvollziehbar. Ergänzend können eigene, unabhängige Prüfzyklen Backmerge-Pull-Requests automatisch mergen und neu angelegte Issues automatisch aufbereiten — beide immer unter dem Benutzerkonto und daher auch im Modus „Separates Konto" wirksam — und eine Abschlussüberwachung Aufgaben mit geschlossener oder gemergter SCM-Referenz automatisch beenden — gesteuert wird das alles unter **Einstellungen → Automatisierung**. Als Ereignisquelle kann außerdem eine im Repository hinterlegte Zeitsteuerung dienen: Geplante Review-Workflows legen regelmäßig Issues mit `@`-Erwähnung des separaten Kontos an und erzeugen so wiederkehrende Prüfaufträge.

## Inhalt

- [Beschreibung](beschreibung.md)
- [Ablauf für Anwender](ablauf-anwender.md)
- [Einrichtung](installation.md)
- [Technischer Ablauf](ablauf-technisch.md)
- [Architektur](architektur.md)
- [Datenmodell](datenmodell.md)
- [Business Rules](business-rules.md)
- [Fehlerbehebung](troubleshooting.md)

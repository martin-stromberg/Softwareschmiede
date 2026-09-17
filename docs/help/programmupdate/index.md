# Programmupdate

Das Programmupdate-Feature hält die Softwareschmiede-Anwendung aktuell. In den Einstellungen (Registerkarte **Allgemein**, Abschnitt **Updates**) wählt der Anwender einen von drei Modi — **Aus**, **Nur Pruefen** oder **Bei Programmstart pruefen und ausfuehren** — und kann optional auch Prerelease-Versionen berücksichtigen lassen. Über die Schaltfläche „⟳ Prüfen" am Fuß der Seitenleiste lässt sich jederzeit manuell prüfen; ein gefundenes Update wird über „⇧ Update" angeboten und installiert. Vor dem Update wird überprüft, ob laufende CLI-Aufgaben das Update blockieren würden; der Anwender wird ggf. gewarnt und kann dann entscheiden, ob er fortfahren möchte. Bei Bestätigung zeigt ein Fortschrittsdialog den Status der Update-Vorbereitung (Download, Entpacken, Update-Vorbereitung) an; die eigentliche Installation führt ein externes Skript nach dem Beenden der Anwendung aus.

## Inhalt

- [Beschreibung](beschreibung.md)
- [Ablauf für Anwender](ablauf-anwender.md)
- [Technischer Ablauf](ablauf-technisch.md)
- [Architektur](architektur.md)
- [Business Rules](business-rules.md)
- [Fehlerbehebung für Anwender](fehlerbehebung-anwender.md)

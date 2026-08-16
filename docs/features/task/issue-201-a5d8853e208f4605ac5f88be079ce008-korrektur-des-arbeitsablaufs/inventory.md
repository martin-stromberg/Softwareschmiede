# Inventar: Korrektur des Arbeitsablaufs

## Umfang

Untersucht wurden die Aufgaben-Entity und Statusmodelle, der Aufgaben- und KI-Ausfuehrungslebenszyklus, die Aufgabendetail- und Projektansicht, Navigation und Persistenz sowie vorhandene Unit-, Integrations- und E2E-Tests. Die Anforderung steht in [requirement.md](requirement.md).

## Kernergebnis

- Der Gesamtstatus der Aufgabe ist in `Aufgabe.Status` als `AufgabeStatus` modelliert.
- Es gibt bereits `AktiveRunId`, Heartbeat-Felder und `LaufStatus`; `LaufStatus` ist jedoch nur ein Laufzeit-Substatus (`Laeuft`/`WartetAufEingabe`) und kein persistenter Ausfuehrungsstatus mit den benoetigten Zustaenden `nicht gestartet`, `aktiv` und `beendet`.
- Beim Laden einer Aufgabe mit Gesamtstatus `Gestartet` wird die CLI automatisch neu gestartet, wenn aktuell kein Prozess laeuft. Das widerspricht der geforderten Behandlung einer beendeten KI-Ausfuehrung.
- Stoppen entfernt den laufenden Prozess, setzt aber weder einen separaten Ausfuehrungsstatus noch den Gesamtstatus auf `Beendet`. Der Gesamtstatus bleibt `Gestartet`.
- Das endgueltige Beenden ist bereits getrennt: `EntwicklungsprozessService.AbschliessenAsync` loescht den lokalen Klon und delegiert die Statussetzung auf `AufgabeStatus.Beendet`.
- Beim Speichern einer neuen Aufgabe ruft `TaskDetailViewModel.SpeichernAsync` derzeit `ZurueckAction` auf. Dadurch wird nach dem Speichern zur Projektansicht navigiert. Die Erstellung selbst oeffnet die Detailansicht bereits direkt.

## Relevante Detailanalysen

1. [Domain-Entity und Statusmodell](inventory/domain-status.md)
2. [Services fuer Starten, Stoppen und Beenden](inventory/services-lifecycle.md)
3. [ViewModels, CLI-Anzeige und Navigation](inventory/viewmodels-navigation.md)
4. [Persistenz und Migrationen](inventory/persistence.md)
5. [Tests und E2E-Abdeckung](inventory/tests.md)

## Betroffene Bereiche fuer die Planung

1. Einen eigenstaendigen persistenten KI-Ausfuehrungsstatus in `Aufgabe` und EF-Modell einfuehren; `LaufStatus` als kurzfristigen Terminal-Substatus getrennt lassen oder eindeutig abgrenzen.
2. Start, manueller Neustart, Stoppen, Prozessende und Recovery auf den neuen Ausfuehrungsstatus ausrichten.
3. `LadenAsync` darf nur bei aktivem Ausfuehrungsstatus eine bestehende CLI-Session wieder anbinden bzw. wiederherstellen; bei beendetem Status darf kein Autostart erfolgen.
4. `StartenCommand` und die Anzeigezustandslogik muessen neue sowie beendete Ausfuehrungen erlauben, aber Gesamtstatus `Beendet` sperren.
5. Nach erfolgreichem Speichern einer neu angelegten Aufgabe muss die bestehende TaskDetailView offen bleiben; die Projektliste soll weiterhin aktualisiert werden, ohne als Ruecknavigation zu dienen.
6. Tests fuer Persistenz, Statusuebergaenge, Wiederaufruf, Neustartverhalten, Stoppen und Navigation ergaenzen bzw. bestehende E2E-Erwartungen anpassen.

## Nicht ausgefuehrt

Gemaess Vorgabe wurden fuer diesen Inventar-Schritt keine Tests ausgefuehrt.

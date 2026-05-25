# Architektur-Review – KI-Protokoll Auto-Scroll

**Feature-Slug:** `ki-protokoll-auto-scroll`

## Zusammenfassung
Die Architektur ist tragfähig und deckt die Kernanforderungen ab. Für eine robuste Umsetzung sind vor allem konsistente Modellabbildung, konservativer Fehlerpfad und belastbare Tests entscheidend.

## Findings

### Blocker
1. **Traceability-Lücke zwischen Anforderungen, Blueprint und ERM**
   - Risiko: Uneinheitliche Umsetzung einzelner Regeln.
   - Maßnahme: Explizite Zuordnung FR -> Architekturkomponente -> ERM-Entität dokumentieren.

### Major
1. **Fehlerstrategie bei Metrik-Lesefehlern muss positionsschonend sein**
   - Risiko: Unerwartetes Springen an das Ende.
   - Maßnahme: Bei `getMetrics`-Fehler standardmäßig kein erzwungener Follow-Scroll.
2. **Unzureichende Absicherung bei Burst-Updates**
   - Risiko: Veraltete Scrollentscheidungen.
   - Maßnahme: Tests für schnelle Folgeupdates und Versionsschutz ergänzen.
3. **Observability unzureichend**
   - Risiko: Fehler schwer analysierbar.
   - Maßnahme: Strukturierte Logs für Entscheidungsgrund und ausgeführte Scrollaktion.

### Minor
1. **Starrer Schwellwert**
   - Risiko: Grenzfälle je Browser/Zoom.
   - Maßnahme: zentraler Default, optional konfigurierbar.

## Empfohlene Reihenfolge
1. Traceability-Matrix ergänzen.
2. Konservativen Fehlerpfad fixieren.
3. Burst-/Container-Tests erweitern.
4. Telemetrie ergänzen.
5. Schwellwert konfigurierbar machen.

## Abnahme-Checkliste
- [ ] Initiales Scrollen funktioniert beim Einblenden beider Container.
- [ ] Follow-Scroll erfolgt nur bei vorheriger Endposition.
- [ ] Leserposition bleibt erhalten, wenn Nutzer hochgescrollt hat.
- [ ] Versionsschutz verhindert veraltete Scrollaktionen.
- [ ] Fehlerfälle führen nicht zu UI-Abbrüchen.
- [ ] Logs enthalten Entscheidungsgrund und Aktion.

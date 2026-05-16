# Architecture-Review – `start.ps1` (parameterlos, autonome Webprojekt-Erkennung, Mehrprojekt-Verarbeitung)

> **Dokument-Typ:** Architecture Review  
> **Status:** Freigabe mit Auflagen  
> **Version:** 1.1.0  
> **Datum:** 2026-05-14

---

## 1. Referenzen

- Anforderungen (v1.1.0): [../requirements/start-ps1-visual-studio-freier-http-port-requirements-analysis.md](../requirements/start-ps1-visual-studio-freier-http-port-requirements-analysis.md)
- Architektur (v1.1.0): [../architecture/start-ps1-visual-studio-freier-http-port-architecture-blueprint.md](../architecture/start-ps1-visual-studio-freier-http-port-architecture-blueprint.md)
- ERM (v1.1.0): [../architecture/start-ps1-visual-studio-freier-http-port-entity-relationship-model.md](../architecture/start-ps1-visual-studio-freier-http-port-entity-relationship-model.md)

---

## 2. Fazit

Der Lösungsansatz ist fachlich korrekt und umsetzbar.  
Der Wechsel zu einem parameterlosen Skriptvertrag reduziert Kopplung und verbessert den lokalen Entwicklerfluss.

**Freigabeentscheidung:** ✅ **Go mit Auflagen**

---

## 3. Priorisierte Findings

| ID | Priorität | Finding | Empfehlung |
|---|---|---|---|
| AR-01 | MAJOR | Discovery-Regeln sind definiert, müssen aber in der Implementierung strikt deterministisch umgesetzt werden (Filter, Reihenfolge, Ausschlüsse). | Discovery-Regeln als testbare Spezifikation übernehmen; Negativfälle für ausgeschlossene Verzeichnisse ergänzen. |
| AR-02 | MAJOR | Mehrprojekt-Teilfehler können zu gemischten Ergebnissen führen. | Aggregationsregel (`13 > 12 > 11 > 10 > 0`) verbindlich implementieren und testen. |
| AR-03 | MAJOR | Atomisches Schreiben ist kritisch bei parallelen Zugriffen auf `launchSettings.json`. | Temp-Write + Move + Cleanup + Retry-Policy technisch fixieren; Exit `13` reproduzierbar nachweisen. |
| AR-04 | MINOR | TOCTOU bleibt auch nach erfolgreicher Portwahl bestehen. | Klaren Retry-Hinweis standardisieren und in Diagnostik aufnehmen. |
| AR-05 | MINOR | Laufzeitdiagnostik muss bei Mehrprojektläufen konsistent korrelierbar sein. | Pflichtfelder pro Projekt (`runId`, `projectPath`, `port`, `resultCode`) in Logs und Tests festschreiben. |

---

## 4. Risiken und Trade-offs

### Risiken
1. Port zwischen Konfiguration und Start erneut belegt (TOCTOU).
2. Teilfehler in Mehrprojektlauf (einige Projekte erfolgreich, andere fehlgeschlagen).
3. Lokale Merge-/Dateikonflikte durch Laufzeitänderungen in `launchSettings.json`.
4. Browser-Debugging kann bei nicht kompatiblen Hostnamen scheitern; `localhost`-Fallback muss im Zweifel Vorrang haben.

### Trade-offs
| Entscheidung | Vorteil | Nachteil |
|---|---|---|
| Parameterloser Standardaufruf | Einfachere Bedienung, weniger App-Kopplung | Weniger manuelle Steuerbarkeit pro Einzelstart |
| Autonome Discovery | Skalierbar auf mehrere Web-Projekte | Höhere Anforderungen an deterministische Regeln |
| Keine Persistenz | Kein DB-/Migrationsaufwand | Keine Historie der Portvergaben |

---

## 5. Auflagen vor Implementierungsabschluss

1. Discovery-Regeln und Ausschlüsse testseitig vollständig abdecken.
2. Exit-Code-Aggregation für Mehrprojektläufe automatisiert verifizieren.
3. Atomische Schreibstrategie inkl. Cleanup für Fehlerfall absichern.
4. Diagnostikformat und Pflichtfelder projektübergreifend standardisieren.

---

## 6. Versionierung

| Version | Datum | Autor | Änderung |
|---|---|---|---|
| 1.1.0 | 2026-05-14 | review-architecture | Review auf parameterlosen Mehrprojekt-Ansatz aktualisiert; offene Risiken/Auflagen präzisiert |
| 1.0.0 | 2026-05-14 | review-architecture (orchestriert) | Initiales Review der Erstfassung |

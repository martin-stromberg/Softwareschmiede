# Architektur-Review – Repository-Startskript mit freier Portzuweisung

> **Dokument-Typ:** Architecture Review  
> **Status:** Freigabe mit Auflagen  
> **Datum:** 2026-05-14

---

## 1. Executive Summary

Der Ansatz ist fachlich sinnvoll: Das Startskript wird repositorybezogen gespeichert und ein freier Port wird vor dem Start ermittelt. Damit lassen sich branchspezifische lokale Runs sauber trennen.

**Gesamtbewertung:** ⚠️ Freigabe mit Auflagen

---

## 2. Bewertungsmatrix

| Bereich | Bewertung | Kurzbegründung |
|---|---|---|
| Systemarchitektur | Gut | Klare Trennung zwischen UI, Portlogik und Skriptausführung. |
| Technologieentscheidungen | Mit Risiko | PowerShell ist passend, die Portübergabe muss aber sicher definiert werden. |
| UI/UX | Solide | Skriptauswahl ist verständlich, Fehlermeldungen müssen noch präziser werden. |
| Qualitätsziele | Teilweise belastbar | Determinismus und Isolation sind benannt, aber nicht ausreichend operationalisiert. |

---

## 3. Strukturierte Bewertung

### 3.1 Systemarchitektur

**Stärken**
- Repositorybezogene Konfiguration ist fachlich passend.
- Laufzeitport und persistente Konfiguration sind getrennt.

**Schwachstellen**
- Die Portreservierung ist als technische Maßnahme beschrieben, aber noch nicht als atomarer Ablauf abgesichert.
- Der Trust-Boundary für Skripte ist noch zu breit.

### 3.2 Technologieentscheidungen

**Positiv**
- `ProcessStartInfo.ArgumentList` ist die richtige Basis.
- Ein relateriver Skriptpfad vermeidet harte Maschinenpfade.

**Risiken**
- `launchSettings.json` kann zwischen Check und Start verändert werden.
- Ein freier Port kann nach der Prüfung bereits belegt sein.

### 3.3 UI/UX-Review

**Verbesserungsbedarf**
- Fehlermeldungen sollten unterscheiden zwischen:
  - Skript fehlt
  - Skript unzulässig
  - Port nicht reservierbar
  - Skriptlauf fehlgeschlagen

### 3.4 Qualitätsziele

**Lücken**
- Kein messbarer Grenzwert für die Portprüfung.
- Keine definierte Wiederholstrategie bei Portkollisionen.

---

## 4. Priorisierte Findings

| ID | Priorität | Finding | Risiko |
|---|---|---|---|
| F-01 | Major | Portprüfung und Portnutzung sind nicht atomar abgesichert. | Race-Condition bei parallelen Starts. |
| F-02 | Major | Skriptpfad-Validierung ist noch nicht als Pflichtgrenze definiert. | Ausführung unzulässiger Dateien. |
| F-03 | Major | Repository-Startkonfiguration und UI-Zugriff sind noch nicht vollständig abgegrenzt. | Inkonsistente Speicherung oder Anzeige. |
| F-04 | Medium | Änderung von `launchSettings.json` ist nur als Ziel beschrieben, nicht als robustes Update-Verfahren. | Beschädigte Projektkonfiguration. |
| F-05 | Medium | Fehlermeldungen sind noch zu generisch. | Erhöhter Supportaufwand. |

---

## 5. Verbesserungsmaßnahmen

### M-01 – Portreservierung schärfen
1. Port erst reservieren, dann an das Skript übergeben.
2. Vor dem eigentlichen Start eine zweite Kurzprüfung durchführen.
3. Bei Konflikt einen neuen Port ziehen.

### M-02 – Skriptgrenze festziehen
1. Nur relative Pfade unterhalb des Repositorys erlauben.
2. Standardmäßig nur `.ps1` zulassen.
3. Fehlende oder verschobene Skripte als harte Fehler behandeln.

### M-03 – Konfigurations-Update absichern
1. Konfigurationsänderungen nur im Branch-Klon durchführen.
2. Vor der Änderung Backup/Restore-Strategie vorsehen.
3. Änderungen atomar schreiben.

### M-04 – Fehlertexte operationalisieren
1. Ursachebezogene Meldungen einführen.
2. Retry nur bei temporären Portproblemen.
3. Nutzerhinweis zur Repository-Konfiguration ergänzen.

---

## 6. Freigabeempfehlung

Umsetzung starten nach Schließen von **M-01 bis M-03**.  
**M-04** sollte spätestens vor Release folgen.

---

## 7. Verlinkung

- Anforderungen: [../requirements/repository-startskript-freier-port-requirements-analysis.md](../requirements/repository-startskript-freier-port-requirements-analysis.md)
- Architektur-Blueprint: [../architecture/repository-startskript-freier-port-architecture-blueprint.md](../architecture/repository-startskript-freier-port-architecture-blueprint.md)
- ERM: [../architecture/repository-startskript-freier-port-entity-relationship-model.md](../architecture/repository-startskript-freier-port-entity-relationship-model.md)


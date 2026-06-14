# Fehlerkorrektur mit Regressionstest

Dieses Kommando gilt für die Implementierung von Fehlerkorrekturen. Für jeden behobenen Fehler muss ein Regressionstest geschrieben werden, der den Fehler reproduziert und nach dem Fix bestätigt, dass er behoben ist.

**Gilt für:** Korrekturen auf Basis von `continue.md`, `review.md` oder `review-code.md`.

---

## Ablauf für jeden Fehler

Arbeite jeden zu behebenden Fehler einzeln ab. Pro Fehler:

### 1. Fehlerursache verstehen

Lies die betroffenen Dateien vollständig. Identifiziere:
- Wo genau liegt der Fehler (Methode, Klasse, Zeile)?
- Was ist die Ursache (nicht das Symptom)?
- In welchem Szenario tritt er auf?

### 2. Reproduzierenden Test schreiben

Schreibe einen Test, der den Fehler direkt reproduziert:

- Testname nach dem Muster: `MethodName_Szenario_FehlerverhaltenDasNichtSeinSollte`
  - Beispiel: `Calculate_WithNullInput_ThrowsArgumentNullException`
- Der Test muss auf das **fachliche Fehlverhalten** prüfen, nicht auf Implementierungsdetails
- Führe den Test aus — er muss **fehlschlagen**, bevor der Fix angewendet wird

Falls der Test bereits besteht (grün ist), ohne dass der Fix implementiert wurde: Das bedeutet, der Test deckt den Fehler nicht korrekt ab. Überarbeite den Test, bis er rot ist.

### 3. Fix implementieren

Implementiere den Fix entsprechend dem Befund / dem offenen Punkt aus `continue.md`.

Halte dich dabei an die Vorgaben aus `plan.md` und der Bestandsaufnahme:
- Keine zusätzlichen Änderungen außerhalb des Fehlerbereichs
- Keine Refactorings „nebenbei"

### 4. Test verifizieren

Führe den in Schritt 2 geschriebenen Test erneut aus. Er muss jetzt **bestehen**.

Falls der Test weiterhin fehlschlägt: Überarbeite den Fix. Fahre nicht fort, bis der Test grün ist.

### 5. Gesamte Testsuite ausführen

Führe alle Tests im Projekt aus:

```
dotnet test
```

Stelle sicher, dass kein zuvor bestandener Test durch den Fix gebrochen wurde. Falls doch: Untersuche die Regression und behebe sie, bevor du mit dem nächsten Fehler fortfährst.

---

## Hinweise

- Ein Fehler ohne Regressionstest gilt als nicht abgeschlossen.
- Schreibe den Test in die Testklasse, die der korrigierten Klasse entspricht. Falls keine existiert, lege eine an.
- Nutze dieselben Test-Konventionen wie im restlichen Projekt (xUnit, Arrange-Act-Assert, FluentAssertions, Moq).
- Falls der Fehler nicht reproduzierbar ist (kein Test lässt sich rot machen), halte das als Kommentar im Testcode fest und fahre mit dem Fix fort.

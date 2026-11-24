# Bestandsaufnahme: Stati

## `ScmIncomingEventStatus` (statische Konstantenklasse)
Datei: `src/Softwareschmiede/Domain/Entities/ScmIncomingEventStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Aufgenommen` | Neu registriert, noch nicht zugestellt (Default). |
| `Verzögert` | Zustellung ruht persistiert bis `NextAttemptUtc` (Verzögerungsdirektive). |
| `Wartend` | Wartet auf Bedingung/Korrektur; `Reason` erklärt. Mit `NextAttemptUtc = long.MaxValue` = manueller Neuversand nötig (bleibt laut Doku bewusst blockierend für Abschluss/Serialisierung). |
| `ZustellungVorbereitet` | Versuchskanal angelegt, Prompt noch nicht geschrieben (In-Flight, `DeliveryAttemptId` gesetzt). |
| `Zugestellt` | Prompt in die CLI geschrieben; Annahme steht aus (In-Flight). |
| `ZustellungUngewiss` | Schreibversuch begonnen, Erfolg unbestätigt; terminal, nur manuell klärbar; in `NonBlocking`. |
| `InBearbeitung` | Korrelierte Annahme eingetroffen; Abschluss steht aus (In-Flight). |
| `VerarbeitungUngeklärt` | Terminaler Diagnose-Status ohne automatischen Neuversand; Klärung nur manuell; in `NonBlocking`. |
| `Verarbeitet` | Final: Annahme + bestätigter Abschluss; in `NonBlocking`. |
| `Abgebrochen` | Final: vom Anwender abgebrochen; in `NonBlocking`. |

**Mengen:** `NonBlocking = [Verarbeitet, Abgebrochen, VerarbeitungUngeklärt, ZustellungUngewiss]` — freigebend für Abschlussprüfung und Vorgänger-Serialisierung; jeder andere (auch unbekannte) Wert blockiert dort konservativ. Die Platzlogik nutzt diese Menge bisher **nicht** — sie wertet lediglich `!= "Verarbeitet" && != "Abgebrochen"` aus.

**Dispatch-Auswahl (`DispatchDueEventsAsync`):** `Status != Abgebrochen && ((Status != Verarbeitet && (NextAttemptUtc == null || <= now)) || offene Outbox-Aktion)` — `VerarbeitungUngeklärt`/`ZustellungUngewiss`/`Wartend` ohne `NextAttemptUtc` werden bei jedem Zyklus erneut ausgewählt.

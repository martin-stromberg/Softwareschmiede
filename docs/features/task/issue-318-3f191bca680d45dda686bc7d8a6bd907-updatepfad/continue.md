# Offene Aufgaben

Erstellt am: 2026-11-03
Abbruchgrund: Kein Fortschritt zwischen den letzten zwei Iterationen

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine — Plan-Review meldet `Vollständig umgesetzt`.

## Code-Review-Befunde

- [ ] `E2E_UpdateFixture.cs:89` — Veralteter Kommentar „RC-Auswahl bei aktivierten Prereleases" (Rest der Iteration-1-Behebung) auf „Update-Pfad"-Terminologie umstellen
- [ ] `UpdateModels.cs:58` — `UpdateSettings.Kanal` ist toter Code (nirgends aufgerufen); entfernen oder eine sinnvolle Nutzung schaffen (z. B. `UpdateCheckOptions`-Erzeugung zentral über `UpdateSettings`)
- [ ] `AppEinstellungService.cs:431` — `SetUpdateSettingsAsync` persistiert `settings.UpdatePfad.Trim()` statt der kanonischen `UpdatePfadKanalResolver.NormalisierePfad`-Form; Schreib-Lese-Roundtrip bei tolerierten Eingaben vereinheitlichen
- [ ] `UpdatePfadKanalResolver.cs:17,64` vs. `DatenbankPfadResolver.cs:16,75` — „-rc"-Klassifikationsregel (Konstante + `Contains(OrdinalIgnoreCase)`) in zwei Klassen dupliziert; `DatenbankPfadResolver` soll `ErmittleKanalFuerTagName` wiederverwenden

## Fehlgeschlagene Tests

- [ ] `Softwareschmiede.Tests.App.ViewModels.KonsolenTestViewModelTests.Wiedergabe_PausierenUndFortsetzen` — `WarteBisAsync`-Timeout nach 15 s unter Volllast; isolierter Wiederholungslauf bestanden (282 ms) → Flaky-Timing, kein reproduzierbarer Fehler, bei erneutem Lauf verifizieren

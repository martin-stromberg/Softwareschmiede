# Offene Aufgaben

Erstellt am: 2026-08-19
Abbruchgrund: Maximale Iterationsanzahl erreicht (3 Iterationen der Implementierungs-/Review-Schleife, Fortsetzungszyklus)

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine. `review.md` trägt weiterhin den Status „Vollständig umgesetzt" (aus der ursprünglichen Iteration 1, seither nicht erneut geprüft, da die Planänderungen in diesem Fortsetzungszyklus — Auflösung des `KannIdeAuswaehlen`-Widerspruchs, `plan.md`-Update — keine Abweichung vom aktualisierten Plan darstellen).

## Code-Review-Befunde

- [ ] **Geringfügig — `TaskDetailViewModelTests_IdeAuswahl.cs`: Doppelter Mock-Aufbau.** `WaehleEntryPointAsync_UsesDisplayNameInDialog` (Zeile ~242–252) und `KannIdeAuswaehlen_WhenOpenEntryPointFailsWithMultipleEntryPoints_BleibtTrue` (Zeile ~287–297) bauen jeweils denselben `Mock<IIdePlugin>` mit acht identischen Setup-Zeilen auf (`PluginName`, `PluginPrefix`, `PluginType`, `GetSettingGroups()`, `CheckCompatibilityAsync(...)`, `FindEntryPointsAsync(...)`) und unterscheiden sich nur im letzten Setup für `OpenEntryPointAsync` (einmal `Returns(Task.CompletedTask)`, einmal `ThrowsAsync(...)`). Empfehlung: Private Hilfsmethode `CreateTestIdePluginMock(IReadOnlyList<IdeEntryPoint> entryPoints)` ergänzen (in der Testklasse oder in `TaskDetailViewModelTestsBase`, falls künftig weitere Tests denselben Aufbau brauchen), die den gemeinsamen Teil kapselt; das `OpenEntryPointAsync`-Setup bleibt Sache des jeweiligen Testfalls.

## Fehlgeschlagene Tests

Keine im letzten automatisierten Testlauf (`test-results.md`, Iteration 3: 1316 gesamt, 1314 bestanden, 0 fehlgeschlagen, 2 übersprungen). Ein Fehlschlag in der OsInterface-Lane beim ersten Durchlauf (`WpfE2ETests`-Kategorie, konkreter Testname durch Log-Rauschen nicht mehr isolierbar) wurde durch zwei vollständige Lane-Wiederholungen ohne Codeänderung dazwischen als Timing-Flakiness verifiziert (beide Wiederholungen: 0 Fehlschläge) — konsistent mit der bereits in Iteration 2 unabhängig verifizierten Flakiness von `WpfBasisSzenarien` (isolierter Rerun bestanden in 7,8s) und mit der in CLAUDE.md dokumentierten FlaUI/UI-Automation-Timing-Charakteristik dieses Projekts. Keine Hinweise auf eine echte Regression durch die Iteration-3-Änderungen.

## Zusammenfassung des Fortsetzungszyklus

Dieser Zyklus hat gegenüber dem vorherigen Abbruch (siehe Commit `63f3d9e`) folgende Befunde behoben:

**Iteration 1 (6 Befunde aus dem vorherigen `continue.md`):**
- `KannIdeAuswaehlen` wird jetzt zusätzlich einmalig am Ende von `LadenAsync` berechnet (`AktualisiereKannIdeAuswaehlenAsync`/`ErmittleIdeEntryPointsAsync`) — kritischer Bug behoben, Dropdown-Button ist jetzt beim ersten Anzeigen der View korrekt sichtbar.
- `plan.md`-Widerspruch zwischen „on-demand" und „bei Initialisierung" aufgelöst (Hybrid-Ansatz dokumentiert).
- README-Fehler (`VerfuegbareEinstiegspunkte`) entfernt.
- Veralteter XML-Doc-Kommentar korrigiert.
- `TaskDetailViewModelTestFactory.CreateVerzeichnisAktionenServices` zu `CreateArbeitsverzeichnisOeffnenService` umbenannt.
- 2 als potenziell fehlschlagend eingestufte E2E-Tests verifiziert (kein Änderungsbedarf durch den Root-Cause-Fix).

**Iteration 2 (6 neue Befunde):**
- Toter Code `IIdePlugin.OpenRepositoryAsync` vollständig entfernt (Interface, beide Implementierungen, Tests).
- Doppelte `KannIdeAuswaehlen`-Berechnung zu `BerechneKannIdeAuswaehlen`-Hilfsmethode extrahiert.
- `WaehleEntryPointAsync`-Fallback erzeugte ungültigen `IdeEntryPoint.Path` — gibt jetzt `null` zurück.
- Doppelte Guard-Klausel in `SettingsView.xaml.cs` zu `TryGetViewModelAndFirstAddedItem` extrahiert.
- `SettingsViewModel.MoveIdePlugin` nutzt jetzt `SafeFireAndForget`.
- `PluginSelectionServiceTests_IdePlugin.CreateSut` nutzt jetzt gemeinsamen `DbContext` (wie in Produktion).

**Iteration 3 (2 neue Befunde, 1 behoben, 1 verbleibt s. o.):**
- **Kritisch:** `OeffneIdeInternAsync` setzte `KannIdeAuswaehlen = false` im generischen `catch`-Block auch dann, wenn nur der Öffnen-Versuch fehlschlug (nicht die Einstiegspunkt-Ermittlung) — Dropdown-Button verschwand fälschlich nach einem fehlgeschlagenen Öffnen-Versuch trotz weiterhin vorhandener mehrerer Einstiegspunkte. Behoben, inkl. neuem Regressionstest.
- `SettingsView.xaml`-Duplikat (SCM/KI- vs. IDE-Plugin-Detailanzeige) zu neuem `PluginDetailPanel`-UserControl extrahiert.
- Verbleibend: doppelter Mock-Aufbau in zwei Testmethoden (s. o., geringfügig, rein testinterne Code-Qualität, keine funktionale Auswirkung).

**Empfehlung für den nächsten Durchlauf:** Der einzige verbleibende Befund ist geringfügig (Testcode-Duplikation ohne funktionale Auswirkung). Er sollte zusammen mit der als nächstes anstehenden, größeren Anforderungsänderung (Dropdown-Auswahl soll Einstiegspunkte über **alle kompatiblen IDE-Plugins** aggregieren, nicht nur über das eine aufgelöste Plugin — siehe separate Anforderung) miterledigt werden, da diese ohnehin `TaskDetailViewModelTests_IdeAuswahl.cs` grundlegend anfassen wird.

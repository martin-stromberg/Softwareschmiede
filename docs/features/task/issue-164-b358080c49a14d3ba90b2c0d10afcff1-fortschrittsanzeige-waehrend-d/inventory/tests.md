# Tests und Absicherung

## Vorhandene relevante Tests

`TaskDetailViewModelTests` deckt den Startpfad bereits breit ab:

- `StartenCommand` ist bei neuer Aufgabe verfuegbar und bei nicht neuer Aufgabe nicht verfuegbar (`TaskDetailViewModelTests.cs`, Zeilen 806-830).
- Plugin-Auswahldialog und Projekt-Default-Speicherung werden getestet (`TaskDetailViewModelTests.cs`, Zeilen 832-889).
- Erfolgreicher Start setzt CLI auf laufend und Status auf `Gestartet` (`TaskDetailViewModelTests.cs`, Zeilen 891-910).
- PseudoConsole-Session wird nach erfolgreichem Start weitergereicht (`TaskDetailViewModelTests.cs`, ab Zeile 932).

`EntwicklungsprozessServiceTests` und Integrationstests decken den Klon-/Branch-/Persistenzpfad ab. Treffer zeigen u. a.:

- Persistenz von Branch und Klonpfad.
- erwarteter Klonpfad unter konfiguriertem oder fallback Arbeitsverzeichnis.
- Protokolleintraege bei Arbeitsverzeichnis-Fallback.
- Working-Directory-Validierung nach dem Klon.

Plugin-Tests decken `CloneRepositoryAsync` fuer GitHub, Bitbucket und LocalDirectory ab, inklusive Fehlerfaellen und Guardrails.

## Fehlende Tests fuer diese Anforderung

Es gibt aktuell keinen Test, der waehrend eines noch laufenden Repository-Starts den Fusszeilentext beobachtet. Die bestehenden Starttests warten auf den kompletten Start und pruefen erst danach CLI-Zustand und Aufgabenstatus.

Empfohlene neue Tests:

- `TaskDetailViewModel.StartenAsync` setzt vor Abschluss des kombinierten Prozesses `CliStatusText` bzw. `FooterStatusText` auf exakt `Bereit Repository vor...`.
- Nach erfolgreichem Start wird der Vorbereitungsstatus durch den passenden CLI-Status ersetzt.
- Bei Fehler aus `ProzessStartenUndCliStartenAsync` wird der Vorbereitungsstatus nicht dauerhaft angezeigt und `FehlerMeldung` bleibt sichtbar.
- Bei Abbruch wird der Status ebenfalls zurueckgesetzt oder nicht falsch stehen gelassen.

## Testbarkeit des laufenden Starts

Da `TaskDetailViewModel` aktuell einen konkreten `EntwicklungsprozessService` injiziert bekommt, koennte ein laufender Await im ViewModel-Test schwieriger zu steuern sein, falls der Service nicht mockbar ist. In der vorhandenen Testfactory scheint der Service mit Mock-Plugins aufgebaut zu werden. Fuer einen deterministischen Zwischenzustand bieten sich zwei Wege an:

- Ein steuerbarer Fake-/Mock-`IGitPlugin.CloneRepositoryAsync`, der erst nach Freigabe einer `TaskCompletionSource` abschliesst.
- Falls die Implementierung einen separaten Statusdienst einfuehrt, diesen direkt testen und in ViewModel-Tests nur die Bindung/Statusuebernahme pruefen.

## UI/E2E

Ein E2E-Test ist moeglich, aber wegen timing-sensitiver Klonablaeufe wahrscheinlich teuer. Sinnvoller ist zunaechst ein ViewModel-Test mit kontrolliert blockierendem Clone. Ein E2E-Test kann spaeter fuer das echte UI-Verhalten ergaenzt werden, falls es bereits Infrastruktur fuer laengere LocalDirectory-Klons gibt.

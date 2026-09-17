# Usability-Review

## Ergebnis

**Status:** Befunde vorhanden

## Geprüfte Interaktionen

- Einstellungen → Auswahlbox „Updates" mit den Optionen „Aus", „Nur Pruefen", „Bei Programmstart pruefen und ausfuehren" (SettingsView.xaml:289-306, SettingsViewModel.cs:20-23) → Befund: Keine sichtbare Feldbezeichnung/Erläuterung zur Auswahlbox; Optionen ohne Umlaute geschrieben, obwohl die übrige Oberfläche „Prüfen"/„prüfen" verwendet.
- Einstellungen → Checkbox „Prerelease-Versionen laden" (SettingsView.xaml:307-310) → Befund: Fachbegriff „Prerelease" ohne jede Erklärung für nicht-technische Anwenderinnen.
- Einstellungen → „Speichern" (SettingsView.xaml:169-172, SettingsViewModel.cs:352-411) → unauffällig: Erfolgsmeldung „Einstellungen gespeichert." und verständliche Fehlermeldung („Der ausgewählte Update-Modus ist ungültig.") vorhanden.
- Einstellungen → „Verwerfen" (SettingsViewModel.cs:413-416) → unauffällig: lädt die gespeicherten Update-Werte erneut in die Oberfläche.
- Modus „Aus" → Update-Prüfung deaktiviert; „⟳ Prüfen"-Schaltfläche in der Seitenleiste wird deaktiviert (MainWindowViewModel.cs:721-725, MainWindow.xaml:157-172) → Befund: Deaktivierung ohne erkennbare Begründung für die Anwenderin (kein Tooltip-/Hinweistext).
- Modus „Nur Pruefen" → Prüfung beim Programmstart und manuell; gefundenes Update erscheint als „⇧ Update"-Schaltfläche mit Versions-Tooltip (MainWindow.xaml:139-155) → unauffällig.
- Modus „Bei Programmstart pruefen und ausfuehren" → automatische Installation mit Fortschrittsdialog und ggf. Sicherheitsabfrage bei laufenden Aufgaben (MainWindowViewModel.cs:483-486, 754-761) → unauffällig: Abfragetext nennt Anzahl und Liste der laufenden Aufgaben verständlich.
- Manuelle Update-Prüfung über „⟳ Prüfen" → Rückmeldung als Hinweistext unter den Schaltflächen, z. B. „Kein Update verfügbar. Die installierte Version ist aktuell." oder „Update-Prüfung ist fehlgeschlagen." (MainWindow.xaml:176-182, UpdateService) → unauffällig.
- Update-Start über „⇧ Update" → Sicherheitsabfrage, Fortschrittsdialog mit Phase, Prozentbalken und „Abbrechen" (UpdateProgressDialog.xaml) → unauffällig.
- Fortschrittsdialog → Abbruch durch die Anwenderin → unauffällig: Rückmeldung „Update-Vorbereitung wird abgebrochen." / „Update-Vorbereitung wurde abgebrochen." (UpdateProgressViewModel.cs:118-126, MainWindowViewModel.cs:667-671).
- Fortschrittsdialog → Fehler-/Endzustand (z. B. geänderte oder nicht lesbare Update-Einstellungen, Vorbereitungsfehler) (UpdateProgressViewModel.cs:100-107, UpdateProgressDialog.xaml:47-51) → Befund: Einzige Schaltfläche „Abbrechen" ist deaktiviert; Schließen ist nur über das Fenster-X möglich; die Fehlermeldung kann rohe Exception-Texte enthalten.

## Befunde

### 1. Auswahlbox „Updates" ohne sichtbare Beschriftung und Erläuterung

Im Abschnitt „Updates" (SettingsView.xaml:289-311) steht nur die Überschrift „Updates" über der Auswahlbox. Was genau die Auswahl steuert und wann geprüft wird, ist nicht erkennbar — die Beschriftung „Update-Modus" existiert nur als unsichtbarer AutomationProperties.Name. Andere Bereiche wie „Arbeitsverzeichnis" haben eine erklärende Zeile („Verzeichnis, in dem Repository-Klone gespeichert werden.").

**Empfehlung:** Sichtbare Feldbezeichnung (z. B. „Update-Prüfung") und eine kurze Erläuterung ergänzen, etwa „Legt fest, ob und wie nach Programmupdates gesucht wird." Optional Hinweis, dass „Nur prüfen" Updates lediglich anzeigt, aber nicht installiert.

### 2. Inkonsistente Schreibweise ohne Umlaute

Die Optionsbeschriftungen „Nur Pruefen" und „Bei Programmstart pruefen und ausfuehren" (SettingsViewModel.cs:20-23) verwenden ASCII-Umschreibungen, während die übrige Oberfläche korrekte Umlaute nutzt („Prüfen"-Schaltfläche und Tooltip „Auf Programmupdate prüfen" in MainWindow.xaml:157-168). Das wirkt auf eine Endanwenderin wie ein Tippfehler. Die Labels entsprechen zwar wörtlich der Anforderung, stehen aber im Widerspruch zur vorhandenen UI-Sprache.

**Empfehlung:** „Nur prüfen" und „Bei Programmstart prüfen und ausführen" verwenden (Anforderung ggf. entsprechend präzisieren).

### 3. Checkbox „Prerelease-Versionen laden" ohne Erklärung

„Prerelease" ist ein englischer Fachbegriff; einer nicht-technischen Anwenderin ist nicht klar, dass es sich um Vorabversionen (Beta/Release-Kandidaten) handelt und dass diese fehleranfälliger sein können (SettingsView.xaml:307-310).

**Empfehlung:** Beschreibungstext unter der Checkbox ergänzen, z. B. „Auch Vorabversionen (z. B. Beta-Versionen) bei der Update-Prüfung berücksichtigen. Diese können noch Fehler enthalten."

### 4. Deaktivierter „Prüfen"-Button bei Modus „Aus" ohne Begründung

Ist der Update-Modus auf „Aus" gestellt, ist die Schaltfläche „⟳ Prüfen" in der Seitenleiste dauerhaft deaktiviert (MainWindowViewModel.cs:721-725). Die Anwenderin erhält weder über Tooltip noch über den Hinweistext eine Erklärung, warum die Prüfung nicht verfügbar ist.

**Empfehlung:** Im deaktivierten Zustand einen Hinweis anzeigen, z. B. Tooltip/Hinweistext „Update-Prüfung ist in den Einstellungen deaktiviert."

### 5. Fortschrittsdialog im Fehlerzustand nur über Fenster-X schließbar; Fehlertext teils technisch

Nach einem Fehler (z. B. „Die Update-Einstellungen wurden geändert. Der Update-Vorgang wurde abgebrochen.") bleibt der Dialog geöffnet, die einzige Schaltfläche „Abbrechen" ist deaktiviert (UpdateProgressViewModel.cs:100-107, UpdateProgressDialog.xaml:47-51). Die Anwenderin muss erkennen, dass nur das Fenster-X den Dialog schließt. Zusätzlich wird bei Vorbereitungsfehlern die rohe Exception-Nachricht angezeigt („Update konnte nicht vorbereitet werden: {ex.Message}", MainWindowViewModel.cs:675), die technische Details enthalten kann.

**Empfehlung:** Im Fehlerzustand eine aktive „Schließen"-Schaltfläche anbieten und die Fehlermeldung auf eine verständliche Kernbotschaft reduzieren ( technische Details ggf. einklappbar oder nur im Hinweistext/Log).

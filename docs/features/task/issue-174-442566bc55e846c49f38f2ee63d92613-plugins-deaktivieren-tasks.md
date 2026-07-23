# Tasks: Issue 174 — Plugins-Einstellungen UI-Korrekturen

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | UI (Kontrast) | Beiden Aktivierungs-`ListBox`en in `SettingsView.xaml` themed `Background="{DynamicResource SurfaceBrush}"` und `Foreground="{DynamicResource PrimaryTextBrush}"` geben | Offen | — |
| 2 | UI (Kontrast) | `ItemContainerStyle` beider `ListBox`en von reinem `EventSetter` auf `ListBoxItem`-Style mit themed Hintergrund + `IsSelected`/`IsMouseOver`-Triggern umstellen | Offen | — |
| 3 | UI (Liste) | Aktivierungs-`CheckBox` aus dem `ItemTemplate` der SCM-`ListBox` entfernen; Namens-`TextBlock` inkl. `AutomationProperties.Name="{...}.Eintrag"` beibehalten | Offen | — |
| 4 | UI (Liste) | Aktivierungs-`CheckBox` aus dem `ItemTemplate` der KI-`ListBox` entfernen; Namens-`TextBlock` inkl. `AutomationProperties.Name="{...}.Eintrag"` beibehalten | Offen | — |
| 5 | UI (rechter Bereich) | `ScrollViewer Grid.Column="2"`-Inhalt in ein `StackPanel` umbauen (Kopfzeile + CheckBox + bestehendes `ItemsControl`) | Offen | — |
| 6 | UI (rechter Bereich) | Kopfzeilen-`TextBlock` gebunden an `SelectedPlugin.PluginName` (Bold, `PrimaryTextBrush`) einfügen | Offen | — |
| 7 | UI (rechter Bereich) | Aktivierungs-`CheckBox` „Plugin aktiviert" mit `AutomationProperties.Name="PluginAktiviert"` und `IsChecked="{Binding SelectedPlugin.IsEnabled, Mode=TwoWay}"` einfügen | Offen | — |
| 8 | UI (Sichtbarkeit) | Kopfzeile + CheckBox über `NullOrEmptyToVisibilityConverter` an `SelectedPlugin` sichtbar schalten (null → Collapsed) | Offen | — |
| 9 | Code-Behind | Handler `OnPluginActivationItemPreviewMouseLeftButtonDown` in `SettingsView.xaml.cs` entfernen | Offen | — |
| 10 | E2E-Tests | `E2E_PluginAktivierung.DeaktivierePlugin`-Helper auf „Item selektieren (`{prefix}.Eintrag`) → CheckBox `PluginAktiviert` umschalten" umstellen | Offen | — |
| 11 | E2E-Tests | SCM-Validierungsphase in `E2E_PluginAktivierung` auf neuen Ablauf umstellen (Item selektieren, rechte CheckBox deaktivieren, Fehlermeldung prüfen) | Offen | — |
| 12 | E2E-Tests | Reload-Assertion in `E2E_PluginAktivierung` auf „ClaudeCli-Item selektieren → CheckBox `PluginAktiviert` IsChecked == false" umstellen | Offen | — |
| 13 | Verifikation | Voller Build + reguläre Unit-Tests (`SettingsViewModelTests`, `PluginActivationServiceTests`) und angepasster E2E-Test (`Category=OsInterface`) ausführen | Offen | — |

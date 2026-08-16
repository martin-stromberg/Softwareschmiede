# ViewModels, CLI-Anzeige und Navigation

## Neuerstellung und Speichern

[`ProjectDetailViewModel.AufgabeErstellenAsync`](../../../../../src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs) legt die Aufgabe mit Status `Neu` an, aktualisiert die Listen und ruft `OeffneAufgabe` auf. Die neue TaskDetailView wird also bereits geoeffnet.

`OeffneAufgabe` erzeugt ein `TaskDetailViewModel`, setzt Ruecknavigation und Listen-Callback und weist anschliessend `AufgabeId` zu. Der Listen-Callback aktualisiert die Projektliste nach einer Speicherung.

 [`TaskDetailViewModel.SpeichernAsync`](../../../../../src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs) speichert, laedt die Aufgabe neu und aktualisiert die Liste. Danach ruft die Methode jedoch `ZurueckAction` auf. Im Projektkontext fuehrt das zurueck zur Projektansicht. Das ist die direkte Stelle fuer die Anforderung "Aufgabenseite bleibt offen".

## Aktueller CLI-Autostart

`LadenAsync` laedt die Aufgabe, prueft den laufenden Prozess und bindet eine vorhandene PseudoConsoleSession wieder an. Danach gilt: Wenn `Aufgabe.Status == Gestartet` und aktuell kein KI-Prozess laeuft, wird `CliAutomatischNeustartenAsync` aufgerufen. Das betrifft sowohl eine absichtlich gestoppte/beendete CLI als auch einen Prozess, der beim Programmneustart nicht mehr im Speicher existiert.

Die CLI-Ansicht wird ueber `ShowCliPanel` aus den Gesamtstatuswerten `Gestartet`/`Wartend` abgeleitet. `ShowDiffPanel` gilt fuer `Beendet`. Ein separater Ausfuehrungsstatus wird in den UI-Bindings derzeit nicht angezeigt.

## Commands und Sperren

`StartenCommand` ist aktuell nur fuer `AufgabeStatus.Neu` zulaessig. `CliNeustartenCommand` ist fuer `Gestartet`/`Wartend` ohne laufenden Prozess vorgesehen. `AufgabeAbschliessenCommand` ist sichtbar, wenn das CLI-Panel sichtbar und kein Prozess aktiv ist. Die neue Anforderung muss Starten fuer `Neu` und einen beendeten KI-Lauf erlauben, aber fuer Gesamtstatus `Beendet` sperren.

## MainWindow-Navigation

[`MainWindowViewModel.NavigateZuAufgabe`](../../../../../src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs) erzeugt bei jedem Aufruf ein neues `TaskDetailViewModel`, setzt Titel und Ruecknavigation und weist die ID zu. Die Seitenleiste listet nur Aufgaben mit Gesamtstatus `Gestartet` oder `Wartend`. Eine Aufgabe mit beendetem KI-Lauf bleibt deshalb zwar in der Projektliste, aber nicht in der aktiven Seitenleiste.

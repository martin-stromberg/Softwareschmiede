# Detailinventar - Persistenz und Validierung

## Speicherung

`ProjectDetailViewModel` speichert das gewaehlte Arbeitsverzeichnis in zwei Pfaden:

- beim Repository-Zuweisen ueber `SaveRepositoryWorkingDirectoryAsync(gitRepository.Id, vm.SelectedWorkingDirectory, ct)` in Zeile 400
- beim spaeteren Bearbeiten ueber `SaveRepositoryWorkingDirectoryAsync(repository.Id, vm.SelectedWorkingDirectory, ct)` in Zeile 463

`ProjektService.SaveRepositoryWorkingDirectoryAsync` beginnt bei Zeile 299.

Aktuelles Verhalten:

- `null`, leer oder `"."` werden als `null` gespeichert.
- andere Werte werden unveraendert als `WorkingDirectoryRelativePath` gespeichert.
- falls keine Startkonfiguration existiert, wird eine erzeugt.

`RepositoryStartKonfiguration.WorkingDirectoryRelativePath` ist die persistierte Spalte beziehungsweise Entity-Property fuer den relativen Arbeitsverzeichnis-Pfad.

## Laufzeitauflosung

`WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync` nutzt `RepositoryStartKonfiguration.WorkingDirectoryRelativePath`, kombiniert ihn mit dem Repository-Root und validiert das Ergebnis.

`WorkingDirectoryResolver.ValidateWorkingDirectory` prueft, ob das effektive Verzeichnis existiert. Das ist fuer die spaetere Aufgaben-/CLI-Ausfuehrung relevant, nicht fuer den Dialog zur Auswahl oder manuellen Eingabe.

## Relevanz fuer die Anforderung

Die Persistenz kann manuelle Werte grundsaetzlich aufnehmen, weil `SaveRepositoryWorkingDirectoryAsync` nicht auf die vorher geladene Auswahl beschraenkt. Die UI verhindert aktuell aber praktische freie Eingaben, weil nur eine nicht editierbare `ComboBox` angezeigt wird.

Beim nachtraeglichen Bearbeiten ist ein vorhandener Wert bereits geschuetzt: `ArbeitsverzeichnisBearbeitenViewModel` fuegt ein aktuelles Arbeitsverzeichnis, das nicht in der geladenen Struktur vorkommt, wieder zur Auswahl hinzu und waehlt es aus.

## Validierungsfrage

Die Anforderung laesst offen, ob manuelle Eingaben validiert werden sollen. Der vorhandene Persistenzpfad akzeptiert jeden nicht-leeren String ausser `"."` als relativen Pfad. Der Runtime-Resolver verhindert spaeter Pfade ausserhalb des Repository-Roots und nicht existierende effektive Verzeichnisse.

Fuer die Planung ist daher zu entscheiden:

- minimaler Fallback: manuelle Eingabe speichern, bestehende Runtime-Validierung reicht
- strikter Fallback: bereits im Dialog syntaktisch nur relative Pfade ohne Traversal erlauben

Die Akzeptanzkriterien verlangen mindestens, dass `"."` und manuell eingegebene relative Unterverzeichnisse gespeichert und wieder angezeigt werden.


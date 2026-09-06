# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### EntwicklungsprozessService.cs (EntwicklungsprozessService)

- **Hardcoded Value** — `PrepareCloneDirectoryAsync`, Zeile 463: Der Pfad-Bestandteil `"softwareschmiede"` ist als String-Literal hartcodiert: `Path.Combine(workdirResult.ResolvedPath, "softwareschmiede", aufgabeId.ToString())`. Dieser Name taucht auch in den Tests als Literal auf (z. B. `Path.Combine(uniqueBase, "softwareschmiede", aufgabe.Id.ToString())`), was zeigt, dass Änderungen an zwei Stellen gepflegt werden müssen.

  Empfehlung: Den Wert `"softwareschmiede"` als private Klassenkonstante `private const string KlonBasisVerzeichnis = "softwareschmiede";` extrahieren und sowohl im Service als auch in den Tests über diesen Bezeichner referenzieren.

- **God Method** — `SetupBranchAsync` (Zeilen 485–568, 84 Zeilen): Die Methode behandelt drei konzeptuell getrennte Branch-Fälle hintereinander: (1) Checkout einer Pull-Request-Review-Quelle, (2) Checkout eines vorhandenen Remote-Branches, (3) Anlage eines neuen Task-Branches mit optionalem Basis-Branch. Jeder Fall hat eigene Vorbedingungen, Validierungen und Logging-Ausgaben.

  Empfehlung: Die drei Fälle in private Hilfsmethoden auslagern, z. B. `CheckoutReviewSourceAsync`, `CheckoutExistingBranchAsync` und `CreateNewTaskBranchAsync`. `SetupBranchAsync` delegiert dann nur noch an die passende Methode.

- **Doppelte Repository-Auflösung in `ProzessStartenUndCliStartenAsync`** (Zeilen ~136–147): `ResolveRepositoryAsync` und `ResolvePluginAsync` werden ein zweites Mal aufgerufen, obwohl `ProzessStartenAsync` (das unmittelbar davor gerufen wird) dieselbe Auflösung bereits intern durchgeführt hat. Ein Kommentar im Code erklärt, warum `aufgabe.GitRepositoryId` nicht als Abkürzung genutzt werden kann – das Problem ist aber, dass `ProzessStartenAsync` seine Zwischen­ergebnisse nicht zurückgibt und der Aufrufer sie deshalb neu berechnen muss.

  Empfehlung: `ProzessStartenAsync` in einen internen Overload aufteilen, der `GitRepository` und `IGitPlugin` zurückgibt (z. B. `ProzessStartenCoreAsync`), und `ProzessStartenUndCliStartenAsync` auf diese aufgelösten Objekte zugreifen lassen, statt die Auflösung zu wiederholen.

- **Lazy Class** — `StartskriptErgebnis` (Zeile 855): Das `sealed record StartskryptErgebnis(string Message)` am Ende der Datei enthält ausschließlich eine `string`-Eigenschaft und fügt gegenüber einem einfachen `string`-Rückgabewert keinen semantischen Mehrwert hinzu. `RepositoryStartskriptAusfuehrenAsync` gibt diesen Typ zurück, der Aufrufer greift direkt auf `.Message` zu.

  Empfehlung: Den Rückgabetyp von `RepositoryStartskriptAusfuehrenAsync` auf `string` ändern und `StartskriptErgebnis` entfernen.

---

### EntwicklungsprozessServiceTests.cs (EntwicklungsprozessServiceTests)

- **Doppelter Code — SUT-Verdrahtung** (8 Vorkommen): Das Muster zum Anlegen einer lokalen `EntwicklungsprozessService`-Instanz mit `new ProjektService(...)`, `CreatePluginSelectionService(...)` und vollem Konstruktoraufruf wiederholt sich in acht Testmethoden nahezu identisch. Nur die `EntwicklungsprozessServiceOptions` variieren. Betroffen u. a.: `ShouldUseConfiguredWorkingDirectory`, `ShouldRollback_WhenConfiguredWorkingDirectoryMissing`, `ShouldContinue_WhenRepositoryStartScriptFails`, `ShouldThrow_WhenRepositoryContextIsAmbiguous`, `ShouldStartTaskWithoutIssueReference`, `ShouldLogWarning_WhenFileCreationFails`, `ShouldLogWarning_WhenFileOperationFails`, `GetRemoteBranchesAsync_ShouldResolvePluginBySelectedPrefix`.

  Empfehlung: Eine private Hilfsmethode `CreateSut(EntwicklungsprozessServiceOptions options, ILogger<EntwicklungsprozessService>? logger = null)` einführen, die die Verdrahtung zentralisiert. Die acht Tests übergeben dann nur noch ihre spezifischen Options.

- **Überflüssige Bereinigung** (Zeilen 1051–1052 und 1101–1102): In den Tests `UpdateGitignoreAsync_ShouldLogWarning_WhenFileOperationFails` und `ProzessStartenAsync_ShouldContinue_WhenGitignoreUpdateFails` wird zunächst das tiefe Unterverzeichnis (`Path.Combine(uniqueBase, "softwareschmiede", aufgabe.Id.ToString())`) gelöscht und danach nochmals `uniqueBase` selbst. Da `DeleteDirectoryIfExists` rekursiv löscht, ist der erste Aufruf redundant.

  Empfehlung: Den ersten `DeleteDirectoryIfExists`-Aufruf auf das Unterverzeichnis entfernen; das Löschen von `uniqueBase` reicht aus.

## Geprüfte Dateien

- `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`
- `src/Softwareschmiede.Tests/Application/Services/EntwicklungsprozessServiceTests.cs`

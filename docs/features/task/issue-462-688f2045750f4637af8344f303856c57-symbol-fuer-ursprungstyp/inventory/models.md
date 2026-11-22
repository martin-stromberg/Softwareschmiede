# Datenmodell — Symbol für Auftragsart auf den Aufgabenkacheln (Issue #462)

## `ActiveWorkItemPanelItem`
Datei: `src/Softwareschmiede.App/ViewModels/ActiveWorkItemPanelItem.cs`

UI-Daten einer Aufgabe in der aktiven Aufgabenliste (Kachelmodell für `ActiveTasksListControl`). Alle Properties außer `IsActive` sind `init`-only; befüllt wird die Klasse ausschließlich in `MainWindowViewModel.MapActiveWorkItemPanelItem`.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige ID der Aufgabe (Z. 12) |
| `Title` | `string` | Titel; Basis aller `…_{Title}`-AutomationIds auf der Kachel (Z. 15) |
| `RepositoryName` | `string` | Untertitel der Kachel: `GitRepository.RepositoryName`, Fallback Projektname (Z. 18) |
| `ScmPluginName` | `string?` | Anzeigename des SCM-/SCI-Plugins bzw. gespeicherter Prefix (Z. 21); Tooltip „SCM: {0}" |
| `ScmPluginSymbol` | `string?` | Unicode-Glyph des SCM-Plugins in der Symbolzeile (Z. 24); `null`/leer → Element ausgeblendet |
| `AiPluginName` | `string?` | Anzeigename des KI-Plugins bzw. gespeicherter Prefix (Z. 27); Tooltip „KI: {0}" |
| `AiPluginSymbol` | `string?` | Unicode-Glyph des KI-Plugins in der Symbolzeile (Z. 30); `null`/leer → Element ausgeblendet |
| `RepositoryGroupName` | `string` | Gruppenname im Repository-Modus (Z. 33) |
| `RepositoryGroupIsFallback` | `bool` | Fallback-Markierung, sortiert „Ohne Repository" ans Ende (Z. 36) |
| `AiGroupName` | `string` | Gruppenname im KI-Modus (Z. 39) |
| `AiGroupIsFallback` | `bool` | Fallback-Markierung, sortiert „Nicht ausgeführt" ans Ende (Z. 42) |
| `Kind` | `WorkItemKind` | Auftragsart der Aufgabe — fachliche Basis der Typ-Markierung (Z. 45) |
| `KindGroupName` | `string` | Deutscher Anzeigename der `Kind`; Gruppenname im Modus „Art" (Z. 48). Enthält bei `Unspecified`/unbekannt den Fallback „Ohne Auftragsart" (`WorkItemListLayout.WithoutKind`) |
| `KindSortOrder` | `int` | Abgeleitet (Z. 51): `Kind == Unspecified` → `int.MaxValue`, sonst `(int)Kind` — `Unspecified` sortiert im „Art"-Modus ans Ende |
| `KindLabel` | `string` | Sichtbarer Badge-Text auf der Kachel (Z. 54); leer bei `Unspecified` → Badge ausgeblendet (`NullOrEmptyToVisibilityConverter`) |
| `SortTimestamp` | `DateTimeOffset` | Sortierzeitpunkt (`LastCliStartUtc ?? CreatedAt`) (Z. 57) |
| `RunStatus` | `WorkItemRunStatus?` | Laufzeit-Substatus (Z. 60) |
| `LiveRunStatus` | `WorkItemRunStatus?` | Live-Substatus aus `IRunningAutomationStatusSource` (Z. 63) |
| `Status` | `WorkItemStatus` | Aufgabenstatus (Z. 66) |
| `ExecutionStatus` | `WorkItemExecutionStatus` | Persistierter Lebenszyklusstatus der KI-Ausführung (Z. 69) |
| `ActiveRunId` | `string?` | Aktive Lauf-ID (Z. 72) |
| `LastHeartbeatUtc` | `DateTimeOffset?` | Letzter Heartbeat (Z. 75) |
| `LastCliStartUtc` | `DateTimeOffset?` | Letzter echter CLI-Start (Z. 78) |
| `PausedUntilUtc` | `DateTimeOffset?` | Pausiert-bis-Zeitpunkt (Z. 81) |
| `IsPaused` | `bool` | Abgeleitet: `PausedUntilUtc` in der Zukunft (Z. 84); steuert Kachel-Abblendung |
| `HasScheduledPrompt` | `bool` | Zeitgesteuerter Prompt in Warteschlange (Z. 87) |
| `OpenTodoCount` | `int` | Anzahl offener To-dos (Z. 90) |
| `OpenTodoLabelText` | `string` | Abgeleitet: „1 Todo" / „{n} Todos" (Z. 93) |
| `ShowOpenTodosCommand` | `ICommand?` | Öffnet den To-do-Dialog (Z. 96) |
| `IsActive` | `bool` | Setzbar (Z. 99–103): markiert die im Inhaltsbereich angezeigte Aufgabe; Accent-Rahmen der Kachel |

**Hinweis:** Eine `KindSymbol`-Property (Unicode-Glyph der Auftragsart) existiert noch nicht — der Badge-Bereich kennt aktuell nur `Kind`/`KindGroupName`/`KindLabel`/`KindSortOrder`.

## `WorkItem`
Datei: `src/Softwareschmiede/Domain/Entities/WorkItem.cs`

Nur die für die Anforderung relevante Eigenschaft:

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Kind` | `WorkItemKind` | Auftragsart = fachlicher Ursprung der Aufgabe (Z. 126, Issue #451); Default `WorkItemKind.Unspecified`. Persistiert als `Aufgaben.Auftragsart` (string-Konversion, `SoftwareschmiedeDbContext` Z. 206–209). Bewusst von `Purpose` (Z. 120, Routing/Isolation) getrennt — die Aufbereitungsaufgabe behält `Purpose = "IssueAufbereitung"`, wechselt aber nach Prompt-Zustellung zu `Kind = Instruction` |
| `Purpose` | `string?` | Zweck-Markierung (z. B. `"IssueAufbereitung"`); kein Anzeigeattribut (Z. 120) |

## `ListCollectionView`-Gruppierung (kein eigenes Modell)

Die Gruppenheader im „Art"-Modus binden `CollectionViewGroup.Name` (= Wert von `KindGroupName`) als Klartext-`TextBlock` — es gibt kein Gruppen-ViewModel und keinen Symbol-Pfad für Gruppenköpfe (Scope-Ausschluss der Anforderung deckt sich mit dem Ist-Zustand).

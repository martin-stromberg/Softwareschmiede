# Anforderungsanalyse – Agent-Auswahl bei Folgeanweisungen

> **Dokument-Typ:** Requirements Analysis  
> **Status:** Umgesetzt  
> **Version:** 1.0.0  
> **Thema:** Agent-Auswahl bei Folgeanweisungen auf der Aufgabendetailseite

---

## 1. Überblick

Anwender geben nach einem KI-Lauf oft weitere Anweisungen ein.
Dabei soll klar sein, welcher Agent die nächste Anweisung erhält.
Die Bedienung soll einfach bleiben und Fehlbedienung vermeiden.
Der erste Prompt der Aufgabe darf sich durch dieses Feature nicht ändern.

---

## 2. Funktionale Anforderungen

| Kennung | Beschreibung | Priorität | Status |
|---|---|---|---|
| FR-1 | Der Bereich für Folgeanweisungen ist nur sichtbar, wenn die Aufgabe in **In Bearbeitung** ist und bereits eine KI-Antwort vorliegt. | MUST HAVE | Umgesetzt |
| FR-2 | Die Agenten-Auswahl bei Folgeanweisungen ist beim Laden auf den Start-Agenten der Aufgabe gesetzt. | MUST HAVE | Umgesetzt |
| FR-3 | Anwender können die Agenten-Auswahl vor dem Senden manuell ändern. | MUST HAVE | Umgesetzt |
| FR-4 | Das Senden nutzt den aktuell ausgewählten Agenten. Danach springt die Auswahl wieder auf den Start-Agenten zurück. | MUST HAVE | Umgesetzt |
| FR-5 | Das Verhalten des ersten Prompts bleibt unverändert. | MUST HAVE | Umgesetzt |

---

## 3. Akzeptanzkriterien

1. **Sichtbar/verfügbar:** Folgeanweisungsfeld und Agenten-Auswahl sind im passenden Zustand sichtbar und nutzbar.
2. **Default = Initial-Agent:** Beim Laden steht der Start-Agent als Standardwert in der Auswahl.
3. **Auswahl änderbar:** Vor dem Senden kann ein anderer Agent gewählt werden.
4. **Versand an ausgewählten Agenten:** Die Folgeanweisung wird an den gewählten Agenten gesendet.
5. **Initialprompt unverändert:** Der erste Prompt funktioniert weiterhin wie bisher.

---

## 4. Nachweis durch implementierte Prüfungen

| Prüffall | Aussage |
|---|---|
| `AufgabeDetailFolgePromptTests.FolgePromptMarkup_ShouldContainAgentSelectionBinding` | Belegt Sichtbarkeit und Bindung der Agenten-Auswahl im Folgebereich. |
| `AufgabeDetailFolgePromptTests.OnInitializedAsync_ShouldDefaultFolgeAgentToInitialAgent` | Belegt Standardwert = Start-Agent beim Laden. |
| `AufgabeDetailFolgePromptTests.FolgePromptAsync_ShouldUseSelectedFollowAgent_AndResetToInitialAgent` | Belegt änderbare Auswahl, Versand an gewählten Agenten und Rücksetzen auf Start-Agent. |
| `EntwicklungsprozessServiceTests.KiStartenAsync_ShouldForwardSelectedAgentToPlugin` | Belegt Weitergabe des gewählten Agenten an die Ausführung. |
| `AufgabeDetailFolgePromptTests.KiStartenAsync_ShouldKeepInitialPromptBehavior` | Belegt unverändertes Verhalten des ersten Prompts. |

---

## 5. Referenzen

- UI: `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor`
- Logik: `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs`
- Tests:  
  - `src/Softwareschmiede.Tests/Components/Pages/Aufgaben/AufgabeDetailFolgePromptTests.cs`  
  - `src/Softwareschmiede.Tests/Application/Services/EntwicklungsprozessServiceTests.cs`

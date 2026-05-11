# Architektur-Review – Agent-Auswahl bei Folgeanweisungen

> **Dokument-Typ:** Architektur-Review  
> **Projekt:** Softwareschmiede  
> **Scope:** Bewertung des umgesetzten Folgeanweisungs-Flows mit Agenten-Auswahl  
> **Datum:** 2026-05-11

---

## 1. Executive Summary

Die Umsetzung ist konsistent zur bestehenden Architektur und erfüllt die fünf Akzeptanzkriterien. Der Flow ergänzt die UI ohne Contract- oder Persistenzänderung und bleibt kompatibel zum bestehenden Initialprompt-Ablauf.

**Gesamtbewertung:** ✅ **Freigabe**

---

## 2. Bewertungsmatrix

| Bereich | Bewertung | Kurzbegründung |
|---|---|---|
| Systemarchitektur | Sehr gut | Bestehende Schichten und Servicegrenzen bleiben erhalten. |
| UI/UX | Sehr gut | Folgeagent ist sichtbar, änderbar, danach kontrollierter Reset. |
| Datenmodell | Sehr gut | Keine Schema- oder Entitätsänderung nötig. |
| Testbarkeit | Sehr gut | Feature durch gezielte Unit-Tests abgesichert. |

---

## 3. Findings

| ID | Priorität | Bereich | Finding | Bewertung |
|---|---|---|---|---|
| F-01 | Low | UX-Konsistenz | Default- und Reset-Verhalten des Folgeagenten ist implementiert und nachvollziehbar. | Positiv |
| F-02 | Low | Stabilität | Initialprompt-Pfad bleibt unverändert und separat geführt. | Positiv |
| F-03 | Low | Technik | Keine Änderungen an `IKiPlugin`-Signatur, daher kein Migrationsbedarf. | Positiv |

---

## 4. Verifikation gegen Akzeptanzkriterien

| Kriterium | Ergebnis | Nachweis |
|---|---|---|
| 1) Agent-Auswahl sichtbar/verfügbar | Erfüllt | `AufgabeDetail.razor`, Test `FolgePromptMarkup_ShouldContainAgentSelectionBinding` |
| 2) Default = Initial-Agent | Erfüllt | `AufgabeDetail.razor.cs`, Test `OnInitializedAsync_ShouldDefaultFolgeAgentToInitialAgent` |
| 3) Auswahl vor Absenden änderbar | Erfüllt | UI-Bindung `_folgeAgentName`, Test `FolgePromptAsync_ShouldUseSelectedFollowAgent_AndResetToInitialAgent` |
| 4) Versand an ausgewählten Agenten | Erfüllt | `FolgePromptAsync` + `KiStartenAsync_ShouldForwardSelectedAgentToPlugin` |
| 5) Initialprompt unverändert | Erfüllt | Test `KiStartenAsync_ShouldKeepInitialPromptBehavior` |

---

## 5. Empfehlung

Das Feature ist architektonisch stimmig umgesetzt. Keine weiteren Änderungen erforderlich.


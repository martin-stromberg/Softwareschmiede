# Requirement: Anzeige der ausgeführten CLI

**Issue:** Die Anzeige der ausgeführten CLI für eine Aufgabe in der Fußleiste der Aufgabendetailansicht sowie in dem Button des Programmmenüs zeigt den falschen CLI-Namen an, wenn ein Projekt eine Standard-CLI eingestellt hat und diese in der Aufgabe gewechselt wird. Es wird dann weiterhin die zuerst gestartete Standard-CLI ausgegeben.

**Akzeptanzkriterien:**

1. Nach einem Plugin-Wechsel in der Aufgabendetailansicht zeigt die Fußzeile (`AktiverCliName`) den neuen CLI-Namen.
2. Nach einem Plugin-Wechsel zeigt der Programmmenü-Button / die Seitenleiste (`KiPluginName`) den neuen CLI-Namen.
3. Ein anschließender manueller CLI-Neustart verwendet das geänderte Plugin, nicht den Projekt-Default.
4. Der bestehende E2E-Test für den CLI-Wechsel wird um die Prüfung der Namensausgabe ergänzt.

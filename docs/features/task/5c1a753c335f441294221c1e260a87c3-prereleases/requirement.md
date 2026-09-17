# Requirement: Prereleases bei Updates

**Issue:** Die Updatefunktionalitaet soll in den Einstellungen konfigurierbar werden. Eine Auswahlbox soll festlegen, ob Updates deaktiviert, nur geprueft oder beim Programmstart geprueft und bei einem gefundenen Update automatisch installiert werden. Zusaetzlich soll ueber eine Checkbox festgelegt werden koennen, dass auch Prerelease-Versionen geladen werden.

**Akzeptanzkriterien:**

1. In den Einstellungen steht eine Auswahlbox mit den Optionen `Aus`, `Nur Pruefen` und `Bei Programmstart pruefen und ausfuehren` zur Verfuegung.
2. Die Auswahl `Aus` deaktiviert die Updatepruefung.
3. Die Auswahl `Nur Pruefen` fuehrt die Updatepruefung aus, installiert ein gefundenes Update jedoch nicht automatisch.
4. Bei der Auswahl `Bei Programmstart pruefen und ausfuehren` wird die Updatepruefung beim Programmstart ausgefuehrt und ein gefundenes Update automatisch installiert.
5. In den Einstellungen steht eine Checkbox zur Verfuegung, mit der das Laden von Prerelease-Versionen aktiviert werden kann.
6. Die Updatepruefung beruecksichtigt Prerelease-Versionen nur, wenn die Checkbox aktiviert ist.
7. Die Einstellungen werden bei der Updatepruefung und beim automatischen Ausfuehren der Installation konsistent beruecksichtigt.


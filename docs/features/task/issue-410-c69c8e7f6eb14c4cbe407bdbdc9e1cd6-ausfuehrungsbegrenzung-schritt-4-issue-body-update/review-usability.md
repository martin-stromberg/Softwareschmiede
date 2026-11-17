# Usability-Review: SCM-Antwort-Art „Issue-Beschreibung aktualisieren" (Schritt 4)

**Status: Keine Befunde**

Der einzige UI-berührende Diff ist die Ergänzung eines Fehlertext-Labels in `ScmEventQueueViewModel.ActionLabel` (`issuebody` → „Issue-Beschreibung"). Keine neuen Sichten, Schaltflächen, Eingabefelder oder Benutzerflüsse.

## Prüfung aus Endanwendersicht

- **Interne Kennungen:** Die neue Antwort-Art ist für Anwender nicht direkt bedienbar und wird es in diesem Schritt bewusst nicht sein (Aufrufer folgt in Schritt 6). Sichtbar wird lediglich der Zweck in der bestehenden Fehlerdiagnose der SCM-Ereignisübersicht — dort erscheint nun der Klartext „Issue-Beschreibung" statt einer internen Kennung oder eines falschen Generikums („KI-Antwort"). ✅
- **Angebotene Aktionen stimmen mit Wirkung überein:** Es gibt keine neue Bedienaktion; die bestehende „Erneut prüfen"-Heilung greift für gesperrte `issuebody`-Aktionen unverändert (Integrationstest `PermissionFailure_LocksUntilRetry` belegt die Aufhebung über `RetryAsync`). ✅
- **Verständlichkeit:** „Issue-Beschreibung" benennt die betroffene Remoteaktion in derselben Klartext-Qualität wie „Empfangsbestätigung"/„Wartehinweis"/„Screenshot-Nachweis". ✅
- **Remote-Wirkung:** Die geschriebene Issue-Beschreibung enthält einen ausgeblendeten HTML-Kommentar-Marker — im gerenderten Issue unsichtbar, im Rohtext vorhanden (identisch zum etablierten Kommentar-Marker). Kein Endanwender-Text wird verändert; der Titel bleibt unberührt. ✅

## Befunde

Keine.

# Requirement: Eventgetriebene Aufgabenbearbeitung mit Devin v3000

**Issue:** Die eventgetriebene Aufgabenbearbeitung funktioniert mit Devin aktuell nicht mehr, weil die Version geändert wurde von v3000.11.* zu v3000.12.* Wir müssen hier toleranter sein und v3000.*.* unterstützen.

**Hinweis zur Erstellung:** Dieses Dokument wurde retrospektiv nach Abschluss der Umsetzung erstellt. Der Lifecycle-Schritt wurde bei dieser Task versäumt; die nachfolgenden Artefakte dokumentieren den tatsächlichen Ablauf und den gemergten Stand (PR #327, Merge-Commit `ee90bb4`).

**Akzeptanzkriterien:**

1. Eine gestartete Devin CLI der Version `v3000.12.x` wird vom SCM-Ereigniskanal als unterstützt erkannt; Ereignisse werden zugestellt statt mit „wird nicht unterstützt" wartend zu bleiben.
2. Grundsätzlich wird jede Devin-CLI-Version `v3000.*.*` (gleiches Major, beliebige Minor- und Patch-Version) unterstützt.
3. Versionen außerhalb von Major 3000 (`< 3000.0.0` sowie `>= 3001.0.0`) bleiben weiterhin nicht unterstützt und melden einen ehrlichen Wartegrund mit CLI-Name, erkannter Version und unterstütztem Bereich.
4. Der im Wartegrund angezeigte unterstützte Bereich und die Hilfedokumentation nennen den neuen Bereich `v3000.x`.

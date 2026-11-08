# Offene Aufgaben

Erstellt am: 2026-11-08
Abbruchgrund: Kein Fortschritt zwischen den letzten zwei Iterationen

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine — `review.md` meldet „Vollständig umgesetzt" (25/25 Tasks erledigt).

## Code-Review-Befunde

- [ ] `UpdateE2EFixture.cs` — `new UpdateOptions()` 4× nur zum Lesen von Defaults (Zeilen 35 ×2, 384, 402, 419): gemeinsame `private static readonly UpdateOptions StandardOptionen = new();` einführen und überall wiederverwenden
- [ ] `UpdateE2EFixture.cs` — `SetzeStandardAntworten` (Zeilen 156–179): vier nahezu identische `Antworten.Add`-Blöcke in eine Schleife über (Url, ZipPfad)-Paare überführen
- [ ] `UpdateE2EFixture.cs` + `E2E_UpdateSettings.cs` — Release-Objekt-Literale (`tag_name`/`prerelease`/`draft`/`published_at`/`assets`) 5× dupliziert (Zeilen 482–492, 497–507 sowie 1463–1474, 1475–1486, 1494–1505): Fabrik `ErstelleReleaseJson` analog zu `ErstelleAssetJson` einführen
- [ ] `GitHubReleaseClientTests.cs` + `UpdatePackageServiceTests.cs` — Fallback-/Primary-URL-Literale erneut gestreut (5× bzw. 3× plus `CreateUpdateInfo` und identisches 500-Arrange): Datei-Konstanten bzw. kleine Arrange-Helfer einführen

## Fehlgeschlagene Tests

Keine — `test-results.md` meldet „Keine Fehler" (2200/2200 bestanden, 1 plattformbedingt übersprungen; `Category=OsInterface`-Lane in der Sandbox nicht ausgeführt).

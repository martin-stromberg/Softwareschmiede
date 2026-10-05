# Usability-Review – Texte kopieren

Status: **Befunde vorhanden**

## Prüfumfang

Geprüft wurde die aktuelle Terminaloberfläche ausschließlich gegen
`requirement.md`, aus der Perspektive einer nicht-technischen Person: Text in
laufender CLI-Ausgabe markieren und anschließend zuverlässig in die
Zwischenablage übernehmen.

## Positiv

- Text kann per Ziehen mit der linken Maustaste markiert werden; die Markierung
  ist sichtbar hervorgehoben.
- Für eine vorhandene Auswahl stehen zwei verständliche Kopierwege bereit:
  Kontextmenü **Kopieren** und `Strg+Umschalt+C`. Das Kontextmenü zeigt die
  Tastenkombination an.
- `Strg+C` bleibt der CLI vorbehalten. Das vermeidet, dass Nutzende beim
  Abbrechen eines laufenden Befehls versehentlich etwas kopieren.
- Mehrzeilige Auswahl wird zeilenweise kopiert und behält sinnvolle
  Zeilenumbrüche.

## Befunde

1. **Eine Auswahl ist bei laufender Ausgabe nicht verlässlich kopierbar.**
   Nach dem Markieren speichert die Oberfläche den Zustand des gesamten
   Terminalpuffers. Trifft anschließend irgendeine neue Ausgabe ein, wird die
   Markierung verworfen, auch wenn deren Text außerhalb der Auswahl liegt.
   Für Nutzende wirkt das so, als verschwinde die gerade markierte
   Fehlermeldung unvermittelt; ein anschließendes `Strg+Umschalt+C` oder
   Kontextmenü kann sie nicht mehr kopieren.

   Reproduzierbarer Ablauf:

   1. In einer laufenden CLI eine bereits ausgegebene Fehlermeldung mit der
      Maus markieren.
   2. Warten, bis die CLI eine weitere Status- oder Protokollzeile ausgibt.
   3. Feststellen, dass die Hervorhebung verschwunden ist, und die gewünschte
      Passage erneut markieren müssen.

   Das verletzt das Akzeptanzkriterium, Auswahl und Kopieren müssten auch
   **während** der Anzeige einer CLI-Ausgabe funktionieren. Die Auswahl sollte
   bestehen bleiben, solange ihre ausgewählten Zellen selbst noch vorhanden
   und unverändert sind; nur eine Änderung oder das Entfernen dieser Zellen
   sollte sie aufheben.

## Ergebnis

Die grundlegenden Bedienwege zum Markieren und Kopieren sind verständlich.
Der Verlust der Auswahl bei normaler, außerhalb der Auswahl eintreffender
Ausgabe verhindert jedoch gerade im häufigsten Anwendungsfall — dem Kopieren
einer laufend angezeigten Fehlermeldung — eine verlässliche Nutzung.

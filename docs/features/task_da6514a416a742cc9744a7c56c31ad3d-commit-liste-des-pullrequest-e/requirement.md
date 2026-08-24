# Übersetzte Anforderung

## Titel
Commit-Liste eines aus der Aufgabenansicht erstellten Pullrequests darf nur Commits des aktuellen Feature-Branches enthalten.

## Auslöser / Kontext
- Pullrequests werden über das Ribbon-Menü in der Aufgabenansicht erstellt.
- Features werden zunächst auf einen Staging-Branch gepusht.
- Erst nach längerer Nutzung der Prerelease-Versionen im lokalen Umfeld wird ein PR gegen `main` gemergt, um eine fertige Release-Version zu erzeugen.
- Dadurch sammeln sich im Staging-Branch mit der Zeit viele Commits an.

## Problem
Die Commit-Liste im Beschreibungstext des erstellten Pullrequests enthält zu viele Commits.
Darin enthalten sind auch Commits, die bereits im Zielbranch vorhanden sind und nicht zum aktuellen Feature-Branch gehören.

## Ziel
Beim Erstellen eines Pullrequests sollen in der Beschreibung nur die Commits aufgelistet werden, die tatsächlich zum aktuellen Feature-Branch gehören.

## Abgrenzung / Detailanforderung
- Commits, die bereits im Zielbranch (target branch) enthalten sind, dürfen im Pullrequest nicht gelistet werden.
- Die Commit-Liste muss auf die Differenz zwischen Feature-Branch und Zielbranch beschränkt werden.

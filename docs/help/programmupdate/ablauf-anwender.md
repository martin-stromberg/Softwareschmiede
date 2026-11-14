← [Zurück zur Übersicht](index.md)

# Programmupdate — Ablauf für Anwender

## Update-Verhalten einstellen

1. Öffnen Sie die **Einstellungen** über das Hauptmenü.
2. Wechseln Sie zur Registerkarte **Allgemein** und scrollen Sie zum Abschnitt **Updates**.
3. Wählen Sie in der Auswahlbox den gewünschten Modus:
   - **Aus** — keine Update-Prüfung (die Schaltfläche „⟳ Prüfen" ist deaktiviert)
   - **Nur prüfen** — prüfen und Update anbieten, aber nie automatisch installieren
   - **Bei Programmstart prüfen und ausführen** — beim Programmstart prüfen und ein gefundenes Update automatisch installieren
4. Wählen Sie in der Auswahlbox **„Update-Pfad"** den Update-Kanal:
   - **Produktivversionen** — nur stabile Versionen werden als Update angeboten (Standard)
   - **Vorabversionen** — nur Vorabversionen (Release-Kandidaten) werden als Update angeboten

   Unter der Auswahlbox zeigt die Zeile **„Kanal:"** den resultierenden Kanal an — die Trennung ist exklusiv, stabile und Vorabversionen werden niemals gemischt.
5. Klicken Sie auf **Speichern**. Die Meldung „Einstellungen gespeichert." bestätigt die Übernahme.

> **Hinweis:** Änderungen wirken erst nach dem Speichern. Wenn Sie die Einstellungen ändern, während ein Update-Angebot angezeigt wird oder ein Update läuft, wird das Angebot zurückgenommen bzw. der laufende Vorgang abgebrochen — es wird dann kein veraltetes Update installiert.

## Manuell auf Updates prüfen

1. Klicken Sie am Fuß der Navigations-Seitenleiste auf **⟳ Prüfen**.
2. Die Prüfung läuft kurz im Hintergrund. Anschließend gilt eines der folgenden Ergebnisse:
   - Ein Update ist verfügbar: Die Schaltfläche **⇧ Update** erscheint oberhalb von „⟳ Prüfen". Der Tooltip zeigt die gefundene Version.
   - Kein Update verfügbar: Unter der Schaltfläche erscheint der Hinweis „Kein Update verfügbar. Die installierte Version ist aktuell."
   - Die Prüfung war nicht möglich: Ein Hinweistext nennt den Grund (z. B. „GitHub-Release ist nicht prüfbar.").

> **Hinweis:** Steht der Update-Modus auf **Aus**, ist „⟳ Prüfen" deaktiviert. Auch während eine Prüfung oder Vorbereitung läuft, ist die Schaltfläche nicht verfügbar.

## Update installieren

1. Klicken Sie auf die Schaltfläche **⇧ Update**.
2. Die Anwendung prüft erneut, ob noch ein neueres Update verfügbar ist — es wird also immer die aktuellste passende Version installiert, nicht ein veraltetes Angebot.
3. Laufen noch CLI-Aufgaben, die nicht auf eine Eingabe warten, erscheint die Sicherheitsabfrage **„Update starten?"**. Sie listet die betroffenen Aufgaben und fragt, ob das Update trotzdem vorbereitet und die Anwendung beendet werden soll:
   - **Bestätigen** — der Update-Vorgang startet.
   - **Ablehnen** — der Vorgang endet, die Anwendung läuft weiter.
4. Der Fortschrittsdialog **„Update vorbereiten"** zeigt die Phasen **Download**, **Verifikation**, **Entpacken** und **Update-Vorbereitung** mit Fortschrittsbalken und Meldungstext an. In der Phase „Verifikation" prüft die Anwendung die Unversehrtheit des heruntergeladenen Pakets anhand seiner Prüfsumme.
5. Nach erfolgreicher Vorbereitung erscheint „Update wird gestartet. Die Anwendung wird beendet." Die Anwendung schließt sich, das Update-Skript tauscht die Programmdateien aus und startet die Anwendung anschließend neu.

## Update abbrechen

1. Klicken Sie im Fortschrittsdialog auf **Abbrechen**.
2. Der Dialog zeigt „Update-Vorbereitung wird abgebrochen." und anschließend „Update-Vorbereitung wurde abgebrochen." — in der Seitenleiste erscheint der entsprechende Hinweis.
3. Die Anwendung läuft unverändert weiter; es wurde nichts installiert.

> **Hinweis:** Sobald die Meldung „Update wird gestartet. Die Anwendung wird beendet." erscheint, ist ein Abbrechen nicht mehr möglich.

## Automatische Installation beim Programmstart

Ist der Modus **Bei Programmstart prüfen und ausführen** gespeichert, prüft die Anwendung einmalig kurz nach dem Programmstart:

- Ist kein Update verfügbar oder kann nicht geprüft werden, passiert nichts weiter — die Anwendung ist normal bedienbar.
- Ist ein Update verfügbar, beginnt der Update-Vorgang ohne weiteren Klick. Laufen riskante CLI-Aufgaben, wird zuvor die Sicherheitsabfrage „Update starten?" gezeigt — die Automatik umgeht diese Entscheidung nicht.
- Die automatische Installation findet nur einmalig beim Start statt. Ein später manuell ausgelöster Check installiert nicht automatisch, sondern bietet das Update über „⇧ Update" an.

## Vorabversionen erhalten

1. Wählen Sie in den Einstellungen unter **Updates** in der Auswahlbox **„Update-Pfad"** den Eintrag **„Vorabversionen"** und klicken Sie **Speichern**.
2. Führen Sie eine Prüfung über **⟳ Prüfen** aus (oder starten Sie die Anwendung neu).
3. Ist eine Vorabversion die neueste passende Version, wird sie wie ein reguläres Update angeboten bzw. — im Startmodus — installiert.

> **Hinweis:** Im Pfad „Vorabversionen" werden ausschließlich Release-Kandidaten berücksichtigt — eine stabile Version wird nicht angeboten, auch wenn sie semantisch neuer ist. Umgekehrt erhält der Pfad „Produktivversionen" niemals eine Vorabversion.

> **Hinweis:** Passt der gewählte Update-Pfad nicht zur installierten Version (z. B. eine Vorab-Installation mit dem Pfad „Produktivversionen"), erscheint beim Update-Angebot der Hinweis „Der gewählte Update-Pfad passt nicht zur installierten Version. Das Update wird trotzdem angeboten." — prüfen Sie dann, ob der Update-Pfad zur Installation passt.

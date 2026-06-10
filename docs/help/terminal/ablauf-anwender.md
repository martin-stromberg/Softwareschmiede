# CLI-Terminal — Ablauf für Anwender

## Voraussetzungen

- Das Terminal-Backend ist gestartet (siehe [Installation](installation.md)).
- Eine Aufgabe ist im Status „In Bearbeitung" und hat ein lokales Klonverzeichnis.
- Ein KI-Plugin (Claude CLI oder GitHub Copilot) ist ausgewählt.

## Schritt-für-Schritt-Anleitung

### 1. Aufgabe öffnen

Navigiere zu einer Aufgabe im Status **In Bearbeitung**.

### 2. Register „Ausführung" öffnen

Klicke auf den Tab **Ausführung** in der Aufgabendetailansicht.

### 3. KI-Plugin wählen

Stelle sicher, dass ein KI-Plugin ausgewählt ist (z.B. „Claude CLI"). Das Terminal erscheint automatisch im unteren Bereich.

### 4. Terminal bedienen

Im Bereich **🖥️ KI-Terminal** ist das CLI bereits gestartet. Du kannst direkt mit dem KI-Tool interagieren:
- Prompts eingeben und mit Enter bestätigen.
- Ausgabe wird live angezeigt.

> **Hinweis:** Das Terminal ist eine echte Shell-Session. Alle Befehle wirken sich direkt auf das lokale Klonverzeichnis aus.

## Ergebnis

Die KI-CLI-Session läuft direkt im Terminal. Geänderte Dateien erscheinen danach im Register **Projektverzeichnis**.

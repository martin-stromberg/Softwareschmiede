# Anforderungsuebersetzung

## Ziel

Die bisher getrennten GitHub Actions `test` und `validate` werden im Pull Request gegen `staging` zu einer gemeinsamen Action `validate & test` zusammengefuehrt.

## Fachliche Anforderungen

1. Die bisherige Action `validate` und die bisherige Action `test` werden durch eine gemeinsame GitHub Action mit dem Namen `validate & test` ersetzt.
2. Innerhalb der gemeinsamen Action werden zuerst der Lint-Format-Check, der Security-Dependency-Check und die statischen Codeanalysen ausgefuehrt.
3. Die Tests werden erst ausgefuehrt, nachdem die vorgenannten Pruefungen abgeschlossen sind.
4. Die Ausfuehrung erfolgt weiterhin im bestehenden Pull-Request-Kontext gegen den Branch `staging`.

## Ablauf

1. Lint-Format-Check ausfuehren.
2. Security-Dependency-Check ausfuehren.
3. Statische Codeanalysen ausfuehren.
4. Tests ausfuehren.

## Akzeptanzkriterien

- Im Pull Request gegen `staging` wird eine gemeinsame Action `validate & test` angezeigt.
- Die separaten Actions `test` und `validate` werden fuer diesen Ablauf nicht mehr unabhaengig voneinander ausgefuehrt.
- Lint-Format-Check, Security-Dependency-Check und statische Codeanalysen laufen vor den Tests.
- Die Tests starten erst nach Abschluss der vorgeschalteten Pruefungen.

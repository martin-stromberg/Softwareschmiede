# Sicherheitsrichtlinie

## Unterstützte Versionen

Softwareschmiede folgt [Semantic Versioning](https://semver.org/) und wird fortlaufend über
GitHub Releases veröffentlicht (siehe [`CHANGELOG.md`](CHANGELOG.md)). Sicherheitsupdates werden
ausschließlich für die jeweils **neueste veröffentlichte Version** bereitgestellt. Ältere Releases
werden nicht rückwirkend gepatcht.

| Version | Unterstützt |
|---------|-------------|
| Neueste Release (aktuell v1.x) | ✅ |
| Ältere Releases | ❌ |

## Eine Sicherheitslücke melden

Bitte melden Sie Sicherheitslücken **nicht** über ein öffentliches Issue. Nutzen Sie stattdessen
den privaten Meldeweg von GitHub:

1. Öffnen Sie den Tab **„Security"** des Repositories.
2. Wählen Sie **„Report a vulnerability"** (Private Security Advisory).
3. Beschreiben Sie die Schwachstelle so detailliert wie möglich: betroffene Version, Schritte zur
   Reproduktion, potenzielle Auswirkung.

Dieser Kanal ist privat zwischen Ihnen und dem Maintainer, sodass die Schwachstelle nicht
öffentlich sichtbar ist, bevor ein Fix verfügbar ist.

## Responsible Disclosure

- Bitte geben Sie uns die Gelegenheit, eine gemeldete Schwachstelle zu prüfen und zu beheben, bevor
  Details öffentlich gemacht werden.
- Wir bemühen uns um eine erste Rückmeldung innerhalb von **5 Werktagen**.
- Nach Bestätigung einer Schwachstelle wird ein Fix priorisiert und in einem der nächsten Releases
  veröffentlicht; die Reaktionszeit hängt vom Schweregrad ab.
- Nach Veröffentlichung eines Fixes wird die Schwachstelle im GitHub Security Advisory
  dokumentiert und, sofern zutreffend, im `CHANGELOG.md` als Fix vermerkt.
- Softwareschmiede führt CLI-Tools anderer Anbieter (z. B. KI-Agenten-CLIs wie GitHub Copilot CLI,
  Claude CLI oder Codex CLI) eingebettet aus. Stellt sich bei der Prüfung einer gemeldeten
  Schwachstelle heraus, dass die Ursache nicht in Softwareschmiede selbst liegt, sondern im
  Verhalten einer eingebetteten Dritt-CLI, kann die Rückmeldung entsprechend darauf hinweisen und
  auf den jeweiligen Anbieter dieser CLI verweisen. Der Meldeweg selbst (GitHub Private Security
  Advisories, siehe oben) bleibt davon unberührt.

## Umfang

Diese Richtlinie gilt für den Code in diesem Repository (`Softwareschmiede`,
`Softwareschmiede.App`, `Softwareschmiede.Plugin.Contracts` sowie die mitgelieferten
Plugin-Projekte unter `plugins/`). Schwachstellen in Drittanbieter-Abhängigkeiten werden zusätzlich
über den automatisierten CI-Vulnerability-Scan (`.github/workflows/security-scan.yml`) erkannt.

## Maintainer

Aktuell ist **[martin-stromberg](https://github.com/martin-stromberg)** alleiniger Maintainer
dieses Repositories und verantwortlich für die Bearbeitung von Sicherheitsmeldungen.

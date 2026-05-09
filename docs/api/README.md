# API-Dokumentation – Softwareschmiede

Technische Dokumentation der öffentlichen Schnittstellen und Plugin-APIs der Softwareschmiede.

---

## Dokumentierte Schnittstellen

| Dokument | Beschreibung |
|---|---|
| [plugin-interfaces.md](./plugin-interfaces.md) | Plugin-Entwickler-Dokumentation: `IGitPlugin` und `IKiPlugin` – Schnittstellenreferenz, Implementierungsanleitungen und Agentenpaket-Struktur |

---

## Überblick Plugin-System

Die Softwareschmiede verwendet ein Plugin-System mit zwei Schnittstellen:

- **`IGitPlugin`** – Kapselt alle Git-Operationen (Issues laden, Repository klonen, Branches verwalten, Pull Requests erstellen, …). Referenzimplementierung: `GitHubPlugin`.
- **`IKiPlugin`** – Kapselt die KI-Integration (Agenten verwalten, Entwicklung starten, Tests ausführen, …). Referenzimplementierung: `GitHubCopilotPlugin`.

Beide Plugins werden als **Singleton** über den DI-Container registriert.

Weitere Informationen: [plugin-interfaces.md](./plugin-interfaces.md)

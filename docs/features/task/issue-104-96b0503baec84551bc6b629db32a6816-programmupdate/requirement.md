# Anforderung

GitHub-Actions-Tests stabilisieren: Die Tests in den GitHub Actions sollen auf stabile Tests reduziert werden, also ohne flake-anfaellige WPF/FlaUI-/E2E-Tests. Der Rest der Tests wird lokal waehrend der Entwicklung ausgefuehrt.

Hinweis: In dieser Umgebung steht kein Subagent-Startwerkzeug fuer die Lifecycle-Delegation zur Verfuegung. Die Bearbeitung erfolgt deshalb direkt durch den Hauptagenten; die Lifecycle-Artefakte dokumentieren den Lauf.

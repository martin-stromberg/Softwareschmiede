using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Softwareschmiede.Migrations
{
    /// <summary>
    /// Defensive Datenmigration: Der Enum-Wert <c>AufgabeAusfuehrungsStatus.AutonomAufgabe</c> wurde entfernt
    /// (Modus regulär/autonom wird seither ausschließlich über <c>Aufgabe.AutonomKonfiguration != null</c> bzw.
    /// die Extension-Methode <c>Aufgabe.IstAutonom()</c> abgebildet, nicht mehr über die Ausführungsphase).
    /// Kein Schema-Wechsel nötig (die Spalte <c>AusfuehrungsStatus</c> ist ein unbeschränktes <c>TEXT</c>-Feld
    /// ohne Check-Constraint), aber verbliebene Zeilen mit dem alten String-Wert 'AutonomAufgabe' (z. B. aus
    /// lokalen Self-Hosting-Datenbanken, die während der Entwicklung dieses Feature-Branches bereits eine
    /// Autonome Aufgabe angelegt haben) würden beim Lesen über <c>HasConversion&lt;string&gt;()</c> eine
    /// <see cref="System.InvalidOperationException"/> auslösen, da der CLR-Enum-Wert nicht mehr existiert.
    /// Diese Migration normalisiert solche Zeilen auf 'Aktiv' (die Phase, die der Sentinel-Wert praktisch immer
    /// nur kurzzeitig überlebte, siehe Kernbefund in
    /// docs/features/task/issue-205-.../plan-autonomaufgabestatus.md, Abschnitt 0).
    /// </summary>
    public partial class NormalisiereAutonomAufgabeAusfuehrungsStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE Aufgaben SET AusfuehrungsStatus = 'Aktiv' WHERE AusfuehrungsStatus = 'AutonomAufgabe';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Bewusstes No-Op: Der Sentinel-Wert 'AutonomAufgabe' hatte schon vor diesem Refactoring keine
            // verlässliche fachliche Bedeutung mehr (er wurde von ProjektleiterAgentService.StarteAgentAsync
            // praktisch sofort nach dem Setzen wieder auf 'Aktiv' überschrieben, siehe Klassen-Dokumentation
            // oben). Ein join-basiertes Zurückschreiben anhand von AutonomAufgabeKonfigurationen.AufgabeId
            // würde daher auch tatsächlich aktive Autonome Aufgaben fälschlich wieder auf den Sentinel-Wert
            // setzen, der nach einem Downgrade auf den alten Enum-Stand ohnehin nur für ein sehr kurzes
            // Zeitfenster gültig war. Nach einem Rollback dieser Migration bleiben betroffene Zeilen auf
            // 'Aktiv' stehen, was für den alten (nicht mehr gültigen) Enum-Stand ebenfalls ein zulässiger Wert
            // war.
        }
    }
}

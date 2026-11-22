using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Softwareschmiede.Migrations;

/// <inheritdoc />
public partial class _20261122125707_AddWorkItemKind : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Auftragsart",
            table: "Aufgaben",
            type: "TEXT",
            nullable: false,
            defaultValue: "Unspecified");

        // Bestandsdaten klassifizieren (Issue #451): bekannte Urspruenge nachtragen; der
        // Rest gilt als manuell angelegt. Reihenfolge ist verbindlich — spezifischere
        // Herkunft zuerst, die allgemeineren Zuordnungen nur fuer noch Unbestimmte.
        // Bereits zugestellte Aufbereitungsaufgaben gelten wie im neuen Code als Anweisung;
        // abgebrochene/unklare Aufbereitungsverlaeufe bleiben konservativ IssuePreparation.
        migrationBuilder.Sql("""
            UPDATE Aufgaben SET Auftragsart = 'IssuePreparation' WHERE Zweck = 'IssueAufbereitung';
            UPDATE Aufgaben SET Auftragsart = 'Instruction' WHERE Zweck = 'IssueAufbereitung'
                AND EXISTS (
                    SELECT 1 FROM ScmIncomingEvents e
                    WHERE e.AufgabeId = Aufgaben.Id
                      AND e.Zweck = 'IssueAufbereitung'
                      AND e.Status IN ('Zugestellt', 'InBearbeitung', 'Verarbeitet'));
            UPDATE Aufgaben SET Auftragsart = 'Alert' WHERE Auftragsart = 'Unspecified'
                AND Id IN (SELECT AufgabeId FROM AlertReferenzen);
            UPDATE Aufgaben SET Auftragsart = 'PullRequest' WHERE Auftragsart = 'Unspecified'
                AND Id IN (SELECT AufgabeId FROM PullRequestReferenzen);
            UPDATE Aufgaben SET Auftragsart = 'Instruction' WHERE Auftragsart = 'Unspecified'
                AND Id IN (SELECT AufgabeId FROM ScmIncomingEvents WHERE AufgabeId IS NOT NULL);
            UPDATE Aufgaben SET Auftragsart = 'Manual' WHERE Auftragsart = 'Unspecified';
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Auftragsart",
            table: "Aufgaben");
    }
}

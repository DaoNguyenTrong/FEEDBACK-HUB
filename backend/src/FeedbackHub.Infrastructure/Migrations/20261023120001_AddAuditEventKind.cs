using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeedbackHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditEventKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "action",
                table: "audit_logs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<string>(
                name: "actor_kind",
                table: "audit_logs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "audit_logs",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Change");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_event_keyset",
                table: "audit_logs",
                columns: new[] { "organization_id", "timestamp", "id" },
                descending: new[] { false, true, true },
                filter: "kind = 'Event'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_audit_logs_event_keyset",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "actor_kind",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "audit_logs");

            migrationBuilder.AlterColumn<string>(
                name: "action",
                table: "audit_logs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);
        }
    }
}

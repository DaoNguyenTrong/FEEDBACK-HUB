using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeedbackHub.Infrastructure.Migrations
{
    /// <summary>
    /// M10: adds the email-change stage. The data step first clears every pending change of a password-less account
    /// (an old machine created it without the current-address step) and consumes its unused links, then marks the
    /// remaining pending changes as already past the current-address step. Idempotent.
    /// </summary>
    public partial class AddAccountPendingEmailStage : Migration
    {
        public const string DataSql = """
            UPDATE email_verification_tokens SET consumed_at = now()
            WHERE consumed_at IS NULL AND purpose = 'ChangeEmail'
              AND account_id IN (SELECT "Id" FROM accounts WHERE "PasswordHash" IS NULL AND "PendingEmail" IS NOT NULL);
            UPDATE accounts SET "PendingEmail" = NULL
            WHERE "PasswordHash" IS NULL AND "PendingEmail" IS NOT NULL;
            UPDATE accounts SET "PendingEmailStage" = 'AwaitingNewEmail'
            WHERE "PendingEmail" IS NOT NULL AND "PendingEmailStage" IS NULL;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PendingEmailStage",
                table: "accounts",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.Sql(DataSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PendingEmailStage",
                table: "accounts");
        }
    }
}

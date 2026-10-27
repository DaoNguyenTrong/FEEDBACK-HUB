using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using FeedbackHub.Infrastructure.Persistence;

#nullable disable

namespace FeedbackHub.Infrastructure.Migrations
{
    /// <summary>
    /// Data-only (M8): marks passwordless accounts linked only to Microsoft as unverified, because the old code read
    /// the wrong claim to certify their email. The NOTICE carries the count and ids only, never an email or username.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261023120000_ReverifyMicrosoftOnlyAccounts")]
    public partial class ReverifyMicrosoftOnlyAccounts : Migration
    {
        public const string UpSql = """
            DO $$
            DECLARE ids uuid[];
            BEGIN
              WITH changed AS (
                UPDATE accounts a SET "EmailConfirmed" = false
                WHERE a."EmailConfirmed" = true AND a."PasswordHash" IS NULL
                  AND EXISTS (SELECT 1 FROM external_logins l WHERE l.account_id = a."Id" AND l.provider = 'Microsoft')
                  AND NOT EXISTS (SELECT 1 FROM external_logins l WHERE l.account_id = a."Id" AND l.provider = 'Google')
                RETURNING a."Id")
              SELECT array_agg("Id") INTO ids FROM changed;
              RAISE NOTICE 'M8 marked % account(s) unverified: %', coalesce(array_length(ids, 1), 0), ids;
            END $$;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(UpSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The earlier verified state is not recoverable and was never trustworthy.
        }
    }
}

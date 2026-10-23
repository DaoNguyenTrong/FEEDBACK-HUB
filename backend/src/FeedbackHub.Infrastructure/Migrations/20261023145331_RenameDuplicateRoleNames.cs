using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeedbackHub.Infrastructure.Migrations
{
    /// <summary>
    /// M3: renames custom roles whose name collides with a system role or another role of the organization, then
    /// swaps the case-sensitive unique index for one on <c>lower(Name)</c>. The NOTICE carries ids only, never a name.
    /// </summary>
    public partial class RenameDuplicateRoleNames : Migration
    {
        // Every lower() pins ICU: plain lower() follows the database ctype (docs/persistence.md).
        public const string UpSql = """
            DO $$
            DECLARE
              r record;
              reserved text[] := ARRAY['owner', 'admin', 'member', 'chủ sở hữu'];
              n integer;
              candidate text;
              candidate_key text;
            BEGIN
              FOR r IN
                SELECT "Id", "OrganizationId", "Name", "CreatedAt"
                FROM roles WHERE "SystemRoleKind" IS NULL
                ORDER BY "OrganizationId", "CreatedAt", "Id"
              LOOP
                IF lower(normalize(btrim(r."Name"), NFC) COLLATE "und-x-icu") = ANY (reserved)
                   OR EXISTS (
                     SELECT 1 FROM roles x
                     WHERE x."OrganizationId" = r."OrganizationId" AND x."Id" <> r."Id"
                       AND lower(normalize(btrim(x."Name"), NFC) COLLATE "und-x-icu")
                         = lower(normalize(btrim(r."Name"), NFC) COLLATE "und-x-icu")
                       AND (x."SystemRoleKind" IS NOT NULL OR (x."CreatedAt", x."Id") < (r."CreatedAt", r."Id")))
                THEN
                  n := 2;
                  LOOP
                    candidate := left(normalize(btrim(r."Name"), NFC), 100 - length(' (' || n || ')')) || ' (' || n || ')';
                    candidate_key := lower(candidate COLLATE "und-x-icu");
                    EXIT WHEN candidate_key <> ALL (reserved)
                      AND NOT EXISTS (
                        SELECT 1 FROM roles x
                        WHERE x."OrganizationId" = r."OrganizationId"
                          AND lower(normalize(btrim(x."Name"), NFC) COLLATE "und-x-icu") = candidate_key);
                    n := n + 1;
                  END LOOP;
                  UPDATE roles SET "Name" = candidate WHERE "Id" = r."Id";
                  RAISE NOTICE 'M3 renamed role % in organization %', r."Id", r."OrganizationId";
                END IF;
              END LOOP;

              UPDATE roles SET "Name" = normalize(btrim("Name"), NFC) WHERE "Name" <> normalize(btrim("Name"), NFC);
            END $$;

            DROP INDEX "IX_roles_OrganizationId_Name";
            CREATE UNIQUE INDEX "IX_roles_OrganizationId_lower_Name"
              ON roles ("OrganizationId", lower("Name" COLLATE "und-x-icu"));
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(UpSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Renamed roles keep their new names.
            migrationBuilder.Sql("""DROP INDEX "IX_roles_OrganizationId_lower_Name";""");

            migrationBuilder.CreateIndex(
                name: "IX_roles_OrganizationId_Name",
                table: "roles",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);
        }
    }
}

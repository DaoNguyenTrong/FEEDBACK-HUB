using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using FeedbackHub.Infrastructure.Persistence;

#nullable disable

namespace FeedbackHub.Infrastructure.Migrations
{
    /// <summary>Data-only (M5): each Admin system role gets feedbackhub.admin; running it twice adds nothing.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261023145333_GrantFeedbackHubAdminToAdminRoles")]
    public partial class GrantFeedbackHubAdminToAdminRoles : Migration
    {
        public const string UpSql = """
            INSERT INTO role_permissions ("Id", "RoleId", "PermissionCode", "CreatedAt")
            SELECT gen_random_uuid(), r."Id", 'feedbackhub.admin', now()
            FROM roles r
            WHERE r."SystemRoleKind" = 'Admin'
              AND NOT EXISTS (
                SELECT 1 FROM role_permissions p WHERE p."RoleId" = r."Id" AND p."PermissionCode" = 'feedbackhub.admin');
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(UpSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The permission may have been granted by hand since; nothing is taken back.
        }
    }
}

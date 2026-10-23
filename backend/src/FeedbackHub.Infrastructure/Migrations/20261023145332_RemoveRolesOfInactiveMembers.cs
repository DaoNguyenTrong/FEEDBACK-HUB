using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using FeedbackHub.Infrastructure.Persistence;

#nullable disable

namespace FeedbackHub.Infrastructure.Migrations
{
    /// <summary>Data-only (M4): an inactive member holds no role, so the assignments left behind are removed.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261023145332_RemoveRolesOfInactiveMembers")]
    public partial class RemoveRolesOfInactiveMembers : Migration
    {
        public const string UpSql = """
            DELETE FROM organization_member_roles r
            USING organization_members m
            WHERE r."OrganizationMemberId" = m."Id" AND m."IsActive" = false;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(UpSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The removed assignments are not recoverable.
        }
    }
}

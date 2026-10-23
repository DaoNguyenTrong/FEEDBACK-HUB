using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeedbackHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationDeactivation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeactivatedAt",
                table: "organizations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeactivatedByAccountId",
                table: "organizations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DeactivatedBySystemAdministrator",
                table: "organizations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DeactivationReason",
                table: "organizations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeactivatedAt",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "DeactivatedByAccountId",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "DeactivatedBySystemAdministrator",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "DeactivationReason",
                table: "organizations");
        }
    }
}

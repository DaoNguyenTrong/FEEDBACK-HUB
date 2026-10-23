using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeedbackHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailChange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "email_verification_tokens",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "email_verification_tokens",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailChangeLockedUntil",
                table: "accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailChangeWindowStartedAt",
                table: "accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FailedEmailChangeCount",
                table: "accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PendingEmail",
                table: "accounts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Email",
                table: "email_verification_tokens");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "email_verification_tokens");

            migrationBuilder.DropColumn(
                name: "EmailChangeLockedUntil",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "EmailChangeWindowStartedAt",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "FailedEmailChangeCount",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "PendingEmail",
                table: "accounts");
        }
    }
}

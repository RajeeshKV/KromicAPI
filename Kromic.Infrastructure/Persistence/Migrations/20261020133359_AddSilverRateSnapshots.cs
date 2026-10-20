using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kromic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSilverRateSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "SilverNotificationsEnabled",
                table: "UserSettings",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<bool>(
                name: "GoldNotificationsEnabled",
                table: "UserSettings",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);

            migrationBuilder.CreateTable(
                name: "SilverRateSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<int>(type: "integer", nullable: true),
                    SilverRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SourceLastUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FetchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RegularEmailMessageId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LowestAlertMessageId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SilverRateSnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SilverRateSnapshots_FetchedAt",
                table: "SilverRateSnapshots",
                column: "FetchedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SilverRateSnapshots_SilverRate",
                table: "SilverRateSnapshots",
                column: "SilverRate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SilverRateSnapshots");

            migrationBuilder.AlterColumn<bool>(
                name: "SilverNotificationsEnabled",
                table: "UserSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "GoldNotificationsEnabled",
                table: "UserSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean");
        }
    }
}

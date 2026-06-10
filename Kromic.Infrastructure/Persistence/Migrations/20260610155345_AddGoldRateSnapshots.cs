using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kromic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGoldRateSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GoldRateSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<int>(type: "integer", nullable: true),
                    R22KT = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    R22KTShow = table.Column<bool>(type: "boolean", nullable: false),
                    R18KT = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    R24KT = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    SourceLastUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FetchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsLowestAtFetch = table.Column<bool>(type: "boolean", nullable: false),
                    RegularEmailMessageId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LowestAlertMessageId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoldRateSnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GoldRateSnapshots_FetchedAt",
                table: "GoldRateSnapshots",
                column: "FetchedAt");

            migrationBuilder.CreateIndex(
                name: "IX_GoldRateSnapshots_R22KT",
                table: "GoldRateSnapshots",
                column: "R22KT");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoldRateSnapshots");
        }
    }
}

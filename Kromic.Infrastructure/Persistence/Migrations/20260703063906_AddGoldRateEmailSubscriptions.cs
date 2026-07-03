using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kromic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGoldRateEmailSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GoldRateEmailSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChatId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UnsubscribeToken = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PendingRequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PendingExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SubscribedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UnsubscribedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoldRateEmailSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GoldRateEmailSubscriptions_ChatId",
                table: "GoldRateEmailSubscriptions",
                column: "ChatId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoldRateEmailSubscriptions_Email",
                table: "GoldRateEmailSubscriptions",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_GoldRateEmailSubscriptions_IsActive",
                table: "GoldRateEmailSubscriptions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_GoldRateEmailSubscriptions_UnsubscribeToken",
                table: "GoldRateEmailSubscriptions",
                column: "UnsubscribeToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoldRateEmailSubscriptions");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kromic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SyncUserSettingsWithTelegramUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent script to create UserSettings for all existing TelegramUsers
            // This ensures every Telegram user has a corresponding UserSettings record
            migrationBuilder.Sql(@"
                INSERT INTO ""UserSettings"" (""Id"", ""ChatId"", ""Language"", ""TelegramNotificationsEnabled"", ""EmailNotificationsEnabled"", ""IsPaused"", ""CreatedAt"", ""UpdatedAt"")
                SELECT 
                    gen_random_uuid() as ""Id"",
                    t.""ChatId"",
                    'en' as ""Language"",
                    true as ""TelegramNotificationsEnabled"",
                    false as ""EmailNotificationsEnabled"",
                    false as ""IsPaused"",
                    t.""CreatedAt"" as ""CreatedAt"",
                    t.""CreatedAt"" as ""UpdatedAt""
                FROM ""TelegramUsers"" t
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""UserSettings"" us 
                    WHERE us.""ChatId"" = t.""ChatId""
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}

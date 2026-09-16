using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kromic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSilverRateToSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "SilverRate",
                table: "GoldRateSnapshots",
                type: "numeric",
                nullable: true);

            // New localization strings for silver menu and stop button — upsert pattern matching existing migrations
            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    r RECORD;
                BEGIN
                    FOR r IN
                        SELECT 'commands.menu_gold_rates'          AS k, 'Gold Rates'              AS v, 'en' AS lang UNION ALL
                        SELECT 'commands.menu_silver_rates',              'Silver Rates',                  'en'        UNION ALL
                        SELECT 'commands.menu_silver_current_rate',       '🥈 Current Silver Rate',        'en'        UNION ALL
                        SELECT 'commands.menu_silver_reports',            '📅 Silver Reports',             'en'        UNION ALL
                        SELECT 'commands.menu_stop',                      '🛑 Stop Notifications',         'en'        UNION ALL
                        SELECT 'commands.menu_gold_rates',                'സ്വർണ്ണ നിരക്ക്',               'ml'        UNION ALL
                        SELECT 'commands.menu_silver_rates',              'വെള്ളി നിരക്ക്',                'ml'        UNION ALL
                        SELECT 'commands.menu_silver_current_rate',       '🥈 നിലവിലെ വെള്ളി നിരക്ക്',    'ml'        UNION ALL
                        SELECT 'commands.menu_silver_reports',            '📅 വെള്ളി റിപ്പോർട്ടുകൾ',      'ml'        UNION ALL
                        SELECT 'commands.menu_stop',                      '🛑 നോട്ടിഫിക്കേഷൻ നിർത്തുക',   'ml'
                    LOOP
                        UPDATE ""LocalizationResources""
                        SET ""Value"" = r.v, ""UpdatedAt"" = NOW()
                        WHERE ""Language"" = r.lang AND ""Key"" = r.k;

                        IF NOT FOUND THEN
                            INSERT INTO ""LocalizationResources"" (""Id"", ""Language"", ""Key"", ""Value"", ""CreatedAt"", ""UpdatedAt"")
                            VALUES (gen_random_uuid(), r.lang, r.k, r.v, NOW(), NOW());
                        END IF;
                    END LOOP;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SilverRate",
                table: "GoldRateSnapshots");
        }
    }
}

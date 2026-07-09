using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kromic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalizationResources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LocalizationResources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalizationResources", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LocalizationResources_Language_Key",
                table: "LocalizationResources",
                columns: new[] { "Language", "Key" },
                unique: true);

                // Seed English translations
            migrationBuilder.Sql(@"
                INSERT INTO ""LocalizationResources"" (""Id"", ""Language"", ""Key"", ""Value"", ""CreatedAt"", ""UpdatedAt"") VALUES
                (gen_random_uuid(), 'en', 'commands.welcome', 'Welcome to Kromic Gold Rate Bot! 📈\n\nI''ll send you daily gold rate updates. You can also check rates anytime using the commands below.', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.start', 'Welcome to Kromic Gold Rate Bot! 📈\n\nI''ll send you daily gold rate updates. You can also check rates anytime using the commands below.', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.help', '📚 Available Commands\n\n/currentrate - Get current gold rate\n/lastonemonthrates - View last 30 days rates\n/emailalerts - Subscribe to email alerts\n/feedback - Send feedback to admin\n/settings - Manage your notification settings\n/pause - Pause Telegram notifications\n/resume - Resume Telegram notifications\n/unsubscribeemail - Unsubscribe from email alerts\n/history - View historical rates by date\n/help - Show this help message', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.settings', '⚙️ Settings\n\n📱 Telegram Notifications: {TelegramStatus}\n📧 Email Notifications: {EmailStatus}\n🌐 Language: {Language}\n\nUse the buttons below to change your settings.', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.pause', '⏸️ Telegram notifications paused.\n\nYou won''t receive rate updates until you resume. Use /resume to enable notifications again.', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.resume', '▶️ Telegram notifications resumed.\n\nYou''ll now receive daily gold rate updates.', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.unsubscribeemail', '📧 Email subscription removed.\n\nYou won''t receive email alerts anymore. Use /emailalerts to subscribe again.', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.current_rate', 'Current Gold Rate', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.no_data', 'No data available', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.rate_unchanged', 'Gold rate unchanged for {Date}. Latest stored 22K rate: {Rate}.', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.welcome_new', 'Welcome! Here''s the Latest Gold Rate', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.youll_receive_updates', 'You''ll receive daily updates when the rate changes.', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.last_30_days', 'Last 30 Days Gold Rate Analysis', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.no_data_30_days', 'No data available for the past 30 days.', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.highest_lowest', 'Highest & Lowest Rates (30 Days)', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.weekly_summary', 'Weekly Gold Rate Summary', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.historical_lookup', 'Historical Rate Lookup', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.select_date', 'Please select a date to view the gold rate:', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.rate_not_found', 'No gold rate data found for {Date}.', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.menu_main', '🏠 Main Menu', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.menu_reports', '📅 Reports', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.menu_settings', '⚙️ Settings', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.menu_email', '📧 Email', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.menu_feedback', '💬 Feedback', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.menu_back', '⬅️ Back', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.menu_current_rate', '📈 Current Rate', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.menu_last_30_days', 'Last 30 Days', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.menu_weekly_summary', 'Weekly Summary', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.menu_highest_lowest', 'Highest & Lowest', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.menu_telegram_notifications', 'Telegram Notifications', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.menu_email_alerts', 'Email Alerts', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.menu_language', 'Language', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.menu_historical', 'Historical Lookup', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.lowest_gold_rate', 'Lowest Gold Rate Found', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.enabled', '✅ Enabled', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.disabled', '❌ Disabled', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.on', 'On', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.off', 'Off', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.english', 'English', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.malayalam', 'Malayalam', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.language_changed', 'Language changed to {Language}.', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.telegram_toggled', 'Telegram notifications {Status}.', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.email_toggled', 'Email notifications {Status}.', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.emailalerts', 'Please enter your email address to subscribe to email alerts:', NOW(), NOW()),
                (gen_random_uuid(), 'en', 'commands.feedback', 'Please send your feedback message. I''ll forward it to the admin.', NOW(), NOW());
            ");

            // Seed Malayalam translations
            migrationBuilder.Sql(@"
                INSERT INTO ""LocalizationResources"" (""Id"", ""Language"", ""Key"", ""Value"", ""CreatedAt"", ""UpdatedAt"") VALUES
                (gen_random_uuid(), 'ml', 'commands.start', 'ക്രോമിക് സ്വർണ്ണ നിരക്ക് ബോട്ടിലേക്ക് സ്വാഗതം! 📈\n\nഞാൻ നിങ്ങൾക്ക് ദൈനംദിന സ്വർണ്ണ നിരക്ക് അപ്ഡേറ്റുകൾ അയയ്ക്കും. താഴെയുള്ള കമാൻഡുകൾ ഉപയോഗിച്ച് നിങ്ങൾക്ക് എപ്പോഴും നിരക്ക് പരിശോധിക്കാവുന്നതാണ്.', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.help', '📚 ലഭ്യമായ കമാൻഡുകൾ\n\n/currentrate - നിലവിലെ സ്വർണ്ണ നിരക്ക് നേടുക\n/lastonemonthrates - കഴിഞ്ഞ 30 ദിവസത്തെ നിരക്കുകൾ കാണുക\n/emailalerts - ഇമെയിൽ അലേർട്ടുകളിൽ സബ്സ്ക്രൈബ് ചെയ്യുക\n/feedback - അഡ്മിനിലേക്ക് ഫീഡ്ബാക്ക് അയയ്ക്കുക\n/settings - നിങ്ങളുടെ നോട്ടിഫിക്കേഷൻ ക്രമീകരണങ്ങൾ നിയന്ത്രിക്കുക\n/pause - ടെലിഗ്രാം നോട്ടിഫിക്കേഷനുകൾ നിർത്തുക\n/resume - ടെലിഗ്രാം നോട്ടിഫിക്കേഷനുകൾ പുനരാരംഭിക്കുക\n/unsubscribeemail - ഇമെയിൽ അലേർട്ടുകളിൽ നിന്ന് വിരമിക്കുക\n/history - തീയതി അനുസരിച്ച് ചരിത്ര നിരക്കുകൾ കാണുക\n/help - ഈ സഹായി സന്ദേശം കാണിക്കുക', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.settings', '⚙️ ക്രമീകരണങ്ങൾ\n\n📱 ടെലിഗ്രാം നോട്ടിഫിക്കേഷനുകൾ: {TelegramStatus}\n📧 ഇമെയിൽ നോട്ടിഫിക്കേഷനുകൾ: {EmailStatus}\n🌐 ഭാഷ: {Language}\n\nനിങ്ങളുടെ ക്രമീകരണങ്ങൾ മാറ്റാൻ താഴെയുള്ള ബട്ടൺ ഉപയോഗിക്കുക.', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.pause', '⏸️ ടെലിഗ്രാം നോട്ടിഫിക്കേഷനുകൾ നിർത്തി.\n\nനിങ്ങൾ പുനരാരംഭിക്കുന്നത് വരെ നിരക്ക് അപ്ഡേറ്റുകൾ ലഭിക്കില്ല. നോട്ടിഫിക്കേഷനുകൾ പുനരാരംഭിക്കാൻ /resume ഉപയോഗിക്കുക.', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.resume', '▶️ ടെലിഗ്രാം നോട്ടിഫിക്കേഷനുകൾ പുനരാരംഭിച്ചു.\n\nനിങ്ങൾക്ക് ഇനി ദൈനംദിന സ്വർണ്ണ നിരക്ക് അപ്ഡേറ്റുകൾ ലഭിക്കും.', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.unsubscribeemail', '📧 ഇമെയിൽ സബ്സ്ക്രിപ്ഷൻ നീക്കം ചെയ്തു.\n\nനിങ്ങൾക്ക് ഇനി ഇമെയിൽ അലേർട്ടുകൾ ലഭിക്കില്ല. വീണ്ടും സബ്സ്ക്രൈബ് ചെയ്യാൻ /emailalerts ഉപയോഗിക്കുക.', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.current_rate', 'നിലവിലെ സ്വർണ്ണ നിരക്ക്', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.no_data', 'ഡാറ്റ ലഭ്യമല്ല', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.rate_unchanged', '{Date} നുള്ള സ്വർണ്ണ നിരക്ക് മാറ്റമില്ല. അവസാനം സൂക്ഷിച്ച 22K നിരക്ക്: {Rate}.', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.welcome_new', 'സ്വാഗതം! ഇതാ ഏറ്റവും പുതിയ സ്വർണ്ണ നിരക്ക്', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.youll_receive_updates', 'നിരക്ക് മാറുമ്പോൾ നിങ്ങൾക്ക് ദൈനംദിന അപ്ഡേറ്റുകൾ ലഭിക്കും.', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.last_30_days', 'കഴിഞ്ഞ 30 ദിവസത്തെ സ്വർണ്ണ നിരക്ക് വിശകലനം', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.no_data_30_days', 'കഴിഞ്ഞ 30 ദിവസങ്ങൾക്ക് ഡാറ്റ ലഭ്യമല്ല.', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.highest_lowest', 'ഉയർന്നതും താഴ്ന്നതും നിരക്കുകൾ (30 ദിവസം)', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.weekly_summary', 'ആഴ്ചതോറും സ്വർണ്ണ നിരക്ക് സംഗ്രഹം', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.historical_lookup', 'ചരിത്ര നിരക്ക് തിരയൽ', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.select_date', 'സ്വർണ്ണ നിരക്ക് കാണാൻ ഒരു തീയതി തിരഞ്ഞെടുക്കുക:', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.rate_not_found', '{Date} നുള്ള സ്വർണ്ണ നിരക്ക് ഡാറ്റ കണ്ടെത്തിയില്ല.', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.menu_main', '🏠 പ്രധാന മെനു', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.menu_reports', '📅 റിപ്പോർട്ടുകൾ', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.menu_settings', '⚙️ ക്രമീകരണങ്ങൾ', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.menu_email', '📧 ഇമെയിൽ', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.menu_feedback', '💬 ഫീഡ്ബാക്ക്', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.menu_back', '⬅️ പിന്നോട്ട്', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.menu_current_rate', '📈 നിലവിലെ നിരക്ക്', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.menu_last_30_days', 'കഴിഞ്ഞ 30 ദിവസം', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.menu_weekly_summary', 'ആഴ്ചതോറും സംഗ്രഹം', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.menu_highest_lowest', 'ഉയർന്നതും താഴ്ന്നതും', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.menu_telegram_notifications', 'ടെലിഗ്രാം നോട്ടിഫിക്കേഷനുകൾ', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.menu_email_alerts', 'ഇമെയിൽ അലേർട്ടുകൾ', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.menu_language', 'ഭാഷ', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.menu_historical', 'ചരിത്ര തിരയൽ', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.lowest_gold_rate', 'ഏറ്റവും കുറഞ്ഞ സ്വർണ്ണ നിരക്ക് കണ്ടെത്തി', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.enabled', '✅ പ്രവർത്തനത്തിലാണ്', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.disabled', '❌ പ്രവർത്തനരഹിതമാണ്', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.on', 'ഓൺ', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.off', 'ഓഫ്', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.english', 'ഇംഗ്ലീഷ്', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.malayalam', 'മലയാളം', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.language_changed', 'ഭാഷ {Language} ആയി മാറ്റി.', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.telegram_toggled', 'ടെലിഗ്രാം നോട്ടിഫിക്കേഷനുകൾ {Status}.', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.email_toggled', 'ഇമെയിൽ നോട്ടിഫിക്കേഷനുകൾ {Status}.', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.emailalerts', 'ഇമെയിൽ അലേർട്ടുകൾ സബ്സ്ക്രൈബ് ചെയ്യാൻ നിങ്ങളുടെ ഇമെയിൽ വിലാസം നൽകുക:', NOW(), NOW()),
                (gen_random_uuid(), 'ml', 'commands.feedback', 'നിങ്ങളുടെ ഫീഡ്ബാക്ക് സന്ദേശം അയയ്ക്കുക. ഞാൻ അഡ്മിനിലേക്ക് അയയ്ക്കും.', NOW(), NOW());
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LocalizationResources");
        }
    }
}

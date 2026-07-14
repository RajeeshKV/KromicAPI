using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kromic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MalayalamLangaugeUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Seed or Update Malayalam translations cleanly
            migrationBuilder.Sql(@"
                DO $$ 
                DECLARE 
                    r RECORD;
                BEGIN
                    -- Array of updates/inserts
                    FOR r IN 
                        SELECT 'commands.start' AS k, 'ക്രോമിക് സ്വർണ്ണ നിരക്ക് ബോട്ടിലേക്ക് സ്വാഗതം! 📈\n\nദിവസേനയുള്ള സ്വർണ്ണവില വിവരങ്ങൾ ഞാൻ നിങ്ങൾക്ക് അയച്ചുതരുന്നതായിരിക്കും. താഴെയുള്ള കമാൻഡുകൾ ഉപയോഗിച്ച് നിങ്ങൾക്ക് എപ്പോൾ വേണമെങ്കിലും നിരക്കുകൾ പരിശോധിക്കാം.' AS v UNION ALL
                        SELECT 'commands.help', '📚 ലഭ്യമായ കമാൻഡുകൾ\n\n/currentrate - നിലവിലെ സ്വർണ്ണ നിരക്ക്\n/lastonemonthrates - കഴിഞ്ഞ 30 ദിവസത്തെ നിരക്കുകൾ\n/emailalerts - ഇമെയിൽ അലേർട്ടുകൾ സബ്സ്ക്രൈബ് ചെയ്യാം\n/feedback - അഡ്മിന് ഫീഡ്ബാക്ക് അയയ്ക്കുക\n/settings - നോട്ടിഫിക്കേഷൻ ക്രമീകരണങ്ങൾ നിയന്ത്രിക്കുക\n/pause - ടെലിഗ്രാം നോട്ടിഫിക്കേഷനുകൾ താൽക്കാലികമായി നിർത്തുക\n/resume - ടെലിഗ്രാം നോട്ടിഫിക്കേഷനുകൾ പുനരാരംഭിക്കുക\n/unsubscribeemail - ഇമെയിൽ അലേർട്ടുകൾ ഒഴിവാക്കുക\n/history - തീയതി അടിസ്ഥാനമാക്കി പഴയ നിരക്കുകൾ തിരയുക\n/help - സഹായ സന്ദേശം കാണിക്കുക' UNION ALL
                        SELECT 'commands.settings', '⚙️ ക്രമീകരണങ്ങൾ\n\n📱 ടെലിഗ്രാം നോട്ടിഫിക്കേഷനുകൾ: {TelegramStatus}\n📧 ഇമെയിൽ നോട്ടിഫിക്കേഷനുകൾ: {EmailStatus}\n🌐 ഭാഷ: {Language}\n\nക്രമീകരണങ്ങളിൽ മാറ്റം വരുത്താൻ താഴെയുള്ള ബട്ടണുകൾ ഉപയോഗിക്കുക.' UNION ALL
                        SELECT 'commands.pause', '⏸️ ടെലിഗ്രാം നോട്ടിഫിക്കേഷനുകൾ താൽക്കാലികമായി നിർത്തിവെച്ചിരിക്കുന്നു.\n\nനിങ്ങൾ ഇത് പുനരാരംഭിക്കുന്നത് വരെ അപ്ഡേറ്റുകൾ ലഭിക്കില്ല. നോട്ടിഫിക്കേഷനുകൾ വീണ്ടും ലഭിക്കാൻ /resume ഉപയോഗിക്കുക.' UNION ALL
                        SELECT 'commands.resume', '▶️ ടെലിഗ്രാം നോട്ടിഫിക്കേഷനുകൾ പുനരാരംഭിച്ചു.\n\nഇനി മുതൽ നിങ്ങൾക്ക് ദിവസേനയുള്ള സ്വർണ്ണ നിരക്ക് അപ്ഡേറ്റുകൾ ലഭിക്കുന്നതാണ്.' UNION ALL
                        SELECT 'commands.unsubscribeemail', '📧 ഇമെയിൽ സബ്സ്ക്രിപ്ഷൻ ഒഴിവാക്കിയിരിക്കുന്നു.\n\nനിങ്ങൾക്ക് ഇനി ഇമെയിൽ അലേർട്ടുകൾ ലഭിക്കില്ല. വീണ്ടും സബ്സ്ക്രൈബ് ചെയ്യാൻ /emailalerts ഉപയോഗിക്കുക.' UNION ALL
                        SELECT 'commands.current_rate', 'നിലവിലെ സ്വർണ്ണ നിരക്ക്' UNION ALL
                        SELECT 'commands.no_data', 'വിവരങ്ങൾ ലഭ്യമല്ല' UNION ALL
                        SELECT 'commands.rate_unchanged', '{Date}-ലെ സ്വർണ്ണ നിരക്കിൽ മാറ്റമില്ല. അവസാനമായി രേഖപ്പെടുത്തിയ 22K നിരക്ക്: {Rate}.' UNION ALL
                        SELECT 'commands.welcome_new', 'സ്വാഗതം! ഇതാ ഏറ്റവും പുതിയ സ്വർണ്ണ നിരക്ക്:' UNION ALL
                        SELECT 'commands.youll_receive_updates', 'വിലയിൽ മാറ്റമുണ്ടാകുന്നതിനനുസരിച്ച് ദിവസേനയുള്ള അപ്ഡേറ്റുകൾ നിങ്ങൾക്ക് ലഭിക്കും.' UNION ALL
                        SELECT 'commands.last_30_days', 'കഴിഞ്ഞ 30 ദിവസത്തെ സ്വർണ്ണ നിരക്ക് വിശകലനം' UNION ALL
                        SELECT 'commands.no_data_30_days', 'കഴിഞ്ഞ 30 ദിവസത്തെ വിവരങ്ങൾ ലഭ്യമല്ല.' UNION ALL
                        SELECT 'commands.highest_lowest', 'ഏറ്റവും ഉയർന്നതും കുറഞ്ഞതുമായ നിരക്കുകൾ (30 ദിവസം)' UNION ALL
                        SELECT 'commands.weekly_summary', 'ആഴ്ചയിലെ സ്വർണ്ണ നിരക്ക് സംഗ്രഹം' UNION ALL
                        SELECT 'commands.historical_lookup', 'പഴയ നിരക്കുകൾ തിരയുക' UNION ALL
                        SELECT 'commands.select_date', 'സ്വർണ്ണ നിരക്ക് അറിയാൻ ഒരു തീയതി തിരഞ്ഞെടുക്കുക:' UNION ALL
                        SELECT 'commands.rate_not_found', '{Date}-ലെ സ്വർണ്ണ നിരക്ക് വിവരങ്ങൾ കണ്ടെത്താനായില്ല.' UNION ALL
                        SELECT 'commands.menu_main', '🏠 പ്രധാന മെനു' UNION ALL
                        SELECT 'commands.menu_reports', '📅 റിപ്പോർട്ടുകൾ' UNION ALL
                        SELECT 'commands.menu_settings', '⚙️ ക്രമീകരണങ്ങൾ' UNION ALL
                        SELECT 'commands.menu_email', '📧 ഇമെയിൽ' UNION ALL
                        SELECT 'commands.menu_feedback', '💬 ഫീഡ്ബാക്ക്' UNION ALL
                        SELECT 'commands.menu_back', '⬅️ പിന്നോട്ട്' UNION ALL
                        SELECT 'commands.menu_current_rate', '📈 നിലവിലെ നിരക്ക്' UNION ALL
                        SELECT 'commands.menu_last_30_days', 'കഴിഞ്ഞ 30 ദിവസം' UNION ALL
                        SELECT 'commands.menu_weekly_summary', 'ആഴ്ചയിലെ സംഗ്രഹം' UNION ALL
                        SELECT 'commands.menu_highest_lowest', 'ഉയർന്നതും കുറഞ്ഞതും' UNION ALL
                        SELECT 'commands.menu_telegram_notifications', 'ടെലിഗ്രാം നോട്ടിഫിക്കേഷൻ' UNION ALL
                        SELECT 'commands.menu_email_alerts', 'ഇമെയിൽ അലേർട്ടുകൾ' UNION ALL
                        SELECT 'commands.menu_language', 'ഭാഷ (Language)' UNION ALL
                        SELECT 'commands.menu_historical', 'പഴയ നിരക്ക് തിരയൽ' UNION ALL
                        SELECT 'commands.lowest_gold_rate', 'ഏറ്റവും കുറഞ്ഞ സ്വർണ്ണ നിരക്ക് കണ്ടെത്തി' UNION ALL
                        SELECT 'commands.enabled', '✅ ആക്ടീവാണ്' UNION ALL
                        SELECT 'commands.disabled', '❌ ഡിസാക്ടീവാണ്' UNION ALL
                        SELECT 'commands.on', 'ഓൺ' UNION ALL
                        SELECT 'commands.off', 'ഓഫ്' UNION ALL
                        SELECT 'commands.english', 'ഇംഗ്ലീഷ്' UNION ALL
                        SELECT 'commands.malayalam', 'മലയാളം' UNION ALL
                        SELECT 'commands.language_changed', 'ഭാഷ {Language}-ലേക്ക് മാറ്റിയിരിക്കുന്നു.' UNION ALL
                        SELECT 'commands.telegram_toggled', 'ടെലിഗ്രാം നോട്ടിഫിക്കേഷനുകൾ {Status}.' UNION ALL
                        SELECT 'commands.email_toggled', 'ഇമെയിൽ നോട്ടിഫിക്കേഷനുകൾ {Status}.' UNION ALL
                        SELECT 'commands.emailalerts', 'ഇമെയിൽ അലേർട്ടുകൾ സബ്സ്ക്രൈബ് ചെയ്യാൻ നിങ്ങളുടെ ഇമെയിൽ വിലാസം നൽകുക:' UNION ALL
                        SELECT 'commands.feedback', 'നിങ്ങളുടെ ഫീഡ്ബാക്ക് സന്ദേശം ടൈപ്പ് ചെയ്ത് അയക്കൂ. ഞാൻ ഇത് അഡ്മിന് കൈമാറുന്നതാണ്.'
                    LOOP
                        UPDATE ""LocalizationResources"" 
                        SET ""Value"" = r.v, ""UpdatedAt"" = NOW() 
                        WHERE ""Language"" = 'ml' AND ""Key"" = r.k;
                        
                        IF NOT FOUND THEN
                            INSERT INTO ""LocalizationResources"" (""Id"", ""Language"", ""Key"", ""Value"", ""CreatedAt"", ""UpdatedAt"")
                            VALUES (gen_random_uuid(), 'ml', r.k, r.v, NOW(), NOW());
                        END IF;
                    LOOP;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}

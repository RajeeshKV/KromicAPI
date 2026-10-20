using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kromic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillSilverAndDropColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                INSERT INTO ""SilverRateSnapshots"" (""Id"", ""SourceId"", ""SilverRate"", ""SourceLastUpdatedAt"", ""FetchedAt"")
                SELECT gen_random_uuid(), ""SourceId"", ""SilverRate"", ""SourceLastUpdatedAt"", ""FetchedAt""
                FROM ""GoldRateSnapshots""
                WHERE ""SilverRate"" IS NOT NULL;
            ");
            migrationBuilder.DropColumn(name: "SilverRate", table: "GoldRateSnapshots");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(name: "SilverRate", table: "GoldRateSnapshots", type: "numeric(18,2)", nullable: true);
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AICustomerSupport.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSectionTitleToDocumentChunk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SectionTitle",
                table: "DocumentChunks",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SectionTitle",
                table: "DocumentChunks");
        }
    }
}

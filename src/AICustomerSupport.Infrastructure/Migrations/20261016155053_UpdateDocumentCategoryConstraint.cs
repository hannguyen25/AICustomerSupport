using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AICustomerSupport.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDocumentCategoryConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "Documents",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Document_Category",
                table: "Documents",
                sql: "[Category] IN ('ADMIN_NOTICE', 'ACADEMIC_COURSE')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Document_Category",
                table: "Documents");

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "Documents",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);
        }
    }
}

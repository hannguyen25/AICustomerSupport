using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AICustomerSupport.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryConstraintToDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Document_Category",
                table: "Documents");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Document_Category",
                table: "Documents",
                sql: "[Category] IN ('Thông báo', 'Gíao trình')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Document_Category",
                table: "Documents");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Document_Category",
                table: "Documents",
                sql: "[Category] IN ('ADMIN_NOTICE', 'ACADEMIC_COURSE')");
        }
    }
}

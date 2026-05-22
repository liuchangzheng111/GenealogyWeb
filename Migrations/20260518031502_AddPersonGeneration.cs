using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenealogyWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonGeneration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Generation",
                table: "Persons",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Generation",
                table: "Persons");
        }
    }
}

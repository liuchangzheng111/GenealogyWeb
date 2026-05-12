using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenealogyWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddParentChildGenealogyParentIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 须先建复合索引：外键 FK_ParentChildren_Genealogies_GenealogyId 依赖 GenealogyId 上有索引，
            // 若先 DROP 单列索引，MySQL 会报错（无法删除被外键使用的索引）。
            migrationBuilder.CreateIndex(
                name: "IX_ParentChildren_GenealogyId_ParentId",
                table: "ParentChildren",
                columns: new[] { "GenealogyId", "ParentId" });

            migrationBuilder.DropIndex(
                name: "IX_ParentChildren_GenealogyId",
                table: "ParentChildren");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 回滚时先恢复单列索引以支撑外键，再删复合索引。
            migrationBuilder.CreateIndex(
                name: "IX_ParentChildren_GenealogyId",
                table: "ParentChildren",
                column: "GenealogyId");

            migrationBuilder.DropIndex(
                name: "IX_ParentChildren_GenealogyId_ParentId",
                table: "ParentChildren");
        }
    }
}

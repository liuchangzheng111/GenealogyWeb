using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenealogyWeb.Migrations
{
    /// <inheritdoc />
    public partial class FkCheckAndTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Person_BirthBeforeDeath",
                table: "Persons",
                sql: "`BirthYear` IS NULL OR `DeathYear` IS NULL OR `BirthYear` <= `DeathYear`");

            migrationBuilder.CreateIndex(
                name: "IX_ParentChildren_GenealogyId",
                table: "ParentChildren",
                column: "GenealogyId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ParentChildren_NotSelf",
                table: "ParentChildren",
                sql: "`ParentId` <> `ChildId`");

            migrationBuilder.CreateIndex(
                name: "IX_Marriages_GenealogyId",
                table: "Marriages",
                column: "GenealogyId");

            migrationBuilder.CreateIndex(
                name: "IX_Marriages_SpouseAId",
                table: "Marriages",
                column: "SpouseAId");

            migrationBuilder.CreateIndex(
                name: "IX_Marriages_SpouseBId",
                table: "Marriages",
                column: "SpouseBId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Marriage_DifferentSpouses",
                table: "Marriages",
                sql: "`SpouseAId` <> `SpouseBId`");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Marriage_DivorceAfterWedding",
                table: "Marriages",
                sql: "`DivorcedAtYear` IS NULL OR `MarriedAtYear` IS NULL OR `DivorcedAtYear` >= `MarriedAtYear`");

            migrationBuilder.CreateIndex(
                name: "IX_GenealogyUsers_InvitedByUserId",
                table: "GenealogyUsers",
                column: "InvitedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GenealogyUsers_UserId",
                table: "GenealogyUsers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Genealogies_CreatedByUserId",
                table: "Genealogies",
                column: "CreatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Genealogies_Users_CreatedByUserId",
                table: "Genealogies",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GenealogyUsers_Genealogies_GenealogyId",
                table: "GenealogyUsers",
                column: "GenealogyId",
                principalTable: "Genealogies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GenealogyUsers_Users_InvitedByUserId",
                table: "GenealogyUsers",
                column: "InvitedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_GenealogyUsers_Users_UserId",
                table: "GenealogyUsers",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Marriages_Genealogies_GenealogyId",
                table: "Marriages",
                column: "GenealogyId",
                principalTable: "Genealogies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Marriages_Persons_SpouseAId",
                table: "Marriages",
                column: "SpouseAId",
                principalTable: "Persons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Marriages_Persons_SpouseBId",
                table: "Marriages",
                column: "SpouseBId",
                principalTable: "Persons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ParentChildren_Genealogies_GenealogyId",
                table: "ParentChildren",
                column: "GenealogyId",
                principalTable: "Genealogies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ParentChildren_Persons_ChildId",
                table: "ParentChildren",
                column: "ChildId",
                principalTable: "Persons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ParentChildren_Persons_ParentId",
                table: "ParentChildren",
                column: "ParentId",
                principalTable: "Persons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Persons_Genealogies_GenealogyId",
                table: "Persons",
                column: "GenealogyId",
                principalTable: "Genealogies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("DROP TRIGGER IF EXISTS tr_ParentChildren_ins_birth;");
            migrationBuilder.Sql("""
CREATE TRIGGER tr_ParentChildren_ins_birth
BEFORE INSERT ON ParentChildren
FOR EACH ROW
BEGIN
  DECLARE pyr INT;
  DECLARE cyr INT;
  DECLARE pg CHAR(36);
  DECLARE cg CHAR(36);
  SELECT BirthYear, GenealogyId INTO pyr, pg FROM Persons WHERE Id = NEW.ParentId LIMIT 1;
  SELECT BirthYear, GenealogyId INTO cyr, cg FROM Persons WHERE Id = NEW.ChildId LIMIT 1;
  IF pg <> NEW.GenealogyId OR cg <> NEW.GenealogyId THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'CHK: ParentChild 须与父母、子女所属族谱一致。';
  END IF;
  IF pyr IS NOT NULL AND cyr IS NOT NULL AND NOT (pyr < cyr) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'CHK: 父母出生年须早于子女出生年（若均已填写）。';
  END IF;
END
""");

            migrationBuilder.Sql("DROP TRIGGER IF EXISTS tr_ParentChildren_upd_birth;");
            migrationBuilder.Sql("""
CREATE TRIGGER tr_ParentChildren_upd_birth
BEFORE UPDATE ON ParentChildren
FOR EACH ROW
BEGIN
  DECLARE pyr INT;
  DECLARE cyr INT;
  DECLARE pg CHAR(36);
  DECLARE cg CHAR(36);
  SELECT BirthYear, GenealogyId INTO pyr, pg FROM Persons WHERE Id = NEW.ParentId LIMIT 1;
  SELECT BirthYear, GenealogyId INTO cyr, cg FROM Persons WHERE Id = NEW.ChildId LIMIT 1;
  IF pg <> NEW.GenealogyId OR cg <> NEW.GenealogyId THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'CHK: ParentChild 须与父母、子女所属族谱一致。';
  END IF;
  IF pyr IS NOT NULL AND cyr IS NOT NULL AND NOT (pyr < cyr) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'CHK: 父母出生年须早于子女出生年（若均已填写）。';
  END IF;
END
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS tr_ParentChildren_upd_birth;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS tr_ParentChildren_ins_birth;");

            migrationBuilder.DropForeignKey(
                name: "FK_Genealogies_Users_CreatedByUserId",
                table: "Genealogies");

            migrationBuilder.DropForeignKey(
                name: "FK_GenealogyUsers_Genealogies_GenealogyId",
                table: "GenealogyUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_GenealogyUsers_Users_InvitedByUserId",
                table: "GenealogyUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_GenealogyUsers_Users_UserId",
                table: "GenealogyUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_Marriages_Genealogies_GenealogyId",
                table: "Marriages");

            migrationBuilder.DropForeignKey(
                name: "FK_Marriages_Persons_SpouseAId",
                table: "Marriages");

            migrationBuilder.DropForeignKey(
                name: "FK_Marriages_Persons_SpouseBId",
                table: "Marriages");

            migrationBuilder.DropForeignKey(
                name: "FK_ParentChildren_Genealogies_GenealogyId",
                table: "ParentChildren");

            migrationBuilder.DropForeignKey(
                name: "FK_ParentChildren_Persons_ChildId",
                table: "ParentChildren");

            migrationBuilder.DropForeignKey(
                name: "FK_ParentChildren_Persons_ParentId",
                table: "ParentChildren");

            migrationBuilder.DropForeignKey(
                name: "FK_Persons_Genealogies_GenealogyId",
                table: "Persons");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Person_BirthBeforeDeath",
                table: "Persons");

            migrationBuilder.DropIndex(
                name: "IX_ParentChildren_GenealogyId",
                table: "ParentChildren");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ParentChildren_NotSelf",
                table: "ParentChildren");

            migrationBuilder.DropIndex(
                name: "IX_Marriages_GenealogyId",
                table: "Marriages");

            migrationBuilder.DropIndex(
                name: "IX_Marriages_SpouseAId",
                table: "Marriages");

            migrationBuilder.DropIndex(
                name: "IX_Marriages_SpouseBId",
                table: "Marriages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Marriage_DifferentSpouses",
                table: "Marriages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Marriage_DivorceAfterWedding",
                table: "Marriages");

            migrationBuilder.DropIndex(
                name: "IX_GenealogyUsers_InvitedByUserId",
                table: "GenealogyUsers");

            migrationBuilder.DropIndex(
                name: "IX_GenealogyUsers_UserId",
                table: "GenealogyUsers");

            migrationBuilder.DropIndex(
                name: "IX_Genealogies_CreatedByUserId",
                table: "Genealogies");
        }
    }
}

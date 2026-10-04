using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <summary>
    /// Renames the "game" schema to "assignment" in place. Hand-written as
    /// renames only (EF scaffolds a drop + create, which would lose data).
    /// </summary>
    public partial class RenameGamesToAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Questions_GameInstances_GameInstanceId",
                table: "Questions");

            migrationBuilder.DropForeignKey(
                name: "FK_StudentAttempts_GameInstances_GameInstanceId",
                table: "StudentAttempts");

            migrationBuilder.RenameTable(
                name: "GameInstances",
                newName: "Assignments");

            migrationBuilder.Sql("EXEC sp_rename N'dbo.PK_GameInstances', N'PK_Assignments', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'dbo.FK_GameInstances_AspNetUsers_CreatedByTeacherId', N'FK_Assignments_AspNetUsers_CreatedByTeacherId', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'dbo.FK_GameInstances_Categories_CategoryId', N'FK_Assignments_Categories_CategoryId', N'OBJECT';");

            migrationBuilder.RenameIndex(
                name: "IX_GameInstances_CategoryId",
                table: "Assignments",
                newName: "IX_Assignments_CategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_GameInstances_CreatedByTeacherId",
                table: "Assignments",
                newName: "IX_Assignments_CreatedByTeacherId");

            migrationBuilder.RenameIndex(
                name: "IX_GameInstances_State",
                table: "Assignments",
                newName: "IX_Assignments_State");

            migrationBuilder.RenameColumn(
                name: "GameType",
                table: "Assignments",
                newName: "AssignmentType");

            migrationBuilder.Sql("UPDATE Assignments SET AssignmentType = N'Matching' WHERE AssignmentType = N'WordMatching';");

            migrationBuilder.RenameColumn(
                name: "GameInstanceId",
                table: "StudentAttempts",
                newName: "AssignmentId");

            migrationBuilder.RenameIndex(
                name: "IX_StudentAttempts_GameInstanceId_StudentId",
                table: "StudentAttempts",
                newName: "IX_StudentAttempts_AssignmentId_StudentId");

            migrationBuilder.RenameColumn(
                name: "GameInstanceId",
                table: "Questions",
                newName: "AssignmentId");

            migrationBuilder.RenameIndex(
                name: "IX_Questions_GameInstanceId_Order",
                table: "Questions",
                newName: "IX_Questions_AssignmentId_Order");

            migrationBuilder.AddForeignKey(
                name: "FK_Questions_Assignments_AssignmentId",
                table: "Questions",
                column: "AssignmentId",
                principalTable: "Assignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StudentAttempts_Assignments_AssignmentId",
                table: "StudentAttempts",
                column: "AssignmentId",
                principalTable: "Assignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Questions_Assignments_AssignmentId",
                table: "Questions");

            migrationBuilder.DropForeignKey(
                name: "FK_StudentAttempts_Assignments_AssignmentId",
                table: "StudentAttempts");

            migrationBuilder.RenameIndex(
                name: "IX_Questions_AssignmentId_Order",
                table: "Questions",
                newName: "IX_Questions_GameInstanceId_Order");

            migrationBuilder.RenameColumn(
                name: "AssignmentId",
                table: "Questions",
                newName: "GameInstanceId");

            migrationBuilder.RenameIndex(
                name: "IX_StudentAttempts_AssignmentId_StudentId",
                table: "StudentAttempts",
                newName: "IX_StudentAttempts_GameInstanceId_StudentId");

            migrationBuilder.RenameColumn(
                name: "AssignmentId",
                table: "StudentAttempts",
                newName: "GameInstanceId");

            migrationBuilder.Sql("UPDATE Assignments SET AssignmentType = N'WordMatching' WHERE AssignmentType = N'Matching';");

            migrationBuilder.RenameColumn(
                name: "AssignmentType",
                table: "Assignments",
                newName: "GameType");

            migrationBuilder.RenameIndex(
                name: "IX_Assignments_State",
                table: "Assignments",
                newName: "IX_GameInstances_State");

            migrationBuilder.RenameIndex(
                name: "IX_Assignments_CreatedByTeacherId",
                table: "Assignments",
                newName: "IX_GameInstances_CreatedByTeacherId");

            migrationBuilder.RenameIndex(
                name: "IX_Assignments_CategoryId",
                table: "Assignments",
                newName: "IX_GameInstances_CategoryId");

            migrationBuilder.Sql("EXEC sp_rename N'dbo.FK_Assignments_Categories_CategoryId', N'FK_GameInstances_Categories_CategoryId', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'dbo.FK_Assignments_AspNetUsers_CreatedByTeacherId', N'FK_GameInstances_AspNetUsers_CreatedByTeacherId', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'dbo.PK_Assignments', N'PK_GameInstances', N'OBJECT';");

            migrationBuilder.RenameTable(
                name: "Assignments",
                newName: "GameInstances");

            migrationBuilder.AddForeignKey(
                name: "FK_Questions_GameInstances_GameInstanceId",
                table: "Questions",
                column: "GameInstanceId",
                principalTable: "GameInstances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StudentAttempts_GameInstances_GameInstanceId",
                table: "StudentAttempts",
                column: "GameInstanceId",
                principalTable: "GameInstances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

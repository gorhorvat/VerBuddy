using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class OwnershipScoping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Added nullable first (no default) so an existing dev DB with rows
            // in AspNetUsers/Rewards can be backfilled before the NOT NULL / FK
            // constraint on Rewards.CreatedById is enforced. On a fresh DB this
            // migration runs before DbSeeder inserts any rows (including
            // teacher.anna), so every backfill below is a no-op — harmless.
            migrationBuilder.AddColumn<string>(
                name: "CreatedById",
                table: "Rewards",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedByAdminId",
                table: "AspNetUsers",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            // Backfill existing students (role 'User') to teacher.anna's ownership.
            migrationBuilder.Sql(@"
                UPDATE u SET CreatedByAdminId = (SELECT TOP 1 Id FROM AspNetUsers WHERE UserName = 'teacher.anna')
                FROM AspNetUsers u
                JOIN AspNetUserRoles ur ON ur.UserId = u.Id
                JOIN AspNetRoles r ON r.Id = ur.RoleId
                WHERE r.Name = 'User' AND u.CreatedByAdminId IS NULL;");

            // Backfill existing rewards to teacher.anna's ownership.
            migrationBuilder.Sql(@"
                UPDATE Rewards SET CreatedById = (SELECT TOP 1 Id FROM AspNetUsers WHERE UserName = 'teacher.anna')
                WHERE CreatedById IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedById",
                table: "Rewards",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rewards_CreatedById",
                table: "Rewards",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_CreatedByAdminId",
                table: "AspNetUsers",
                column: "CreatedByAdminId");

            migrationBuilder.AddForeignKey(
                name: "FK_Rewards_AspNetUsers_CreatedById",
                table: "Rewards",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Rewards_AspNetUsers_CreatedById",
                table: "Rewards");

            migrationBuilder.DropIndex(
                name: "IX_Rewards_CreatedById",
                table: "Rewards");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_CreatedByAdminId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Rewards");

            migrationBuilder.DropColumn(
                name: "CreatedByAdminId",
                table: "AspNetUsers");
        }
    }
}

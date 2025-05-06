using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Computernewb.CollabVMAuthServer.Database.Migrations
{
    /// <inheritdoc />
    public partial class UseUserIdAsForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.DropForeignKey(
                name: "sessions_ibfk_1",
                table: "sessions");
            
            migrationBuilder.DropForeignKey(
                name: "bots_ibfk_1",
                table: "bots");

            migrationBuilder.DropIndex(
                name: "username",
                table: "sessions");
            
            migrationBuilder.DropIndex(
                name: "owner",
                table: "bots"
            );

            migrationBuilder.RenameColumn(
                table: "bots",
                name: "owner",
                newName: "owner_tmp");

            migrationBuilder.AddColumn<uint>(
                name: "user",
                table: "sessions",
                type: "int(10) unsigned",
                nullable: false);

            migrationBuilder.AddColumn<uint>(
                name: "owner",
                table: "bots",
                type: "int(10) unsigned",
                nullable: false);

            migrationBuilder.CreateIndex(
                name: "user",
                table: "sessions",
                column: "user");
            
            migrationBuilder.CreateIndex(
                name: "owner",
                table: "bots",
                column: "owner");

            // Migrate data
            migrationBuilder.Sql(
                """
                UPDATE sessions INNER JOIN users ON sessions.username=users.username
                SET sessions.user = users.id;
                """
            );

            migrationBuilder.Sql(
                """
                UPDATE bots INNER JOIN users ON bots.owner_tmp=users.username
                SET bots.owner = users.id;
                """
            );

            migrationBuilder.AddForeignKey(
                name: "owner",
                table: "bots",
                column: "owner",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "user",
                table: "sessions",
                column: "user",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.DropColumn(
                name: "username",
                table: "sessions");
            
            migrationBuilder.DropColumn(
                name: "owner_tmp",
                table: "bots");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "owner",
                table: "bots");

            migrationBuilder.DropForeignKey(
                name: "user",
                table: "sessions");

            migrationBuilder.DropIndex(
                name: "user",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "user",
                table: "sessions");

            migrationBuilder.RenameIndex(
                name: "username1",
                table: "users",
                newName: "username2");

            migrationBuilder.AddColumn<string>(
                name: "username",
                table: "sessions",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "owner",
                table: "bots",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                collation: "utf8mb4_unicode_ci",
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_users_username",
                table: "users",
                column: "username");

            migrationBuilder.CreateIndex(
                name: "username1",
                table: "sessions",
                column: "username");

            migrationBuilder.AddForeignKey(
                name: "bots_ibfk_1",
                table: "bots",
                column: "owner",
                principalTable: "users",
                principalColumn: "username",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "sessions_ibfk_1",
                table: "sessions",
                column: "username",
                principalTable: "users",
                principalColumn: "username",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

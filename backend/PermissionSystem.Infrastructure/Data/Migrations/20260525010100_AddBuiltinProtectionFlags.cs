using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionSystem.Infrastructure.Data.Migrations;

public partial class AddBuiltinProtectionFlags : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsBuiltin",
            table: "Users",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "IsBuiltin",
            table: "Roles",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.Sql(
            "UPDATE Users SET IsBuiltin = 1 WHERE NormalizedUserName = 'ADMIN';");

        migrationBuilder.Sql(
            "UPDATE Roles SET IsBuiltin = 1 WHERE Code = 'SuperAdmin';");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IsBuiltin",
            table: "Users");

        migrationBuilder.DropColumn(
            name: "IsBuiltin",
            table: "Roles");
    }
}

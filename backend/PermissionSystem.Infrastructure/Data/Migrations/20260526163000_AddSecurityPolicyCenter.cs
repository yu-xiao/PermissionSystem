using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionSystem.Infrastructure.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260526163000_AddSecurityPolicyCenter")]
    public partial class AddSecurityPolicyCenter : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SecurityPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PasswordMinLength = table.Column<int>(type: "int", nullable: false, defaultValue: 8),
                    RequireDigit = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RequireUppercase = table.Column<bool>(type: "bit", nullable: false),
                    RequireLowercase = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RequireSpecialChar = table.Column<bool>(type: "bit", nullable: false),
                    PasswordExpireDays = table.Column<int>(type: "int", nullable: false),
                    LoginFailureLockThreshold = table.Column<int>(type: "int", nullable: false, defaultValue: 5),
                    LoginFailureLockMinutes = table.Column<int>(type: "int", nullable: false, defaultValue: 15),
                    EnableMfa = table.Column<bool>(type: "bit", nullable: false),
                    EnableSensitiveOperationVerify = table.Column<bool>(type: "bit", nullable: false),
                    EnableIpWhitelist = table.Column<bool>(type: "bit", nullable: false),
                    EnableIpBlacklist = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoginFailureRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    FailureCount = table.Column<int>(type: "int", nullable: false),
                    LockedUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastFailureAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginFailureRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SensitiveOperationVerifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    VerifyCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SensitiveOperationVerifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IpAccessRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IpPattern = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IpAccessRules", x => x.Id);
                });

            migrationBuilder.CreateIndex(name: "IX_SecurityPolicies_IsDeleted", table: "SecurityPolicies", column: "IsDeleted");
            migrationBuilder.CreateIndex(name: "IX_SecurityPolicies_TenantId", table: "SecurityPolicies", column: "TenantId", unique: true);

            migrationBuilder.CreateIndex(name: "IX_LoginFailureRecords_IsDeleted", table: "LoginFailureRecords", column: "IsDeleted");
            migrationBuilder.CreateIndex(name: "IX_LoginFailureRecords_TenantId", table: "LoginFailureRecords", column: "TenantId");
            migrationBuilder.CreateIndex(name: "IX_LoginFailureRecords_TenantId_LockedUntil", table: "LoginFailureRecords", columns: new[] { "TenantId", "LockedUntil" });
            migrationBuilder.CreateIndex(name: "IX_LoginFailureRecords_TenantId_UserName_IpAddress", table: "LoginFailureRecords", columns: new[] { "TenantId", "UserName", "IpAddress" }, unique: true, filter: "[IpAddress] IS NOT NULL");

            migrationBuilder.CreateIndex(name: "IX_SensitiveOperationVerifications_IsDeleted", table: "SensitiveOperationVerifications", column: "IsDeleted");
            migrationBuilder.CreateIndex(name: "IX_SensitiveOperationVerifications_TenantId", table: "SensitiveOperationVerifications", column: "TenantId");
            migrationBuilder.CreateIndex(name: "IX_SensitiveOperationVerifications_TenantId_UserId_OperationCode_ExpiresAt", table: "SensitiveOperationVerifications", columns: new[] { "TenantId", "UserId", "OperationCode", "ExpiresAt" });
            migrationBuilder.CreateIndex(name: "IX_SensitiveOperationVerifications_TenantId_VerifyCode", table: "SensitiveOperationVerifications", columns: new[] { "TenantId", "VerifyCode" });

            migrationBuilder.CreateIndex(name: "IX_IpAccessRules_IsDeleted", table: "IpAccessRules", column: "IsDeleted");
            migrationBuilder.CreateIndex(name: "IX_IpAccessRules_TenantId", table: "IpAccessRules", column: "TenantId");
            migrationBuilder.CreateIndex(name: "IX_IpAccessRules_TenantId_RuleType_IpPattern", table: "IpAccessRules", columns: new[] { "TenantId", "RuleType", "IpPattern" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_IpAccessRules_TenantId_RuleType_IsEnabled", table: "IpAccessRules", columns: new[] { "TenantId", "RuleType", "IsEnabled" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "IpAccessRules");
            migrationBuilder.DropTable(name: "SensitiveOperationVerifications");
            migrationBuilder.DropTable(name: "LoginFailureRecords");
            migrationBuilder.DropTable(name: "SecurityPolicies");
        }
    }
}

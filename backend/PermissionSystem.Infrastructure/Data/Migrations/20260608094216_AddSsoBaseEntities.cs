using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSsoBaseEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sso_login_log",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProviderName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProviderType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ExternalUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ExternalUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LocalUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LocalUserName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    LoginResult = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    TraceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sso_login_log", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sso_provider",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProviderName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProviderType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Authority = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MetadataAddress = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ClientId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ClientSecretEncrypted = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Scopes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CallbackPath = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ResponseType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UsePkce = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    GetClaimsFromUserInfoEndpoint = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    UserIdClaim = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    UserNameClaim = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    EmailClaim = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PhoneClaim = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DisplayNameClaim = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RoleClaim = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DepartmentClaim = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    AutoCreateUser = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    AutoBindUser = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    DefaultRoleIds = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AllowLocalLoginFallback = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    LogoutRedirectUri = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sso_provider", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sso_department_mapping",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalDepartment = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LocalDepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sso_department_mapping", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sso_department_mapping_Departments_LocalDepartmentId",
                        column: x => x.LocalDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sso_department_mapping_sso_provider_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "sso_provider",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sso_role_mapping",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalRole = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LocalRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sso_role_mapping", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sso_role_mapping_Roles_LocalRoleId",
                        column: x => x.LocalRoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sso_role_mapping_sso_provider_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "sso_provider",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sso_user_binding",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExternalUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ExternalUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ExternalEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ExternalPhone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LocalUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastLoginAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClaimsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sso_user_binding", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sso_user_binding_Users_LocalUserId",
                        column: x => x.LocalUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sso_user_binding_sso_provider_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "sso_provider",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sso_department_mapping_IsDeleted",
                table: "sso_department_mapping",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_sso_department_mapping_LocalDepartmentId",
                table: "sso_department_mapping",
                column: "LocalDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_sso_department_mapping_ProviderId",
                table: "sso_department_mapping",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_sso_department_mapping_TenantId",
                table: "sso_department_mapping",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_sso_department_mapping_TenantId_ProviderId",
                table: "sso_department_mapping",
                columns: new[] { "TenantId", "ProviderId" });

            migrationBuilder.CreateIndex(
                name: "IX_sso_department_mapping_TenantId_ProviderId_ExternalDepartment_LocalDepartmentId",
                table: "sso_department_mapping",
                columns: new[] { "TenantId", "ProviderId", "ExternalDepartment", "LocalDepartmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sso_login_log_ExternalUserId",
                table: "sso_login_log",
                column: "ExternalUserId");

            migrationBuilder.CreateIndex(
                name: "IX_sso_login_log_IsDeleted",
                table: "sso_login_log",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_sso_login_log_LocalUserId",
                table: "sso_login_log",
                column: "LocalUserId");

            migrationBuilder.CreateIndex(
                name: "IX_sso_login_log_TenantId",
                table: "sso_login_log",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_sso_login_log_TenantId_CreatedAt",
                table: "sso_login_log",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_sso_login_log_TenantId_LoginResult_CreatedAt",
                table: "sso_login_log",
                columns: new[] { "TenantId", "LoginResult", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_sso_login_log_TenantId_ProviderCode_CreatedAt",
                table: "sso_login_log",
                columns: new[] { "TenantId", "ProviderCode", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_sso_login_log_TraceId",
                table: "sso_login_log",
                column: "TraceId");

            migrationBuilder.CreateIndex(
                name: "IX_sso_provider_IsDeleted",
                table: "sso_provider",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_sso_provider_TenantId",
                table: "sso_provider",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_sso_provider_TenantId_ProviderCode",
                table: "sso_provider",
                columns: new[] { "TenantId", "ProviderCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sso_provider_TenantId_ProviderType_Enabled",
                table: "sso_provider",
                columns: new[] { "TenantId", "ProviderType", "Enabled" });

            migrationBuilder.CreateIndex(
                name: "IX_sso_role_mapping_IsDeleted",
                table: "sso_role_mapping",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_sso_role_mapping_LocalRoleId",
                table: "sso_role_mapping",
                column: "LocalRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_sso_role_mapping_ProviderId",
                table: "sso_role_mapping",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_sso_role_mapping_TenantId",
                table: "sso_role_mapping",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_sso_role_mapping_TenantId_ProviderId",
                table: "sso_role_mapping",
                columns: new[] { "TenantId", "ProviderId" });

            migrationBuilder.CreateIndex(
                name: "IX_sso_role_mapping_TenantId_ProviderId_ExternalRole_LocalRoleId",
                table: "sso_role_mapping",
                columns: new[] { "TenantId", "ProviderId", "ExternalRole", "LocalRoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sso_user_binding_IsDeleted",
                table: "sso_user_binding",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_sso_user_binding_LocalUserId",
                table: "sso_user_binding",
                column: "LocalUserId");

            migrationBuilder.CreateIndex(
                name: "IX_sso_user_binding_ProviderId",
                table: "sso_user_binding",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_sso_user_binding_TenantId",
                table: "sso_user_binding",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_sso_user_binding_TenantId_ExternalEmail",
                table: "sso_user_binding",
                columns: new[] { "TenantId", "ExternalEmail" });

            migrationBuilder.CreateIndex(
                name: "IX_sso_user_binding_TenantId_ExternalPhone",
                table: "sso_user_binding",
                columns: new[] { "TenantId", "ExternalPhone" });

            migrationBuilder.CreateIndex(
                name: "IX_sso_user_binding_TenantId_ProviderCode",
                table: "sso_user_binding",
                columns: new[] { "TenantId", "ProviderCode" });

            migrationBuilder.CreateIndex(
                name: "IX_sso_user_binding_TenantId_ProviderId_ExternalUserId",
                table: "sso_user_binding",
                columns: new[] { "TenantId", "ProviderId", "ExternalUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sso_user_binding_TenantId_ProviderId_LocalUserId",
                table: "sso_user_binding",
                columns: new[] { "TenantId", "ProviderId", "LocalUserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sso_department_mapping");

            migrationBuilder.DropTable(
                name: "sso_login_log");

            migrationBuilder.DropTable(
                name: "sso_role_mapping");

            migrationBuilder.DropTable(
                name: "sso_user_binding");

            migrationBuilder.DropTable(
                name: "sso_provider");
        }
    }
}

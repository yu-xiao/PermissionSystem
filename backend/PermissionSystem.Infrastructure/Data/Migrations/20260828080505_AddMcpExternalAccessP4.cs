using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMcpExternalAccessP4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mcp_client_binding",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApiClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OAuthClientId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mcp_client_binding", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mcp_client_binding_ApiClients_ApiClientId",
                        column: x => x.ApiClientId,
                        principalTable: "ApiClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "mcp_dataset_definition",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DatasetCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DatasetName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DataClassification = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    HandlerCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MaxRows = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mcp_dataset_definition", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "mcp_invocation_log",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientBindingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CallerType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OAuthClientId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ToolName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DatasetCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TraceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    InputDigest = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RowCount = table.Column<int>(type: "int", nullable: false),
                    IsTruncated = table.Column<bool>(type: "bit", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DurationMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ErrorSummary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mcp_invocation_log", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mcp_invocation_log_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mcp_invocation_log_mcp_client_binding_ClientBindingId",
                        column: x => x.ClientBindingId,
                        principalTable: "mcp_client_binding",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "mcp_client_dataset_grant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientBindingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DatasetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllowedFieldsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mcp_client_dataset_grant", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mcp_client_dataset_grant_mcp_client_binding_ClientBindingId",
                        column: x => x.ClientBindingId,
                        principalTable: "mcp_client_binding",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mcp_client_dataset_grant_mcp_dataset_definition_DatasetId",
                        column: x => x.DatasetId,
                        principalTable: "mcp_dataset_definition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "mcp_dataset_field",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DatasetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FieldCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DataType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DataClassification = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsFilterable = table.Column<bool>(type: "bit", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mcp_dataset_field", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mcp_dataset_field_mcp_dataset_definition_DatasetId",
                        column: x => x.DatasetId,
                        principalTable: "mcp_dataset_definition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mcp_client_binding_ApiClientId",
                table: "mcp_client_binding",
                column: "ApiClientId");

            migrationBuilder.CreateIndex(
                name: "IX_mcp_client_binding_IsDeleted",
                table: "mcp_client_binding",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_mcp_client_binding_OAuthClientId",
                table: "mcp_client_binding",
                column: "OAuthClientId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_mcp_client_binding_TenantId",
                table: "mcp_client_binding",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_mcp_client_binding_TenantId_ApiClientId",
                table: "mcp_client_binding",
                columns: new[] { "TenantId", "ApiClientId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_mcp_client_binding_TenantId_IsEnabled",
                table: "mcp_client_binding",
                columns: new[] { "TenantId", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_mcp_client_dataset_grant_ClientBindingId",
                table: "mcp_client_dataset_grant",
                column: "ClientBindingId");

            migrationBuilder.CreateIndex(
                name: "IX_mcp_client_dataset_grant_DatasetId",
                table: "mcp_client_dataset_grant",
                column: "DatasetId");

            migrationBuilder.CreateIndex(
                name: "IX_mcp_client_dataset_grant_IsDeleted",
                table: "mcp_client_dataset_grant",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_mcp_client_dataset_grant_TenantId",
                table: "mcp_client_dataset_grant",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_mcp_client_dataset_grant_TenantId_ClientBindingId_DatasetId",
                table: "mcp_client_dataset_grant",
                columns: new[] { "TenantId", "ClientBindingId", "DatasetId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_mcp_client_dataset_grant_TenantId_ClientBindingId_IsEnabled",
                table: "mcp_client_dataset_grant",
                columns: new[] { "TenantId", "ClientBindingId", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_mcp_dataset_definition_IsDeleted",
                table: "mcp_dataset_definition",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_mcp_dataset_definition_TenantId",
                table: "mcp_dataset_definition",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_mcp_dataset_definition_TenantId_DatasetCode_Version",
                table: "mcp_dataset_definition",
                columns: new[] { "TenantId", "DatasetCode", "Version" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_mcp_dataset_definition_TenantId_IsEnabled_DatasetCode",
                table: "mcp_dataset_definition",
                columns: new[] { "TenantId", "IsEnabled", "DatasetCode" });

            migrationBuilder.CreateIndex(
                name: "IX_mcp_dataset_field_DatasetId",
                table: "mcp_dataset_field",
                column: "DatasetId");

            migrationBuilder.CreateIndex(
                name: "IX_mcp_dataset_field_IsDeleted",
                table: "mcp_dataset_field",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_mcp_dataset_field_TenantId",
                table: "mcp_dataset_field",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_mcp_dataset_field_TenantId_DatasetId_FieldCode",
                table: "mcp_dataset_field",
                columns: new[] { "TenantId", "DatasetId", "FieldCode" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_mcp_dataset_field_TenantId_DatasetId_IsDefault",
                table: "mcp_dataset_field",
                columns: new[] { "TenantId", "DatasetId", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_mcp_invocation_log_ActorUserId",
                table: "mcp_invocation_log",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_mcp_invocation_log_ClientBindingId",
                table: "mcp_invocation_log",
                column: "ClientBindingId");

            migrationBuilder.CreateIndex(
                name: "IX_mcp_invocation_log_IsDeleted",
                table: "mcp_invocation_log",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_mcp_invocation_log_TenantId",
                table: "mcp_invocation_log",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_mcp_invocation_log_TenantId_ClientBindingId_CreatedAt",
                table: "mcp_invocation_log",
                columns: new[] { "TenantId", "ClientBindingId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_mcp_invocation_log_TenantId_DatasetCode_CreatedAt",
                table: "mcp_invocation_log",
                columns: new[] { "TenantId", "DatasetCode", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_mcp_invocation_log_TenantId_Status_CreatedAt",
                table: "mcp_invocation_log",
                columns: new[] { "TenantId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_mcp_invocation_log_TenantId_TraceId",
                table: "mcp_invocation_log",
                columns: new[] { "TenantId", "TraceId" });

            migrationBuilder.Sql(
                """
                INSERT INTO [mcp_dataset_definition]
                    ([Id], [DatasetCode], [DatasetName], [Version], [Description], [DataClassification], [HandlerCode], [MaxRows], [IsEnabled], [TenantId], [CreatedAt], [IsDeleted])
                SELECT NEWID(), seed.[DatasetCode], seed.[DatasetName], N'1.0', seed.[Description], seed.[DataClassification], seed.[DatasetCode], seed.[MaxRows], 1, tenant.[Id], SYSUTCDATETIME(), 0
                FROM [Tenants] AS tenant
                CROSS JOIN (VALUES
                    (N'platform-capabilities', N'Platform capabilities', N'Non-sensitive metadata describing enabled PermissionSystem capability families.', N'Public', 20),
                    (N'department-directory', N'Department directory', N'Tenant-scoped department directory without internal identifiers or audit fields.', N'Internal', 100)
                ) AS seed ([DatasetCode], [DatasetName], [Description], [DataClassification], [MaxRows])
                WHERE tenant.[IsDeleted] = 0
                  AND NOT EXISTS (
                      SELECT 1 FROM [mcp_dataset_definition] existing
                      WHERE existing.[TenantId] = tenant.[Id]
                        AND existing.[DatasetCode] = seed.[DatasetCode]
                        AND existing.[Version] = N'1.0'
                        AND existing.[IsDeleted] = 0);

                INSERT INTO [mcp_dataset_field]
                    ([Id], [DatasetId], [FieldCode], [DisplayName], [DataType], [DataClassification], [IsFilterable], [IsDefault], [TenantId], [CreatedAt], [IsDeleted])
                SELECT NEWID(), dataset.[Id], seed.[FieldCode], seed.[DisplayName], seed.[DataType], seed.[DataClassification], seed.[IsFilterable], 1, dataset.[TenantId], SYSUTCDATETIME(), 0
                FROM [mcp_dataset_definition] AS dataset
                INNER JOIN (VALUES
                    (N'platform-capabilities', N'code', N'Code', N'string', N'Public', 1),
                    (N'platform-capabilities', N'name', N'Name', N'string', N'Public', 1),
                    (N'platform-capabilities', N'status', N'Status', N'string', N'Public', 1),
                    (N'department-directory', N'code', N'Department code', N'string', N'Internal', 1),
                    (N'department-directory', N'name', N'Department name', N'string', N'Internal', 1),
                    (N'department-directory', N'parentCode', N'Parent department code', N'string', N'Internal', 0),
                    (N'department-directory', N'isEnabled', N'Enabled', N'boolean', N'Internal', 1)
                ) AS seed ([DatasetCode], [FieldCode], [DisplayName], [DataType], [DataClassification], [IsFilterable])
                    ON seed.[DatasetCode] = dataset.[DatasetCode]
                WHERE dataset.[Version] = N'1.0'
                  AND dataset.[IsDeleted] = 0
                  AND NOT EXISTS (
                      SELECT 1 FROM [mcp_dataset_field] existing
                      WHERE existing.[TenantId] = dataset.[TenantId]
                        AND existing.[DatasetId] = dataset.[Id]
                        AND existing.[FieldCode] = seed.[FieldCode]
                        AND existing.[IsDeleted] = 0);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mcp_client_dataset_grant");

            migrationBuilder.DropTable(
                name: "mcp_dataset_field");

            migrationBuilder.DropTable(
                name: "mcp_invocation_log");

            migrationBuilder.DropTable(
                name: "mcp_dataset_definition");

            migrationBuilder.DropTable(
                name: "mcp_client_binding");
        }
    }
}

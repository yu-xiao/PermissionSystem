using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMcpDatasetSchemaGovernanceP4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PublicationStatus",
                table: "mcp_dataset_definition",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PublishedAt",
                table: "mcp_dataset_definition",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SchemaHash",
                table: "mcp_dataset_definition",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ApprovedSchemaHash",
                table: "mcp_client_dataset_grant",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE dataset
                SET dataset.[SchemaHash] = CASE dataset.[DatasetCode]
                        WHEN N'platform-capabilities' THEN N'B9DCA44A8861B0327C5185CCE989DFC5B8234C57270BA1077AAEF73EA0FEE6C2'
                        WHEN N'department-directory' THEN N'716DF9CB29D081721687E2420E981DB950CE82E7F8E262B2331FF7E489A4EDD0'
                    END,
                    dataset.[PublicationStatus] = N'Published',
                    dataset.[PublishedAt] = COALESCE(dataset.[PublishedAt], SYSUTCDATETIME())
                FROM [mcp_dataset_definition] AS dataset
                WHERE dataset.[Version] = N'1.0'
                  AND dataset.[IsDeleted] = 0
                  AND dataset.[DatasetCode] IN (N'platform-capabilities', N'department-directory');

                UPDATE grantRow
                SET grantRow.[ApprovedSchemaHash] = dataset.[SchemaHash]
                FROM [mcp_client_dataset_grant] AS grantRow
                INNER JOIN [mcp_dataset_definition] AS dataset
                    ON dataset.[Id] = grantRow.[DatasetId]
                   AND dataset.[TenantId] = grantRow.[TenantId]
                WHERE grantRow.[IsDeleted] = 0
                  AND dataset.[IsDeleted] = 0
                  AND LEN(dataset.[SchemaHash]) = 64;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_mcp_dataset_definition_TenantId_PublicationStatus_IsEnabled_DatasetCode",
                table: "mcp_dataset_definition",
                columns: new[] { "TenantId", "PublicationStatus", "IsEnabled", "DatasetCode" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_mcp_dataset_definition_TenantId_PublicationStatus_IsEnabled_DatasetCode",
                table: "mcp_dataset_definition");

            migrationBuilder.DropColumn(
                name: "PublicationStatus",
                table: "mcp_dataset_definition");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "mcp_dataset_definition");

            migrationBuilder.DropColumn(
                name: "SchemaHash",
                table: "mcp_dataset_definition");

            migrationBuilder.DropColumn(
                name: "ApprovedSchemaHash",
                table: "mcp_client_dataset_grant");
        }
    }
}

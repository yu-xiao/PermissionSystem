using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionSystem.Infrastructure.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260526170000_AddOpenIntegrationCenter")]
    public partial class AddOpenIntegrationCenter : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiClients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ClientName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AllowedScopes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AllowedIpList = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RateLimitPerMinute = table.Column<int>(type: "int", nullable: false, defaultValue: 60),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiClients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApiClientSecrets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SecretHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiClientSecrets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WebhookSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Secret = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    RetryCount = table.Column<int>(type: "int", nullable: false, defaultValue: 3),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WebhookDeliveryLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ResponseStatusCode = table.Column<int>(type: "int", nullable: true),
                    ResponseBody = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookDeliveryLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExternalApiCallLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Path = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Method = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    StatusCode = table.Column<int>(type: "int", nullable: false),
                    ElapsedMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalApiCallLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(name: "IX_ApiClients_IsDeleted", table: "ApiClients", column: "IsDeleted");
            migrationBuilder.CreateIndex(name: "IX_ApiClients_TenantId", table: "ApiClients", column: "TenantId");
            migrationBuilder.CreateIndex(name: "IX_ApiClients_TenantId_ClientCode", table: "ApiClients", columns: new[] { "TenantId", "ClientCode" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_ApiClients_TenantId_IsEnabled", table: "ApiClients", columns: new[] { "TenantId", "IsEnabled" });

            migrationBuilder.CreateIndex(name: "IX_ApiClientSecrets_IsDeleted", table: "ApiClientSecrets", column: "IsDeleted");
            migrationBuilder.CreateIndex(name: "IX_ApiClientSecrets_TenantId", table: "ApiClientSecrets", column: "TenantId");
            migrationBuilder.CreateIndex(name: "IX_ApiClientSecrets_TenantId_ClientId", table: "ApiClientSecrets", columns: new[] { "TenantId", "ClientId" });
            migrationBuilder.CreateIndex(name: "IX_ApiClientSecrets_TenantId_SecretHash", table: "ApiClientSecrets", columns: new[] { "TenantId", "SecretHash" }, unique: true);

            migrationBuilder.CreateIndex(name: "IX_WebhookSubscriptions_IsDeleted", table: "WebhookSubscriptions", column: "IsDeleted");
            migrationBuilder.CreateIndex(name: "IX_WebhookSubscriptions_TenantId", table: "WebhookSubscriptions", column: "TenantId");
            migrationBuilder.CreateIndex(name: "IX_WebhookSubscriptions_TenantId_EventType_IsEnabled", table: "WebhookSubscriptions", columns: new[] { "TenantId", "EventType", "IsEnabled" });

            migrationBuilder.CreateIndex(name: "IX_WebhookDeliveryLogs_IsDeleted", table: "WebhookDeliveryLogs", column: "IsDeleted");
            migrationBuilder.CreateIndex(name: "IX_WebhookDeliveryLogs_TenantId", table: "WebhookDeliveryLogs", column: "TenantId");
            migrationBuilder.CreateIndex(name: "IX_WebhookDeliveryLogs_TenantId_EventType_CreatedAt", table: "WebhookDeliveryLogs", columns: new[] { "TenantId", "EventType", "CreatedAt" });
            migrationBuilder.CreateIndex(name: "IX_WebhookDeliveryLogs_TenantId_Status_CreatedAt", table: "WebhookDeliveryLogs", columns: new[] { "TenantId", "Status", "CreatedAt" });
            migrationBuilder.CreateIndex(name: "IX_WebhookDeliveryLogs_TenantId_SubscriptionId_CreatedAt", table: "WebhookDeliveryLogs", columns: new[] { "TenantId", "SubscriptionId", "CreatedAt" });

            migrationBuilder.CreateIndex(name: "IX_ExternalApiCallLogs_IsDeleted", table: "ExternalApiCallLogs", column: "IsDeleted");
            migrationBuilder.CreateIndex(name: "IX_ExternalApiCallLogs_TenantId", table: "ExternalApiCallLogs", column: "TenantId");
            migrationBuilder.CreateIndex(name: "IX_ExternalApiCallLogs_TenantId_ClientId_CreatedAt", table: "ExternalApiCallLogs", columns: new[] { "TenantId", "ClientId", "CreatedAt" });
            migrationBuilder.CreateIndex(name: "IX_ExternalApiCallLogs_TenantId_Path_CreatedAt", table: "ExternalApiCallLogs", columns: new[] { "TenantId", "Path", "CreatedAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ExternalApiCallLogs");
            migrationBuilder.DropTable(name: "WebhookDeliveryLogs");
            migrationBuilder.DropTable(name: "WebhookSubscriptions");
            migrationBuilder.DropTable(name: "ApiClientSecrets");
            migrationBuilder.DropTable(name: "ApiClients");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class EA024RabbitMqDeadLetterGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "InboxMessages",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DeadLetterMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Consumer = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SourceQueue = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Exchange = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RoutingKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    MessageType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Headers = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ReplayCount = table.Column<int>(type: "int", nullable: false),
                    LastReplayedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DispositionRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_DeadLetterMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeadLetterMessages_IsDeleted",
                table: "DeadLetterMessages",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_DeadLetterMessages_TenantId",
                table: "DeadLetterMessages",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_DeadLetterMessages_TenantId_MessageId_Consumer",
                table: "DeadLetterMessages",
                columns: new[] { "TenantId", "MessageId", "Consumer" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeadLetterMessages_TenantId_SourceQueue_CreatedAt",
                table: "DeadLetterMessages",
                columns: new[] { "TenantId", "SourceQueue", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DeadLetterMessages_TenantId_Status_CreatedAt",
                table: "DeadLetterMessages",
                columns: new[] { "TenantId", "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeadLetterMessages");

            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                table: "InboxMessages");
        }
    }
}

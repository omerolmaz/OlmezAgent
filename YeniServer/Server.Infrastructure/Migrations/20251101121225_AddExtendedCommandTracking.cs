using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExtendedCommandTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "Commands",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "Commands",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ExecutionDurationMs",
                table: "Commands",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxRetries",
                table: "Commands",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "Commands",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "Commands",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "SentAt",
                table: "Commands",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SessionId",
                table: "Commands",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CommandId",
                table: "AuditLogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeviceId",
                table: "AuditLogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "AuditLogs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EventType",
                table: "AuditLogs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Success",
                table: "AuditLogs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "UserAgent",
                table: "AuditLogs",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AgentSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SessionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SessionData = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastActivityAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentSessions_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CommandId",
                table: "AuditLogs",
                column: "CommandId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_DeviceId",
                table: "AuditLogs",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentSessions_DeviceId",
                table: "AgentSessions",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentSessions_IsActive",
                table: "AgentSessions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AgentSessions_SessionId",
                table: "AgentSessions",
                column: "SessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentSessions_SessionType",
                table: "AgentSessions",
                column: "SessionType");

            migrationBuilder.CreateIndex(
                name: "IX_AgentSessions_UserId",
                table: "AgentSessions",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_Commands_CommandId",
                table: "AuditLogs",
                column: "CommandId",
                principalTable: "Commands",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_Devices_DeviceId",
                table: "AuditLogs",
                column: "DeviceId",
                principalTable: "Devices",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_Commands_CommandId",
                table: "AuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_Devices_DeviceId",
                table: "AuditLogs");

            migrationBuilder.DropTable(
                name: "AgentSessions");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_CommandId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_DeviceId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Commands");

            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                table: "Commands");

            migrationBuilder.DropColumn(
                name: "ExecutionDurationMs",
                table: "Commands");

            migrationBuilder.DropColumn(
                name: "MaxRetries",
                table: "Commands");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "Commands");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "Commands");

            migrationBuilder.DropColumn(
                name: "SentAt",
                table: "Commands");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "Commands");

            migrationBuilder.DropColumn(
                name: "CommandId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "DeviceId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "EventType",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "Success",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "UserAgent",
                table: "AuditLogs");
        }
    }
}

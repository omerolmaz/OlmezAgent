using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAdminPasswordHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "PasswordHash",
                value: "PrP+ZrMeO00Q+nC1ytSccRIpSvauTkdqHEBRVdRaoSE=");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "PasswordHash",
                value: "AQAAAAEAACcQAAAAEFVE5cFBz7TdnqxtL+8n4OKG1xOPHHfYGQw2LKTlZwI7AwVmNv5PxkGv5xGj+Qfx5g==");
        }
    }
}

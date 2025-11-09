using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeviceInventories",
                columns: table => new
                {
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CollectedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Manufacturer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Model = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    BiosVersion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BiosManufacturer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BiosReleaseDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SystemSKU = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SystemFamily = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChassisType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProcessorName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProcessorManufacturer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProcessorCores = table.Column<int>(type: "int", nullable: true),
                    ProcessorLogicalProcessors = table.Column<int>(type: "int", nullable: true),
                    ProcessorArchitecture = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProcessorMaxClockSpeed = table.Column<int>(type: "int", nullable: true),
                    TotalPhysicalMemory = table.Column<long>(type: "bigint", nullable: true),
                    MemorySlots = table.Column<int>(type: "int", nullable: true),
                    MemoryType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MemorySpeed = table.Column<int>(type: "int", nullable: true),
                    TotalDiskSpace = table.Column<long>(type: "bigint", nullable: true),
                    DiskCount = table.Column<int>(type: "int", nullable: true),
                    OsName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OsVersion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OsBuild = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OsArchitecture = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OsInstallDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OsSerialNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OsProductKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PrimaryMacAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PrimaryIpAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HostName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DomainName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GraphicsCard = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GraphicsCardMemory = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CurrentResolution = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NetworkAdapters = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DiskDrives = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MonitorInfo = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceInventories", x => x.DeviceId);
                    table.ForeignKey(
                        name: "FK_DeviceInventories_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InstalledPatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CollectedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HotFixId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InstalledOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InstalledBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstalledPatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstalledPatches_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InstalledSoftware",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CollectedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Version = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Publisher = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InstallDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InstallLocation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SizeInBytes = table.Column<long>(type: "bigint", nullable: true),
                    UninstallString = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RegistryPath = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstalledSoftware", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstalledSoftware_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceInventories_CollectedAt",
                table: "DeviceInventories",
                column: "CollectedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceInventories_SerialNumber",
                table: "DeviceInventories",
                column: "SerialNumber");

            migrationBuilder.CreateIndex(
                name: "IX_InstalledPatches_CollectedAt",
                table: "InstalledPatches",
                column: "CollectedAt");

            migrationBuilder.CreateIndex(
                name: "IX_InstalledPatches_DeviceId_HotFixId",
                table: "InstalledPatches",
                columns: new[] { "DeviceId", "HotFixId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InstalledSoftware_CollectedAt",
                table: "InstalledSoftware",
                column: "CollectedAt");

            migrationBuilder.CreateIndex(
                name: "IX_InstalledSoftware_DeviceId_RegistryPath",
                table: "InstalledSoftware",
                columns: new[] { "DeviceId", "RegistryPath" },
                unique: true,
                filter: "[RegistryPath] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InstalledSoftware_Name",
                table: "InstalledSoftware",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceInventories");

            migrationBuilder.DropTable(
                name: "InstalledPatches");

            migrationBuilder.DropTable(
                name: "InstalledSoftware");
        }
    }
}

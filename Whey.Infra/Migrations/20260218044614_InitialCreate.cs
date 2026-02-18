using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Whey.Core.Models;

#nullable disable

namespace Whey.Infra.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicKey = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<string>(type: "text", nullable: true),
                    Platform = table.Column<string>(type: "text", nullable: true),
                    TokenExpiry = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RegisteredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Packages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Owner = table.Column<string>(type: "text", nullable: false),
                    Repo = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<string>(type: "text", nullable: false),
                    LastReleased = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ETag = table.Column<string>(type: "text", nullable: true),
                    SupportedPlatforms = table.Column<int>(type: "integer", nullable: false),
                    Dependencies = table.Column<Dictionary<Platform, string[]>>(type: "jsonb", nullable: false),
                    ReleaseAssets = table.Column<Dictionary<Platform, string[]>>(type: "jsonb", nullable: false),
                    LastPolled = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Packages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PackageStats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Installs = table.Column<string>(type: "jsonb", nullable: false),
                    TotalInteractions = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageStats", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clients_PublicKey",
                table: "Clients",
                column: "PublicKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Packages_Owner_Repo",
                table: "Packages",
                columns: new[] { "Owner", "Repo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PackageStats_TotalInteractions",
                table: "PackageStats",
                column: "TotalInteractions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Clients");

            migrationBuilder.DropTable(
                name: "Packages");

            migrationBuilder.DropTable(
                name: "PackageStats");
        }
    }
}

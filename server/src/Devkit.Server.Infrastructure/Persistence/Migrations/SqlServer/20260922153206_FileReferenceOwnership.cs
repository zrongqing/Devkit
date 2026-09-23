using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Devkit.Server.Infrastructure.Persistence.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class FileReferenceOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "FileReferences",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "FileReferences");
        }
    }
}

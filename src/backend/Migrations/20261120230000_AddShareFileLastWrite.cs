using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileShare.Migrations
{
    /// <inheritdoc />
    public partial class AddShareFileLastWrite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FileLastWriteUtc",
                table: "Shares",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileLastWriteUtc",
                table: "Shares");
        }
    }
}

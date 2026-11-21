using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileShare.Migrations
{
    /// <summary>
    /// Stores only SHA-256(token). Plaintext tokens can't be hashed in SQLite, so existing shares are
    /// removed: their links stop working and must be re-created.
    /// </summary>
    public partial class HashShareTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM \"Shares\";");

            migrationBuilder.DropIndex(
                name: "IX_Shares_Token",
                table: "Shares");

            migrationBuilder.RenameColumn(
                name: "Token",
                table: "Shares",
                newName: "TokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_Shares_TokenHash",
                table: "Shares",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM \"Shares\";");

            migrationBuilder.DropIndex(
                name: "IX_Shares_TokenHash",
                table: "Shares");

            migrationBuilder.RenameColumn(
                name: "TokenHash",
                table: "Shares",
                newName: "Token");

            migrationBuilder.CreateIndex(
                name: "IX_Shares_Token",
                table: "Shares",
                column: "Token",
                unique: true);
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SolanaAPI_Test.Migrations
{
    /// <inheritdoc />
    public partial class AddEncryptedPrivateKeyToUserWallet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EncryptedPrivateKey",
                table: "UserWallets",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EncryptedPrivateKey",
                table: "UserWallets");
        }
    }
}

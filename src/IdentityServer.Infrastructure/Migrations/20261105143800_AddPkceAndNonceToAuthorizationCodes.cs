using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityServer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPkceAndNonceToAuthorizationCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CodeChallenge",
                table: "AuthorizationCodes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CodeChallengeMethod",
                table: "AuthorizationCodes",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Nonce",
                table: "AuthorizationCodes",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            // Keep existing development databases in sync with DataSeeder: the SPA demo client uses refresh tokens.
            // (No-op on a fresh database, where the seeder inserts the client afterwards.)
            migrationBuilder.Sql("UPDATE Clients SET AllowedGrantTypes = 'authorization_code,refresh_token', AllowOfflineAccess = 1 WHERE ClientIdentifier = 'spa'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CodeChallenge",
                table: "AuthorizationCodes");

            migrationBuilder.DropColumn(
                name: "CodeChallengeMethod",
                table: "AuthorizationCodes");

            migrationBuilder.DropColumn(
                name: "Nonce",
                table: "AuthorizationCodes");
        }
    }
}

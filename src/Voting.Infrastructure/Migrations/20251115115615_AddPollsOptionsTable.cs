using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SynchronousVoting.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPollsOptionsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OptionId",
                table: "Votes");

            migrationBuilder.DropColumn(
                name: "OptionsJson",
                table: "Polls");

            migrationBuilder.AddColumn<Guid>(
                name: "PollOptionId",
                table: "Votes",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.CreateTable(
                name: "PollOptions",
                columns: table => new
                {
                    PollOptionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PollId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Text = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OrderIndex = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PollOptions", x => x.PollOptionId);
                    table.ForeignKey(
                        name: "FK_PollOptions_Polls_PollId",
                        column: x => x.PollId,
                        principalTable: "Polls",
                        principalColumn: "PollId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Votes_PollOptionId",
                table: "Votes",
                column: "PollOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_PollOptions_PollId",
                table: "PollOptions",
                column: "PollId");

            migrationBuilder.AddForeignKey(
                name: "FK_Votes_PollOptions_PollOptionId",
                table: "Votes",
                column: "PollOptionId",
                principalTable: "PollOptions",
                principalColumn: "PollOptionId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Votes_PollOptions_PollOptionId",
                table: "Votes");

            migrationBuilder.DropTable(
                name: "PollOptions");

            migrationBuilder.DropIndex(
                name: "IX_Votes_PollOptionId",
                table: "Votes");

            migrationBuilder.DropColumn(
                name: "PollOptionId",
                table: "Votes");

            migrationBuilder.AddColumn<string>(
                name: "OptionId",
                table: "Votes",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "OptionsJson",
                table: "Polls",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}

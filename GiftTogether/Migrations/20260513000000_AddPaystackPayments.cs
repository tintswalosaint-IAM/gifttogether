using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiftTogether.Migrations
{
    /// <inheritdoc />
    public partial class AddPaystackPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add PaystackSubaccountCode to Users
            migrationBuilder.AddColumn<string>(
                name: "PaystackSubaccountCode",
                table: "Users",
                type: "TEXT",
                nullable: true);

            // Add PaystackReference to Contributions
            migrationBuilder.AddColumn<string>(
                name: "PaystackReference",
                table: "Contributions",
                type: "TEXT",
                nullable: true);

            // Create PendingPayments table
            migrationBuilder.CreateTable(
                name: "PendingPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Reference = table.Column<string>(type: "TEXT", nullable: false),
                    GiftGoalId = table.Column<int>(type: "INTEGER", nullable: false),
                    ContributionAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ContributorName = table.Column<string>(type: "TEXT", nullable: true),
                    ContributorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PendingPayments_GiftGoals_GiftGoalId",
                        column: x => x.GiftGoalId,
                        principalTable: "GiftGoals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingPayments_GiftGoalId",
                table: "PendingPayments",
                column: "GiftGoalId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingPayments_Reference",
                table: "PendingPayments",
                column: "Reference",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PendingPayments");

            migrationBuilder.DropColumn(name: "PaystackReference", table: "Contributions");
            migrationBuilder.DropColumn(name: "PaystackSubaccountCode", table: "Users");
        }
    }
}

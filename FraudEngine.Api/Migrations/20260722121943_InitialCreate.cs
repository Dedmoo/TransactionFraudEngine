using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FraudEngine.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TransactionId = table.Column<string>(type: "TEXT", nullable: false),
                    CustomerId = table.Column<string>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    Currency = table.Column<string>(type: "TEXT", nullable: false),
                    MerchantCategory = table.Column<string>(type: "TEXT", nullable: false),
                    CountryCode = table.Column<string>(type: "TEXT", nullable: false),
                    CustomerHomeCountry = table.Column<string>(type: "TEXT", nullable: false),
                    OccurredAt = table.Column<long>(type: "INTEGER", nullable: false),
                    TransactionsLastHourInput = table.Column<int>(type: "INTEGER", nullable: true),
                    IsNewDevice = table.Column<bool>(type: "INTEGER", nullable: false),
                    RiskScore = table.Column<int>(type: "INTEGER", nullable: false),
                    Decision = table.Column<int>(type: "INTEGER", nullable: false),
                    AssessedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VelocityEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CustomerId = table.Column<string>(type: "TEXT", nullable: false),
                    OccurredAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VelocityEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RuleHits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AssessmentAuditRecordId = table.Column<long>(type: "INTEGER", nullable: false),
                    RuleCode = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Score = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleHits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RuleHits_AuditRecords_AssessmentAuditRecordId",
                        column: x => x.AssessmentAuditRecordId,
                        principalTable: "AuditRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_AssessedAt",
                table: "AuditRecords",
                column: "AssessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_CustomerId",
                table: "AuditRecords",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_TransactionId",
                table: "AuditRecords",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_RuleHits_AssessmentAuditRecordId",
                table: "RuleHits",
                column: "AssessmentAuditRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_VelocityEvents_CustomerId_OccurredAt",
                table: "VelocityEvents",
                columns: new[] { "CustomerId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RuleHits");

            migrationBuilder.DropTable(
                name: "VelocityEvents");

            migrationBuilder.DropTable(
                name: "AuditRecords");
        }
    }
}

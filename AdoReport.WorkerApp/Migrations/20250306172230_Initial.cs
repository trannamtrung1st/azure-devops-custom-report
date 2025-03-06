using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AdoReport.WorkerApp.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AreaPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ParentId = table.Column<int>(type: "integer", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChangedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Fields = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_AreaPath",
                table: "WorkItems",
                column: "AreaPath");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_ChangedDate",
                table: "WorkItems",
                column: "ChangedDate");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_CreatedDate",
                table: "WorkItems",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_Fields",
                table: "WorkItems",
                column: "Fields")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_ParentId",
                table: "WorkItems",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_State",
                table: "WorkItems",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_Type",
                table: "WorkItems",
                column: "Type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkItems");
        }
    }
}

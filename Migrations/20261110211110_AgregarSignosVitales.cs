using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EDSOM.Migrations
{
    /// <inheritdoc />
    public partial class AgregarSignosVitales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConsultasSignosVitales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FuncionarioId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Fecha = table.Column<DateTime>(type: "date", nullable: false),
                    Hora = table.Column<TimeSpan>(type: "time", nullable: false),
                    Servicio = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PresionSistolica = table.Column<int>(type: "int", nullable: false),
                    PresionDiastolica = table.Column<int>(type: "int", nullable: false),
                    Pulso = table.Column<int>(type: "int", nullable: false),
                    Observaciones = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RegistradoPor = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsultasSignosVitales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsultasSignosVitales_Funcionarios_FuncionarioId",
                        column: x => x.FuncionarioId,
                        principalTable: "Funcionarios",
                        principalColumn: "Id_Cedula",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsultasSignosVitales_Fecha",
                table: "ConsultasSignosVitales",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultasSignosVitales_FuncionarioId_Fecha",
                table: "ConsultasSignosVitales",
                columns: new[] { "FuncionarioId", "Fecha" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsultasSignosVitales");
        }
    }
}

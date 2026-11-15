using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeanSceneReservationSystem.Migrations
{
    /// <inheritdoc />
    public partial class CorrectReservationModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ReservationDate",
                table: "Reservations",
                newName: "StartTime");

            migrationBuilder.AddColumn<int>(
                name: "Duration",
                table: "Reservations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Reservations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Duration",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Reservations");

            migrationBuilder.RenameColumn(
                name: "StartTime",
                table: "Reservations",
                newName: "ReservationDate");
        }
    }
}

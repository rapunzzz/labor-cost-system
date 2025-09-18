using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class NewModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LineConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LineName = table.Column<string>(type: "TEXT", nullable: false),
                    DefaultCapacity = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LineConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModelReferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ModelName = table.Column<string>(type: "TEXT", nullable: false),
                    SUT = table.Column<double>(type: "REAL", nullable: false),
                    HeadCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelReferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShiftWorkConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WorkType = table.Column<int>(type: "INTEGER", nullable: false),
                    RegularDayMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    FridayMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftWorkConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OptimizedLineCapacities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LineConfigurationId = table.Column<int>(type: "INTEGER", nullable: false),
                    Month = table.Column<string>(type: "TEXT", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    WorkType = table.Column<int>(type: "INTEGER", nullable: false),
                    RequiredWorkers = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OptimizedLineCapacities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OptimizedLineCapacities_LineConfigurations_LineConfigurationId",
                        column: x => x.LineConfigurationId,
                        principalTable: "LineConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ModelDatas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ModelName = table.Column<string>(type: "TEXT", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    Month = table.Column<string>(type: "TEXT", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    ModelReferenceId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelDatas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelDatas_ModelReferences_ModelReferenceId",
                        column: x => x.ModelReferenceId,
                        principalTable: "ModelReferences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OvertimeProductionAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ModelDataId = table.Column<int>(type: "INTEGER", nullable: false),
                    LineId = table.Column<int>(type: "INTEGER", nullable: false),
                    AssignedQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    PlannedHours = table.Column<double>(type: "REAL", nullable: false),
                    ChangeoverHours = table.Column<double>(type: "REAL", nullable: false),
                    RequiredWorkers = table.Column<int>(type: "INTEGER", nullable: false),
                    ActualAllocatedWorkers = table.Column<int>(type: "INTEGER", nullable: false),
                    DefaultCapacity = table.Column<int>(type: "INTEGER", nullable: false),
                    SurplusWorkers = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OvertimeProductionAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OvertimeProductionAssignments_LineConfigurations_LineId",
                        column: x => x.LineId,
                        principalTable: "LineConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OvertimeProductionAssignments_ModelDatas_ModelDataId",
                        column: x => x.ModelDataId,
                        principalTable: "ModelDatas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductionAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ModelDataId = table.Column<int>(type: "INTEGER", nullable: false),
                    LineId = table.Column<int>(type: "INTEGER", nullable: false),
                    AssignedQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    PlannedHours = table.Column<double>(type: "REAL", nullable: false),
                    ChangeoverHours = table.Column<double>(type: "REAL", nullable: false),
                    RequiredWorkers = table.Column<int>(type: "INTEGER", nullable: false),
                    ActualAllocatedWorkers = table.Column<int>(type: "INTEGER", nullable: false),
                    DefaultCapacity = table.Column<int>(type: "INTEGER", nullable: false),
                    SurplusWorkers = table.Column<int>(type: "INTEGER", nullable: false),
                    AssignedShift = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionAssignments_LineConfigurations_LineId",
                        column: x => x.LineId,
                        principalTable: "LineConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionAssignments_ModelDatas_ModelDataId",
                        column: x => x.ModelDataId,
                        principalTable: "ModelDatas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModelDatas_ModelReferenceId",
                table: "ModelDatas",
                column: "ModelReferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_OptimizedLineCapacities_LineConfigurationId",
                table: "OptimizedLineCapacities",
                column: "LineConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeProductionAssignments_LineId",
                table: "OvertimeProductionAssignments",
                column: "LineId");

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeProductionAssignments_ModelDataId",
                table: "OvertimeProductionAssignments",
                column: "ModelDataId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionAssignments_LineId",
                table: "ProductionAssignments",
                column: "LineId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionAssignments_ModelDataId",
                table: "ProductionAssignments",
                column: "ModelDataId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OptimizedLineCapacities");

            migrationBuilder.DropTable(
                name: "OvertimeProductionAssignments");

            migrationBuilder.DropTable(
                name: "ProductionAssignments");

            migrationBuilder.DropTable(
                name: "ShiftWorkConfigurations");

            migrationBuilder.DropTable(
                name: "LineConfigurations");

            migrationBuilder.DropTable(
                name: "ModelDatas");

            migrationBuilder.DropTable(
                name: "ModelReferences");
        }
    }
}

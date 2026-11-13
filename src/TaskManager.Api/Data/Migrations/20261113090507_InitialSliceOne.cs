using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSliceOne : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "workspaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspaces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "device_credentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Verifier = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IssuedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_credentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_device_credentials_workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recovery_credentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Verifier = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IssuedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recovery_credentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_recovery_credentials_workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "task_lists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_lists", x => x.Id);
                    table.UniqueConstraint("AK_task_lists_WorkspaceId_Id", x => new { x.WorkspaceId, x.Id });
                    table.ForeignKey(
                        name: "FK_task_lists_workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workspace_creation_operations",
                columns: table => new
                {
                    KeyDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspace_creation_operations", x => x.KeyDigest);
                    table.ForeignKey(
                        name: "FK_workspace_creation_operations_workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ListId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Priority = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tasks", x => x.Id);
                    table.UniqueConstraint("AK_tasks_WorkspaceId_Id", x => new { x.WorkspaceId, x.Id });
                    table.CheckConstraint("ck_tasks_priority", "\"Priority\" IS NULL OR \"Priority\" IN ('Low', 'Medium', 'High')");
                    table.ForeignKey(
                        name: "FK_tasks_task_lists_WorkspaceId_ListId",
                        columns: x => new { x.WorkspaceId, x.ListId },
                        principalTable: "task_lists",
                        principalColumns: new[] { "WorkspaceId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tasks_workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "task_creation_operations",
                columns: table => new
                {
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    KeyDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_creation_operations", x => new { x.WorkspaceId, x.KeyDigest });
                    table.ForeignKey(
                        name: "FK_task_creation_operations_tasks_WorkspaceId_TaskId",
                        columns: x => new { x.WorkspaceId, x.TaskId },
                        principalTable: "tasks",
                        principalColumns: new[] { "WorkspaceId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_device_credentials_Verifier",
                table: "device_credentials",
                column: "Verifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_device_credentials_WorkspaceId",
                table: "device_credentials",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_recovery_credentials_Verifier",
                table: "recovery_credentials",
                column: "Verifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recovery_credentials_WorkspaceId",
                table: "recovery_credentials",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_task_creation_operations_WorkspaceId_TaskId",
                table: "task_creation_operations",
                columns: new[] { "WorkspaceId", "TaskId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_task_lists_WorkspaceId_Id",
                table: "task_lists",
                columns: new[] { "WorkspaceId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tasks_WorkspaceId_CreatedAtUtc",
                table: "tasks",
                columns: new[] { "WorkspaceId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_tasks_WorkspaceId_ListId",
                table: "tasks",
                columns: new[] { "WorkspaceId", "ListId" });

            migrationBuilder.CreateIndex(
                name: "IX_workspace_creation_operations_WorkspaceId",
                table: "workspace_creation_operations",
                column: "WorkspaceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_credentials");

            migrationBuilder.DropTable(
                name: "recovery_credentials");

            migrationBuilder.DropTable(
                name: "task_creation_operations");

            migrationBuilder.DropTable(
                name: "workspace_creation_operations");

            migrationBuilder.DropTable(
                name: "tasks");

            migrationBuilder.DropTable(
                name: "task_lists");

            migrationBuilder.DropTable(
                name: "workspaces");
        }
    }
}

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace TaskManager.Api.Data.Migrations;

[DbContext(typeof(TaskManagerDbContext))]
[Migration("20261113000000_InitialCreate")]
public sealed class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            CREATE TABLE workspaces (
                id uuid PRIMARY KEY,
                time_zone text NOT NULL,
                created_at timestamptz NOT NULL
            );
            CREATE TABLE task_lists (
                id uuid PRIMARY KEY,
                workspace_id uuid NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
                name text NOT NULL,
                CONSTRAINT uq_task_lists_workspace_id UNIQUE (workspace_id, id),
                CONSTRAINT uq_task_lists_workspace_name UNIQUE (workspace_id, name)
            );
            CREATE TABLE device_credentials (
                id uuid PRIMARY KEY,
                workspace_id uuid NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
                verifier bytea NOT NULL CHECK (octet_length(verifier) = 32),
                expires_at timestamptz NOT NULL,
                revoked boolean NOT NULL DEFAULT false,
                CONSTRAINT uq_device_credentials_verifier UNIQUE (verifier)
            );
            CREATE TABLE recovery_credentials (
                workspace_id uuid PRIMARY KEY REFERENCES workspaces(id) ON DELETE CASCADE,
                verifier bytea NOT NULL CHECK (octet_length(verifier) = 32)
            );
            CREATE TABLE workspace_create_operations (
                key_digest bytea PRIMARY KEY CHECK (octet_length(key_digest) = 32),
                request_fingerprint bytea NOT NULL CHECK (octet_length(request_fingerprint) = 32),
                workspace_id uuid NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
                device_id uuid NOT NULL REFERENCES device_credentials(id) ON DELETE CASCADE,
                created_at timestamptz NOT NULL
            );
            CREATE TABLE tasks (
                id uuid PRIMARY KEY,
                workspace_id uuid NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
                list_id uuid NOT NULL,
                title text NOT NULL CHECK (length(btrim(title)) > 0),
                description text NULL,
                due_date date NULL,
                priority text NULL CHECK (priority IS NULL OR priority IN ('Low', 'Medium', 'High')),
                created_at timestamptz NOT NULL,
                CONSTRAINT uq_tasks_workspace_id UNIQUE (workspace_id, id),
                CONSTRAINT fk_tasks_workspace_list FOREIGN KEY (workspace_id, list_id)
                    REFERENCES task_lists(workspace_id, id) ON DELETE RESTRICT
            );
            CREATE TABLE task_create_operations (
                workspace_id uuid NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
                key_digest bytea NOT NULL CHECK (octet_length(key_digest) = 32),
                request_fingerprint bytea NOT NULL CHECK (octet_length(request_fingerprint) = 32),
                task_id uuid NOT NULL,
                created_at timestamptz NOT NULL,
                PRIMARY KEY (workspace_id, key_digest),
                CONSTRAINT fk_task_create_operations_task FOREIGN KEY (workspace_id, task_id)
                    REFERENCES tasks(workspace_id, id) ON DELETE CASCADE
            );
            """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            DROP TABLE task_create_operations;
            DROP TABLE tasks;
            DROP TABLE workspace_create_operations;
            DROP TABLE recovery_credentials;
            DROP TABLE device_credentials;
            DROP TABLE task_lists;
            DROP TABLE workspaces;
            """);
}

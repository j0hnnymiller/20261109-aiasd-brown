using Microsoft.EntityFrameworkCore;

namespace TaskManager.Api.Data;

public sealed class TaskManagerDbContext(DbContextOptions<TaskManagerDbContext> options)
    : DbContext(options)
{
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<TaskList> TaskLists => Set<TaskList>();
    public DbSet<DeviceCredential> DeviceCredentials => Set<DeviceCredential>();
    public DbSet<RecoveryCredential> RecoveryCredentials => Set<RecoveryCredential>();
    public DbSet<WorkspaceCreationOperation> WorkspaceCreationOperations => Set<WorkspaceCreationOperation>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<TaskCreationOperation> TaskCreationOperations => Set<TaskCreationOperation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Workspace>(entity =>
        {
            entity.ToTable("workspaces");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.TimeZoneId).HasMaxLength(100).IsRequired();
            entity.Property(item => item.CreatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<TaskList>(entity =>
        {
            entity.ToTable("task_lists");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.WorkspaceId, item.Id }).IsUnique();
            entity.Property(item => item.Name).HasMaxLength(100).IsRequired();
            entity.HasOne(item => item.Workspace)
                .WithMany(item => item.TaskLists)
                .HasForeignKey(item => item.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceCredential>(entity =>
        {
            entity.ToTable("device_credentials");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.Verifier).IsUnique();
            entity.Property(item => item.Verifier).HasMaxLength(64).IsRequired();
            entity.HasOne(item => item.Workspace)
                .WithMany(item => item.DeviceCredentials)
                .HasForeignKey(item => item.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RecoveryCredential>(entity =>
        {
            entity.ToTable("recovery_credentials");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.Verifier).IsUnique();
            entity.Property(item => item.Verifier).HasMaxLength(64).IsRequired();
            entity.HasOne(item => item.Workspace)
                .WithMany(item => item.RecoveryCredentials)
                .HasForeignKey(item => item.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkspaceCreationOperation>(entity =>
        {
            entity.ToTable("workspace_creation_operations");
            entity.HasKey(item => item.KeyDigest);
            entity.Property(item => item.KeyDigest).HasMaxLength(64);
            entity.Property(item => item.RequestFingerprint).HasMaxLength(64).IsRequired();
            entity.HasOne(item => item.Workspace)
                .WithMany()
                .HasForeignKey(item => item.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TaskItem>(entity =>
        {
            entity.ToTable("tasks", table =>
            {
                table.HasCheckConstraint(
                    "ck_tasks_priority",
                    "\"Priority\" IS NULL OR \"Priority\" IN ('Low', 'Medium', 'High')");
            });
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.WorkspaceId, item.CreatedAtUtc });
            entity.HasAlternateKey(item => new { item.WorkspaceId, item.Id });
            entity.Property(item => item.Title).HasColumnType("text").IsRequired();
            entity.Property(item => item.Description).HasColumnType("text");
            entity.Property(item => item.DueDate).HasColumnType("date");
            entity.Property(item => item.Priority).HasMaxLength(10);
            entity.HasOne(item => item.Workspace)
                .WithMany()
                .HasForeignKey(item => item.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.TaskList)
                .WithMany(item => item.Tasks)
                .HasForeignKey(item => new { item.WorkspaceId, item.ListId })
                .HasPrincipalKey(item => new { item.WorkspaceId, item.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TaskCreationOperation>(entity =>
        {
            entity.ToTable("task_creation_operations");
            entity.HasKey(item => new { item.WorkspaceId, item.KeyDigest });
            entity.Property(item => item.KeyDigest).HasMaxLength(64);
            entity.Property(item => item.RequestFingerprint).HasMaxLength(64).IsRequired();
            entity.HasOne(item => item.Task)
                .WithOne()
                .HasForeignKey<TaskCreationOperation>(item => new { item.WorkspaceId, item.TaskId })
                .HasPrincipalKey<TaskItem>(item => new { item.WorkspaceId, item.Id })
                .OnDelete(DeleteBehavior.Cascade);
            entity.Property(item => item.TaskId).IsRequired();
        });
    }
}

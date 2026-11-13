using Microsoft.EntityFrameworkCore;

namespace TaskManager.Api.Data;

public sealed class TaskManagerDbContext(DbContextOptions<TaskManagerDbContext> options) : DbContext(options)
{
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<TaskList> TaskLists => Set<TaskList>();
    public DbSet<DeviceCredential> DeviceCredentials => Set<DeviceCredential>();
    public DbSet<RecoveryCredential> RecoveryCredentials => Set<RecoveryCredential>();
    public DbSet<WorkspaceCreateOperation> WorkspaceCreateOperations => Set<WorkspaceCreateOperation>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<TaskCreateOperation> TaskCreateOperations => Set<TaskCreateOperation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Workspace>(entity =>
        {
            entity.ToTable("workspaces");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Id).HasColumnName("id");
            entity.Property(value => value.TimeZone).HasColumnName("time_zone").IsRequired();
            entity.Property(value => value.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<TaskList>(entity =>
        {
            entity.ToTable("task_lists");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Id).HasColumnName("id");
            entity.Property(value => value.WorkspaceId).HasColumnName("workspace_id");
            entity.Property(value => value.Name).HasColumnName("name").IsRequired();
            entity.HasIndex(value => new { value.WorkspaceId, value.Id }).IsUnique();
            entity.HasIndex(value => new { value.WorkspaceId, value.Name }).IsUnique();
            entity.HasOne(value => value.Workspace)
                .WithMany(value => value.Lists)
                .HasForeignKey(value => value.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceCredential>(entity =>
        {
            entity.ToTable("device_credentials");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Id).HasColumnName("id");
            entity.Property(value => value.WorkspaceId).HasColumnName("workspace_id");
            entity.Property(value => value.Verifier).HasColumnName("verifier").IsRequired();
            entity.Property(value => value.ExpiresAt).HasColumnName("expires_at");
            entity.Property(value => value.Revoked).HasColumnName("revoked");
            entity.HasIndex(value => value.Verifier).IsUnique();
            entity.HasOne<Workspace>().WithMany().HasForeignKey(value => value.WorkspaceId);
        });

        modelBuilder.Entity<RecoveryCredential>(entity =>
        {
            entity.ToTable("recovery_credentials");
            entity.HasKey(value => value.WorkspaceId);
            entity.Property(value => value.WorkspaceId).HasColumnName("workspace_id");
            entity.Property(value => value.Verifier).HasColumnName("verifier").IsRequired();
            entity.HasOne<Workspace>().WithOne().HasForeignKey<RecoveryCredential>(value => value.WorkspaceId);
        });

        modelBuilder.Entity<WorkspaceCreateOperation>(entity =>
        {
            entity.ToTable("workspace_create_operations");
            entity.HasKey(value => value.KeyDigest);
            entity.Property(value => value.KeyDigest).HasColumnName("key_digest");
            entity.Property(value => value.RequestFingerprint).HasColumnName("request_fingerprint");
            entity.Property(value => value.WorkspaceId).HasColumnName("workspace_id");
            entity.Property(value => value.DeviceId).HasColumnName("device_id");
            entity.Property(value => value.CreatedAt).HasColumnName("created_at");
            entity.HasOne<Workspace>().WithMany().HasForeignKey(value => value.WorkspaceId);
            entity.HasOne<DeviceCredential>().WithMany().HasForeignKey(value => value.DeviceId);
        });

        modelBuilder.Entity<TaskItem>(entity =>
        {
            entity.ToTable("tasks");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Id).HasColumnName("id");
            entity.Property(value => value.WorkspaceId).HasColumnName("workspace_id");
            entity.Property(value => value.ListId).HasColumnName("list_id");
            entity.Property(value => value.Title).HasColumnName("title").IsRequired();
            entity.Property(value => value.Description).HasColumnName("description");
            entity.Property(value => value.DueDate).HasColumnName("due_date").HasColumnType("date");
            entity.Property(value => value.Priority).HasColumnName("priority").HasConversion<string>();
            entity.Property(value => value.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(value => new { value.WorkspaceId, value.Id }).IsUnique();
            entity.HasOne<Workspace>().WithMany().HasForeignKey(value => value.WorkspaceId);
            entity.HasOne<TaskList>().WithMany().HasForeignKey(value => new { value.WorkspaceId, value.ListId })
                .HasPrincipalKey(value => new { value.WorkspaceId, value.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TaskCreateOperation>(entity =>
        {
            entity.ToTable("task_create_operations");
            entity.HasKey(value => new { value.WorkspaceId, value.KeyDigest });
            entity.Property(value => value.WorkspaceId).HasColumnName("workspace_id");
            entity.Property(value => value.KeyDigest).HasColumnName("key_digest");
            entity.Property(value => value.RequestFingerprint).HasColumnName("request_fingerprint");
            entity.Property(value => value.TaskId).HasColumnName("task_id");
            entity.Property(value => value.CreatedAt).HasColumnName("created_at");
            entity.HasOne<Workspace>().WithMany().HasForeignKey(value => value.WorkspaceId);
            entity.HasOne<TaskItem>().WithMany().HasForeignKey(value => new { value.WorkspaceId, value.TaskId })
                .HasPrincipalKey(value => new { value.WorkspaceId, value.Id });
        });
    }
}

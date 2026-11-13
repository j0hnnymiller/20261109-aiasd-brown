using Microsoft.EntityFrameworkCore;

namespace TaskManager.Api.Data;

public sealed class TaskManagerDbContext(DbContextOptions<TaskManagerDbContext> options) : DbContext(options)
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
            entity.HasKey(workspace => workspace.Id);
            entity.Property(workspace => workspace.TimeZoneId).HasMaxLength(100).IsRequired();
            entity.Property(workspace => workspace.CreatedAt).HasColumnType("timestamp with time zone");
        });

        modelBuilder.Entity<TaskList>(entity =>
        {
            entity.HasKey(list => list.Id);
            entity.HasAlternateKey(list => new { list.WorkspaceId, list.Id });
            entity.Property(list => list.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(list => new { list.WorkspaceId, list.Name }).IsUnique();
            entity.Property(list => list.CreatedAt).HasColumnType("timestamp with time zone");
            entity.HasOne<Workspace>()
                .WithMany()
                .HasForeignKey(list => list.WorkspaceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DeviceCredential>(entity =>
        {
            entity.HasKey(credential => credential.Id);
            entity.Property(credential => credential.Verifier).HasMaxLength(64).IsRequired();
            entity.HasIndex(credential => credential.Verifier).IsUnique();
            entity.Property(credential => credential.ExpiresAt).HasColumnType("timestamp with time zone");
            entity.Property(credential => credential.CreatedAt).HasColumnType("timestamp with time zone");
            entity.HasOne<Workspace>()
                .WithMany()
                .HasForeignKey(credential => credential.WorkspaceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RecoveryCredential>(entity =>
        {
            entity.HasKey(credential => credential.Id);
            entity.Property(credential => credential.Verifier).HasMaxLength(64).IsRequired();
            entity.HasIndex(credential => credential.WorkspaceId).IsUnique();
            entity.Property(credential => credential.CreatedAt).HasColumnType("timestamp with time zone");
            entity.HasOne<Workspace>()
                .WithMany()
                .HasForeignKey(credential => credential.WorkspaceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkspaceCreationOperation>(entity =>
        {
            entity.HasKey(operation => operation.IdempotencyKeyDigest);
            entity.Property(operation => operation.IdempotencyKeyDigest).HasMaxLength(64);
            entity.Property(operation => operation.RequestFingerprint).HasMaxLength(64).IsRequired();
            entity.Property(operation => operation.CreatedAt).HasColumnType("timestamp with time zone");
            entity.HasOne<Workspace>()
                .WithMany()
                .HasForeignKey(operation => operation.WorkspaceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<TaskList>()
                .WithMany()
                .HasForeignKey(operation => new { operation.WorkspaceId, operation.DefaultListId })
                .HasPrincipalKey(list => new { list.WorkspaceId, list.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TaskItem>(entity =>
        {
            entity.HasKey(task => task.Id);
            entity.HasAlternateKey(task => new { task.WorkspaceId, task.Id });
            entity.Property(task => task.Title).HasColumnType("text").IsRequired();
            entity.Property(task => task.Description).HasColumnType("text");
            entity.Property(task => task.Priority).HasMaxLength(10);
            entity.Property(task => task.DueDate).HasColumnType("date");
            entity.Property(task => task.CreatedAt).HasColumnType("timestamp with time zone");
            entity.HasOne<Workspace>()
                .WithMany()
                .HasForeignKey(task => task.WorkspaceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<TaskList>()
                .WithMany()
                .HasForeignKey(task => new { task.WorkspaceId, task.ListId })
                .HasPrincipalKey(list => new { list.WorkspaceId, list.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TaskCreationOperation>(entity =>
        {
            entity.HasKey(operation => new { operation.WorkspaceId, operation.IdempotencyKeyDigest });
            entity.Property(operation => operation.IdempotencyKeyDigest).HasMaxLength(64);
            entity.Property(operation => operation.RequestFingerprint).HasMaxLength(64).IsRequired();
            entity.Property(operation => operation.CreatedAt).HasColumnType("timestamp with time zone");
            entity.HasOne<Workspace>()
                .WithMany()
                .HasForeignKey(operation => operation.WorkspaceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<TaskItem>()
                .WithMany()
                .HasForeignKey(operation => new { operation.WorkspaceId, operation.TaskId })
                .HasPrincipalKey(task => new { task.WorkspaceId, task.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}

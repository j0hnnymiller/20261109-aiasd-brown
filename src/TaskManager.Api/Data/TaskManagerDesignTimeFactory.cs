using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TaskManager.Api.Data;

public sealed class TaskManagerDesignTimeFactory : IDesignTimeDbContextFactory<TaskManagerDbContext>
{
    public TaskManagerDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__TaskManager")
            ?? throw new InvalidOperationException("Set ConnectionStrings__TaskManager before running EF migration commands.");
        var options = new DbContextOptionsBuilder<TaskManagerDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new TaskManagerDbContext(options);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TaskManager.Api.Data;

public sealed class TaskManagerDbContextFactory : IDesignTimeDbContextFactory<TaskManagerDbContext>
{
    public TaskManagerDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TaskManagerDbContext>()
            .UseNpgsql("Host=localhost;Database=design_time_only;Username=postgres")
            .Options;
        return new TaskManagerDbContext(options);
    }
}

using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Data;
using TaskManager.Api.Features.Tasks;
using TaskManager.Api.Features.Workspace;
using TaskManager.Api.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContextFactory<TaskManagerDbContext>((services, options) =>
{
    var connectionString = services.GetRequiredService<IConfiguration>().GetConnectionString("TaskManager")
        ?? throw new InvalidOperationException("Configure the TaskManager PostgreSQL connection string.");
    options.UseNpgsql(connectionString);
});
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Use(RequestProtection.MiddlewareAsync);
app.MapGet("/health/ready", async (IDbContextFactory<TaskManagerDbContext> contextFactory, CancellationToken cancellationToken) =>
{
    await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
    return await database.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { status = "ready" })
        : Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "The service is not ready.");
});
app.MapWorkspaceEndpoints();
app.MapTaskEndpoints();

await using (var database = await app.Services.GetRequiredService<IDbContextFactory<TaskManagerDbContext>>()
    .CreateDbContextAsync())
{
    await database.Database.MigrateAsync();
}

app.Run();

public partial class Program { }

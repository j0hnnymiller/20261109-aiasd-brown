using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TaskManager.Api.Data;
using TaskManager.Api.Features.Tasks;
using TaskManager.Api.Features.Workspace;

var builder = WebApplication.CreateBuilder(args);
var apiTestConnection = Environment.GetEnvironmentVariable("TASK_MANAGER_TEST_CONNECTION");
var connectionString = apiTestConnection ?? builder.Configuration.GetConnectionString("TaskManager");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("The TaskManager database connection is required.");
}

NpgsqlConnectionStringBuilder parsedConnection;
try
{
    parsedConnection = new NpgsqlConnectionStringBuilder(connectionString);
}
catch
{
    throw new InvalidOperationException("The TaskManager database connection is invalid.");
}

var expectedDatabase = Environment.GetEnvironmentVariable("TASK_MANAGER_EXPECTED_DATABASE") ??
    builder.Configuration["TaskManager:ExpectedDatabase"] ??
    (apiTestConnection is null ? null : "task_manager_test");
if (!string.IsNullOrWhiteSpace(expectedDatabase) &&
    !string.Equals(parsedConnection.Database, expectedDatabase, StringComparison.Ordinal))
{
    throw new InvalidOperationException("The configured TaskManager database does not match the required test database.");
}

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddDbContext<TaskManagerDbContext>(options => options.UseNpgsql(connectionString));

var app = builder.Build();

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch
    {
        if (context.Response.HasStarted)
        {
            context.Abort();
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            type = "about:blank",
            title = "Unexpected service failure.",
            status = StatusCodes.Status500InternalServerError
        });
    }
});

app.Use(async (context, next) =>
{
    if (!context.Request.IsHttps)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new
        {
            title = "HTTPS is required.",
            status = StatusCodes.Status400BadRequest
        });
        return;
    }

    await next();
});

var api = app.MapGroup("/api");
WorkspaceEndpoints.Map(api);
TaskEndpoints.Map(api);
api.MapGet("/health/ready", async (TaskManagerDbContext database, CancellationToken cancellationToken) =>
    await database.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { status = "ready" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));

if (builder.Configuration.GetValue("TaskManager:RunMigrationsOnStartup", true))
{
    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<TaskManagerDbContext>();
    await database.Database.MigrateAsync();
}

await app.RunAsync();

public partial class Program { }

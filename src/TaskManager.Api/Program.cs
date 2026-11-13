using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Data;
using TaskManager.Api.Features.Tasks;
using TaskManager.Api.Features.Workspace;
using TaskManager.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var testConnectionString = builder.Configuration["TASK_MANAGER_TEST_CONNECTION"];
var testOwnedDatabase = !string.IsNullOrWhiteSpace(testConnectionString)
    || builder.Configuration.GetValue<bool>("TestInfrastructure:OwnedDatabase");
var connectionString = DatabaseConnectionPolicy.RequireConnectionString(
    builder.Configuration,
    builder.Environment,
    testConnectionString,
    testOwnedDatabase);
var allowedOrigins = builder.Configuration.GetSection("Security:AllowedOrigins").Get<string[]>()
    ?? (testOwnedDatabase || builder.Environment.IsDevelopment()
        ? ["http://localhost", "http://localhost:5173", "http://127.0.0.1:5173"]
        : []);
if (allowedOrigins.Length == 0)
{
    throw new InvalidOperationException("Security:AllowedOrigins must be configured.");
}

builder.Services.AddDbContext<TaskManagerDbContext>((services, options) =>
    options.UseNpgsql(connectionString)
        .AddInterceptors(services.GetRequiredService<DatabaseFailureInterceptor>()));
builder.Services.AddSingleton<DatabaseFailureSwitch>();
builder.Services.AddSingleton<DatabaseFailureInterceptor>();
builder.Services.AddSingleton<DatabaseConnectionPolicy>();
builder.Services.AddSingleton(new SecurityOptions(allowedOrigins));
builder.Services.AddScoped<DeviceAuthorization>();

var app = builder.Build();

app.UseMiddleware<SanitizedExceptionMiddleware>();
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    await next();
});

using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider.GetRequiredService<TaskManagerDbContext>();
    await database.Database.MigrateAsync();
}

app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/time-zones", TimeZoneEndpoints.GetSupportedTimeZones);
app.MapGet("/api/security/csrf", CsrfEndpoints.Issue);
app.MapGet("/api/workspace", WorkspaceEndpoints.Read);
app.MapPost("/api/workspaces", WorkspaceEndpoints.Create);
app.MapGet("/api/tasks", TaskEndpoints.Read);
app.MapPost("/api/tasks", TaskEndpoints.Create);

app.Run();

public partial class Program
{
}

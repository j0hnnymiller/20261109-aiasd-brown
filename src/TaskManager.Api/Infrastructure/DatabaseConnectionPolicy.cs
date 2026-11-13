using Microsoft.AspNetCore.Hosting;
using Npgsql;

namespace TaskManager.Api.Infrastructure;

public sealed class DatabaseConnectionPolicy
{
    public static string RequireConnectionString(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        string? testConnectionString,
        bool testOwnedDatabase)
    {
        var connectionString = testOwnedDatabase
            ? testConnectionString ?? configuration["ConnectionStrings:TaskManager"]
            : configuration["ConnectionStrings:TaskManager"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:TaskManager must be configured.");
        }

        NpgsqlConnectionStringBuilder settings;
        try
        {
            settings = new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException("ConnectionStrings:TaskManager must be a valid PostgreSQL connection string.");
        }

        if (testOwnedDatabase)
        {
            RequireLocalDatabase(settings, "_test");
        }
        else if (environment.IsEnvironment("E2E"))
        {
            RequireLocalDatabase(settings, "_e2e");
        }
        else if (environment.IsEnvironment("Testing"))
        {
            RequireLocalDatabase(settings, "_test");
        }
        else if (environment.IsDevelopment())
        {
            RequireLocalDatabase(settings, "_dev");
        }

        return connectionString;
    }

    private static void RequireLocalDatabase(NpgsqlConnectionStringBuilder settings, string suffix)
    {
        var host = settings.Host ?? string.Empty;
        var localHost = host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.Equals("127.0.0.1", StringComparison.Ordinal)
            || host.Equals("::1", StringComparison.Ordinal);
        var databaseName = settings.Database ?? string.Empty;
        var userName = settings.Username ?? string.Empty;
        if (!localHost
            || !databaseName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(userName))
        {
            throw new InvalidOperationException($"This environment requires a local database whose name ends in {suffix}.");
        }
    }
}

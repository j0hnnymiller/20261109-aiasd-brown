using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using TaskManager.Api.Data;
using TaskManager.Api.Infrastructure;

namespace TaskManager.Api.Tests;

public sealed class ApiTestFactory(string environmentName = "Testing")
    : WebApplicationFactory<Program>
{
    private readonly string _connectionString = TestDatabaseConnection.Require();
    private readonly DatabaseFailureSwitch _failureSwitch = new();

    public DatabaseFailureSwitch FailureSwitch => _failureSwitch;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environmentName);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:TaskManager"] = _connectionString,
                ["TestInfrastructure:OwnedDatabase"] = "true",
                ["Security:AllowedOrigins:0"] = "http://localhost"
            });
        });
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DatabaseFailureSwitch>();
            services.AddSingleton(_failureSwitch);
        });
    }
}

internal static class TestDatabaseConnection
{
    public static string Require()
    {
        var connectionString = Environment.GetEnvironmentVariable("TASK_MANAGER_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Set TASK_MANAGER_TEST_CONNECTION to the isolated API test database.");
        }

        NpgsqlConnectionStringBuilder settings;
        try
        {
            settings = new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException("TASK_MANAGER_TEST_CONNECTION is not a valid PostgreSQL connection string.");
        }

        if (string.IsNullOrWhiteSpace(settings.Database)
            || !settings.Database.EndsWith("_test", StringComparison.OrdinalIgnoreCase)
            || !IsLoopback(settings.Host ?? string.Empty)
            || string.IsNullOrWhiteSpace(settings.Username))
        {
            throw new InvalidOperationException("API tests require a local PostgreSQL database whose name ends in _test.");
        }

        return connectionString;
    }

    private static bool IsLoopback(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || host.Equals("127.0.0.1", StringComparison.Ordinal)
        || host.Equals("::1", StringComparison.Ordinal);
}

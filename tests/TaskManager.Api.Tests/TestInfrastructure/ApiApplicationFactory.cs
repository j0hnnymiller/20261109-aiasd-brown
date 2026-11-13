using System.Security.Cryptography;
using System.Text.Json;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using TaskManager.Api.Data;

namespace TaskManager.Api.Tests.TestInfrastructure;

public sealed class ApiApplicationFactory : WebApplicationFactory<Program>
{
    public const string AllowedOrigin = "https://localhost:4173";
    private readonly string environmentName;
    private readonly bool failTaskReads;
    private readonly SafeLogSink logSink = new();

    public ApiApplicationFactory() : this("Development", false)
    {
    }

    public ApiApplicationFactory(string environmentName, bool failTaskReads = false)
    {
        this.environmentName = environmentName;
        this.failTaskReads = failTaskReads;
    }

    public IReadOnlyCollection<string> LogMessages => logSink.Messages;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environmentName);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:TaskManager"] = TestDatabase.ConnectionString,
                ["TaskManager:ExpectedDatabase"] = "task_manager_test",
                ["TaskManager:AllowedOrigins:0"] = AllowedOrigin
            });
        });
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Warning);
            logging.AddProvider(logSink);
        });
        if (failTaskReads)
        {
            builder.ConfigureServices(services =>
                services.AddDbContext<TaskManagerDbContext>(options =>
                    options.UseNpgsql(TestDatabase.ConnectionString)
                        .AddInterceptors(new TaskReadFailureInterceptor())));
        }
    }
}

public sealed class TaskReadFailureInterceptor : DbCommandInterceptor
{
    public override ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
        System.Data.Common.DbCommand command,
        CommandEventData eventData,
        InterceptionResult<System.Data.Common.DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("tasks", StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.FromException<InterceptionResult<System.Data.Common.DbDataReader>>(
                new NpgsqlException("Synthetic request-time database failure."));
        }

        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}

public sealed class SafeLogSink : ILoggerProvider
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<string> messages = new();

    public IReadOnlyCollection<string> Messages => messages.ToArray();

    public ILogger CreateLogger(string categoryName) => new SafeLogger(messages);

    public void Dispose()
    {
    }

    private sealed class SafeLogger(System.Collections.Concurrent.ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                messages.Enqueue(formatter(state, null));
            }
        }
    }
}

public static class TestDatabase
{
    private static readonly string Connection = ReadConnectionString();

    public static string ConnectionString => Connection;

    private static string ReadConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("TASK_MANAGER_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set TASK_MANAGER_TEST_CONNECTION to the isolated task_manager_test database before running API tests.");
        }

        NpgsqlConnectionStringBuilder builder;
        try
        {
            builder = new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch
        {
            throw new InvalidOperationException(
                "TASK_MANAGER_TEST_CONNECTION must be a valid connection string for the isolated task_manager_test database.");
        }

        if (!string.Equals(builder.Database, "task_manager_test", StringComparison.Ordinal) ||
            builder.Host is not ("127.0.0.1" or "localhost" or "::1"))
        {
            throw new InvalidOperationException(
                "API tests require the isolated task_manager_test database on loopback.");
        }

        return builder.ConnectionString;
    }
}

public sealed class TestSession : IAsyncDisposable
{
    private readonly string csrfToken;
    private readonly string csrfCookie;
    private string? deviceCookie;

    private TestSession(HttpClient client, string csrfToken, string csrfCookie)
    {
        Client = client;
        this.csrfToken = csrfToken;
        this.csrfCookie = csrfCookie;
    }

    public HttpClient Client { get; }
    public string? DeviceTokenForTesting => deviceCookie?.Split('=', 2).LastOrDefault();

    public string CookieHeader => deviceCookie is null
        ? csrfCookie
        : $"{csrfCookie}; {deviceCookie}";

    public static async Task<TestSession> CreateAsync(ApiApplicationFactory factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            BaseAddress = new Uri("https://localhost")
        });
        using var response = await client.GetAsync("/api/csrf");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var token = document.RootElement.GetProperty("token").GetString();
        var cookie = response.Headers.GetValues("Set-Cookie")
            .Select(value => value.Split(';', 2)[0])
            .First(value => value.StartsWith("__Host-csrf=", StringComparison.Ordinal));

        return new TestSession(client, token!, cookie);
    }

    public async Task<HttpResponseMessage> CreateWorkspaceAsync(
        string? timeZone,
        string? key = null,
        string? deviceToken = null,
        string? recoverySecret = null)
    {
        key ??= NewToken();
        deviceToken ??= NewToken();
        recoverySecret ??= NewToken();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/workspaces")
        {
            Content = JsonContent.Create(new
            {
                timeZone,
                deviceToken,
                recoverySecret
            })
        };
        request.Headers.Add("Origin", ApiApplicationFactory.AllowedOrigin);
        request.Headers.Add("X-CSRF-Token", csrfToken);
        request.Headers.Add("Idempotency-Key", key);
        request.Headers.Add("Cookie", CookieHeader);

        var response = await Client.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            deviceCookie = response.Headers.GetValues("Set-Cookie")
                .Select(value => value.Split(';', 2)[0])
                .First(value => value.StartsWith("__Host-device=", StringComparison.Ordinal));
        }

        return response;
    }

    public async Task<HttpResponseMessage> CreateTaskAsync(
        object task,
        string? key = null,
        bool includeCsrf = true)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/tasks")
        {
            Content = JsonContent.Create(task)
        };
        request.Headers.Add("Origin", ApiApplicationFactory.AllowedOrigin);
        request.Headers.Add("Idempotency-Key", key ?? NewToken());
        request.Headers.Add("Cookie", includeCsrf ? CookieHeader : deviceCookie ?? string.Empty);
        if (includeCsrf)
        {
            request.Headers.Add("X-CSRF-Token", csrfToken);
        }

        return await Client.SendAsync(request);
    }

    public async Task<HttpResponseMessage> GetTasksAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/tasks");
        if (deviceCookie is not null)
        {
            request.Headers.Add("Cookie", deviceCookie);
        }

        return await Client.SendAsync(request);
    }

    public async Task<HttpResponseMessage> GetWorkspaceAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/workspace");
        if (deviceCookie is not null)
        {
            request.Headers.Add("Cookie", deviceCookie);
        }

        return await Client.SendAsync(request);
    }

    public async Task<HttpResponseMessage> GetTasksWithDiagnosticHeaderAsync(string diagnosticHeader)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/tasks");
        if (deviceCookie is not null)
        {
            request.Headers.Add("Cookie", deviceCookie);
        }

        request.Headers.Add("X-Synthetic-Request-Header", diagnosticHeader);
        return await Client.SendAsync(request);
    }

    public static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    public ValueTask DisposeAsync()
    {
        Client.Dispose();
        return ValueTask.CompletedTask;
    }
}

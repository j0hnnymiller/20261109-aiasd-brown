using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace TaskManager.Api.Tests;

public sealed class Slice1ApiTests : IClassFixture<Slice1ApiFactory>
{
    private readonly Slice1ApiFactory _factory;

    public Slice1ApiTests(Slice1ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreatesWorkspaceAndFirstTaskAndReadsItBack()
    {
        using var client = CreateClient();
        var supportedZones = await client.GetFromJsonAsync<string[]>("/api/time-zones");
        Assert.NotNull(supportedZones);
        Assert.Contains("America/Los_Angeles", supportedZones);
        var (csrfToken, csrfCookie) = await GetCsrfTokenAsync(client);
        var create = NewWorkspaceRequest();

        using var createResponse = await CreateWorkspaceAsync(client, create, csrfToken, csrfCookie);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var creation = await createResponse.Content.ReadFromJsonAsync<WorkspaceResponse>();
        Assert.NotNull(creation);
        Assert.Equal(create.TimeZoneId, creation.TimeZoneId);
        Assert.Equal("Tasks", creation.DefaultListName);
        Assert.DoesNotContain(create.DeviceCredential, await createResponse.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.DoesNotContain(create.RecoverySecret, await createResponse.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        AssertDeviceCookie(createResponse);
        await AssertPersistedInitializationAsync(creation.WorkspaceId, create);

        var deviceCookie = GetDeviceCookieValue(createResponse);
        using var invalidResponse = await CreateTaskAsync(client, csrfToken, csrfCookie, deviceCookie, new TaskRequest("  ", null, null, null), NewKey());
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);

        var taskRequest = new TaskRequest("First task", "A test description", new DateOnly(2026, 12, 1), "High");
        using var taskResponse = await CreateTaskAsync(client, csrfToken, csrfCookie, deviceCookie, taskRequest, NewKey());
        Assert.Equal(HttpStatusCode.Created, taskResponse.StatusCode);

        var savedTask = await taskResponse.Content.ReadFromJsonAsync<TaskResponse>();
        Assert.NotNull(savedTask);
        Assert.Equal(taskRequest.Title, savedTask.Title);
        Assert.Equal(taskRequest.Description, savedTask.Description);
        Assert.Equal(taskRequest.DueDate, savedTask.DueDate);
        Assert.Equal(taskRequest.Priority, savedTask.Priority);
        Assert.Equal("Tasks", savedTask.ListName);

        using var readRequest = new HttpRequestMessage(HttpMethod.Get, "/api/tasks");
        readRequest.Headers.Add("Cookie", $"__Host-device={deviceCookie}");
        using var readResponse = await client.SendAsync(readRequest);
        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
        var tasks = await readResponse.Content.ReadFromJsonAsync<TaskResponse[]>();
        Assert.NotNull(tasks);
        Assert.Contains(tasks, task => task.Id == savedTask.Id);
    }

    [Fact]
    public async Task ReplayingWorkspaceCreationReturnsSameWorkspaceAndCookie()
    {
        using var client = CreateClient();
        var (csrfToken, csrfCookie) = await GetCsrfTokenAsync(client);
        var create = NewWorkspaceRequest();

        using var first = await CreateWorkspaceAsync(client, create, csrfToken, csrfCookie);
        using var replay = await CreateWorkspaceAsync(client, create, csrfToken, csrfCookie);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var firstWorkspace = await first.Content.ReadFromJsonAsync<WorkspaceResponse>();
        var replayedWorkspace = await replay.Content.ReadFromJsonAsync<WorkspaceResponse>();
        Assert.NotNull(firstWorkspace);
        Assert.NotNull(replayedWorkspace);
        Assert.Equal(firstWorkspace.WorkspaceId, replayedWorkspace.WorkspaceId);
        Assert.Equal(GetCookieAttribute(first, "expires"), GetCookieAttribute(replay, "expires"));

        using var changedRequest = await CreateWorkspaceAsync(
            client,
            create with { TimeZoneId = "Europe/Paris" },
            csrfToken,
            csrfCookie);
        Assert.Equal(HttpStatusCode.Conflict, changedRequest.StatusCode);
    }

    [Fact]
    public async Task TaskCreateIsIdempotentAndCannotBeReadWithoutDeviceCookie()
    {
        using var client = CreateClient();
        var (csrfToken, csrfCookie) = await GetCsrfTokenAsync(client);
        var create = NewWorkspaceRequest();
        using var workspaceResponse = await CreateWorkspaceAsync(client, create, csrfToken, csrfCookie);
        Assert.Equal(HttpStatusCode.Created, workspaceResponse.StatusCode);
        var deviceCookie = GetDeviceCookieValue(workspaceResponse);
        var firstWorkspace = await workspaceResponse.Content.ReadFromJsonAsync<WorkspaceResponse>();
        Assert.NotNull(firstWorkspace);

        var task = new TaskRequest("Retry me", null, null, "Low");
        var key = NewKey();
        using var first = await CreateTaskAsync(client, csrfToken, csrfCookie, deviceCookie, task, key);
        using var replay = await CreateTaskAsync(client, csrfToken, csrfCookie, deviceCookie, task, key);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var firstTask = await first.Content.ReadFromJsonAsync<TaskResponse>();
        var replayedTask = await replay.Content.ReadFromJsonAsync<TaskResponse>();
        Assert.NotNull(firstTask);
        Assert.NotNull(replayedTask);
        Assert.Equal(firstTask.Id, replayedTask.Id);

        using var changedTask = await CreateTaskAsync(
            client,
            csrfToken,
            csrfCookie,
            deviceCookie,
            task with { Title = "Different task" },
            key);
        Assert.Equal(HttpStatusCode.Conflict, changedTask.StatusCode);

        using var anonymousClient = CreateClient();
        using var anonymousResponse = await anonymousClient.GetAsync("/api/tasks");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        using var otherClient = CreateClient();
        var (otherCsrfToken, otherCsrfCookie) = await GetCsrfTokenAsync(otherClient);
        var otherWorkspace = NewWorkspaceRequest();
        using var otherWorkspaceResponse = await CreateWorkspaceAsync(
            otherClient,
            otherWorkspace,
            otherCsrfToken,
            otherCsrfCookie);
        var otherDeviceCookie = GetDeviceCookieValue(otherWorkspaceResponse);
        using var crossWorkspaceRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/tasks?workspaceId={firstWorkspace.WorkspaceId}");
        crossWorkspaceRequest.Headers.Add("Cookie", $"__Host-device={otherDeviceCookie}");
        using var crossWorkspaceResponse = await otherClient.SendAsync(crossWorkspaceRequest);
        Assert.Equal(HttpStatusCode.OK, crossWorkspaceResponse.StatusCode);
        var otherTasks = await crossWorkspaceResponse.Content.ReadFromJsonAsync<TaskResponse[]>();
        Assert.NotNull(otherTasks);
        Assert.Empty(otherTasks);
    }

    [Fact]
    public async Task RejectsMutationWithAnUntrustedOrigin()
    {
        using var client = CreateClient();
        var csrfToken = await GetCsrfTokenAsync(client);
        var create = NewWorkspaceRequest();
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/workspaces")
        {
            Content = JsonContent.Create(new WorkspacePayload(create.TimeZoneId, create.DeviceCredential, create.RecoverySecret))
        };
        message.Headers.Add("Origin", "https://evil.example");
        message.Headers.Add("X-CSRF-Token", csrfToken.Token);
        message.Headers.Add("Cookie", $"__Host-csrf={csrfToken.Cookie}");
        message.Headers.Add("Idempotency-Key", create.IdempotencyKey);

        using var response = await client.SendAsync(message);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = false
    });

    private static async Task<(string Token, string Cookie)> GetCsrfTokenAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/security/csrf");
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<CsrfResponse>();
        Assert.NotNull(payload);
        var cookie = response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("__Host-csrf=", StringComparison.Ordinal));
        Assert.Contains("Secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SameSite=Strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Path=/", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Domain=", cookie, StringComparison.OrdinalIgnoreCase);
        return (payload.Token, cookie.Split(';', 2)[0].Split('=', 2)[1]);
    }

    private static async Task<HttpResponseMessage> CreateWorkspaceAsync(
        HttpClient client,
        WorkspaceRequest request,
        string csrfToken,
        string csrfCookie)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/workspaces")
        {
            Content = JsonContent.Create(new WorkspacePayload(request.TimeZoneId, request.DeviceCredential, request.RecoverySecret))
        };
        message.Headers.Add("Origin", "https://localhost");
        message.Headers.Add("X-CSRF-Token", csrfToken);
        message.Headers.Add("Cookie", $"__Host-csrf={csrfCookie}");
        message.Headers.Add("Idempotency-Key", request.IdempotencyKey);
        return await client.SendAsync(message);
    }

    private static async Task<HttpResponseMessage> CreateTaskAsync(
        HttpClient client,
        string csrfToken,
        string csrfCookie,
        string deviceCookie,
        TaskRequest request,
        string idempotencyKey)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/tasks")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("Origin", "https://localhost");
        message.Headers.Add("X-CSRF-Token", csrfToken);
        message.Headers.Add("Cookie", $"__Host-csrf={csrfCookie}; __Host-device={deviceCookie}");
        message.Headers.Add("Idempotency-Key", idempotencyKey);
        return await client.SendAsync(message);
    }

    private static WorkspaceRequest NewWorkspaceRequest() => new(
        "America/Los_Angeles",
        NewKey(),
        NewKey(),
        NewKey());

    private static string NewKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    private static void AssertDeviceCookie(HttpResponseMessage response)
    {
        var cookie = response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("__Host-device=", StringComparison.Ordinal));
        Assert.Contains("Secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HttpOnly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SameSite=Lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Path=/", cookie, StringComparison.OrdinalIgnoreCase);
        var maxAge = int.Parse(GetCookieAttribute(response, "max-age"));
        Assert.InRange(maxAge, 7775990, 7776000);
        Assert.DoesNotContain("Domain=", cookie, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetDeviceCookieValue(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("__Host-device=", StringComparison.Ordinal))
            .Split(';', 2)[0]
            .Split('=', 2)[1];

    private static string GetCookieAttribute(HttpResponseMessage response, string name)
    {
        var cookie = response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("__Host-device=", StringComparison.Ordinal));
        return cookie.Split(';')
            .Select(attribute => attribute.Trim())
            .Single(attribute => attribute.StartsWith($"{name}=", StringComparison.OrdinalIgnoreCase))
            .Split('=', 2)[1];
    }

    private static async Task AssertPersistedInitializationAsync(Guid workspaceId, WorkspaceRequest request)
    {
        var connectionString = Environment.GetEnvironmentVariable("TASK_MANAGER_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set TASK_MANAGER_TEST_CONNECTION to an isolated PostgreSQL test database.");
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT w."TimeZoneId", l."Name", d."Verifier", r."Verifier",
                   d."ExpiresAt" - d."CreatedAt",
                   (SELECT count(*) FROM "Workspaces" WHERE "Id" = @workspaceId),
                   (SELECT count(*) FROM "TaskLists" WHERE "WorkspaceId" = @workspaceId),
                   (SELECT count(*) FROM "DeviceCredentials" WHERE "WorkspaceId" = @workspaceId),
                   (SELECT count(*) FROM "RecoveryCredentials" WHERE "WorkspaceId" = @workspaceId),
                   (SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'Accounts')
            FROM "Workspaces" w
            JOIN "TaskLists" l ON l."WorkspaceId" = w."Id" AND l."Name" = 'Tasks'
            JOIN "DeviceCredentials" d ON d."WorkspaceId" = w."Id"
            JOIN "RecoveryCredentials" r ON r."WorkspaceId" = w."Id"
            WHERE w."Id" = @workspaceId
            """;
        command.Parameters.AddWithValue("workspaceId", workspaceId);

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(request.TimeZoneId, reader.GetString(0));
        Assert.Equal("Tasks", reader.GetString(1));
        Assert.Equal(HashToken(request.DeviceCredential), reader.GetString(2));
        Assert.Equal(HashToken(request.RecoverySecret), reader.GetString(3));
        Assert.Equal(TimeSpan.FromDays(90), reader.GetFieldValue<TimeSpan>(4));
        Assert.Equal(1L, reader.GetInt64(5));
        Assert.Equal(1L, reader.GetInt64(6));
        Assert.Equal(1L, reader.GetInt64(7));
        Assert.Equal(1L, reader.GetInt64(8));
        Assert.Equal(0L, reader.GetInt64(9));
    }

    private static string HashToken(string token)
    {
        var bytes = Convert.FromBase64String(token.Replace('-', '+').Replace('_', '/') + "=");
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private sealed record CsrfResponse(string Token);
    private sealed record WorkspaceRequest(string TimeZoneId, string DeviceCredential, string RecoverySecret, string IdempotencyKey);
    private sealed record WorkspacePayload(string TimeZoneId, string DeviceCredential, string RecoverySecret);
    private sealed record WorkspaceResponse(Guid WorkspaceId, string TimeZoneId, string DefaultListName);
    private sealed record TaskRequest(string Title, string? Description, DateOnly? DueDate, string? Priority);
    private sealed record TaskResponse(Guid Id, string Title, string? Description, DateOnly? DueDate, string? Priority, string ListName);
}

public sealed class Slice1ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connectionString = Environment.GetEnvironmentVariable("TASK_MANAGER_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set TASK_MANAGER_TEST_CONNECTION to an isolated PostgreSQL test database.");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:TaskManager"] = connectionString
            });
        });
    }
}

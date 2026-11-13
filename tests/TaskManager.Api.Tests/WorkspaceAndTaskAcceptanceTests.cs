using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using TaskManager.Api.Tests.TestInfrastructure;

namespace TaskManager.Api.Tests;

public sealed class WorkspaceAndTaskAcceptanceTests : IDisposable
{
    private readonly ApiApplicationFactory factory = new();

    [Fact]
    public async Task WorkspaceCreationPersistsDefaultListTimeZoneAndVerifiers()
    {
        await using var session = await TestSession.CreateAsync(factory);
        var deviceToken = TestSession.NewToken();
        var recoverySecret = TestSession.NewToken();
        var response = await session.CreateWorkspaceAsync(
            "America/Los_Angeles",
            deviceToken: deviceToken,
            recoverySecret: recoverySecret);

        Assert.True(response.StatusCode == HttpStatusCode.Created, "Workspace initialization should be confirmed.");
        var cookie = response.Headers.GetValues("Set-Cookie")
            .First(value => value.StartsWith("__Host-device=", StringComparison.Ordinal));
        var cookieFlags = cookie.Split(';').Skip(1).Select(value => value.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var safeCookiePolicy = cookieFlags.Contains("Secure") &&
            cookieFlags.Contains("HttpOnly") &&
            cookieFlags.Contains("Path=/") &&
            cookieFlags.Contains("SameSite=Lax") &&
            !cookieFlags.Any(value => value.StartsWith("Domain=", StringComparison.OrdinalIgnoreCase));
        Assert.True(safeCookiePolicy, "The device cookie must use the approved host-only Secure policy.");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var workspaceId = document.RootElement.GetProperty("workspaceId").GetGuid();
        var body = await response.Content.ReadAsStringAsync();
        var rawCredentialLeaked = body.Contains(deviceToken, StringComparison.Ordinal) ||
            body.Contains(recoverySecret, StringComparison.Ordinal);
        Assert.False(rawCredentialLeaked, "Workspace responses must not return raw credential material.");

        var record = await ReadWorkspaceAsync(workspaceId);
        Assert.Equal("America/Los_Angeles", record.TimeZone);
        Assert.Equal("Tasks", record.ListName);
        Assert.True(CryptographicOperations.FixedTimeEquals(
            record.DeviceVerifier,
            HashToken(deviceToken)), "The device verifier must match the submitted high-entropy credential.");
        Assert.True(CryptographicOperations.FixedTimeEquals(
            record.RecoveryVerifier,
            HashToken(recoverySecret)), "The recovery verifier must match the submitted high-entropy secret.");
        Assert.False(record.WorkspaceColumns.Any(column => column.Contains("account", StringComparison.OrdinalIgnoreCase)),
            "Workspace initialization must not create account identity fields.");
    }

    [Fact]
    public async Task WorkspaceCreationRejectsUnselectedOrUnsupportedTimeZones()
    {
        await using var session = await TestSession.CreateAsync(factory);
        var before = await ReadScalarAsync<long>("SELECT count(*) FROM workspaces");

        using var missing = await session.CreateWorkspaceAsync(null);
        using var unsupported = await session.CreateWorkspaceAsync("Mars/Olympus");

        Assert.True(missing.StatusCode == HttpStatusCode.BadRequest, "A missing zone must require explicit selection.");
        Assert.True(unsupported.StatusCode == HttpStatusCode.BadRequest, "An unsupported zone must be rejected.");
        Assert.Equal(before, await ReadScalarAsync<long>("SELECT count(*) FROM workspaces"));
    }

    [Fact]
    public async Task TaskCreationPersistsOptionalValuesAndReadsOnlyServerConfirmedTasks()
    {
        await using var session = await CreateAuthorizedSessionAsync();
        var workspaceId = await ReadWorkspaceIdAsync(session);
        var response = await session.CreateTaskAsync(new
        {
            title = "Review design",
            description = "Keep the original whitespace.",
            dueDate = "2026-12-05",
            priority = "High"
        });

        Assert.True(response.StatusCode == HttpStatusCode.Created, "A valid task should be persisted.");
        using var tasksResponse = await session.GetTasksAsync();
        Assert.True(tasksResponse.StatusCode == HttpStatusCode.OK, "An authorized task read should succeed.");
        using var tasks = JsonDocument.Parse(await tasksResponse.Content.ReadAsStringAsync());
        var task = tasks.RootElement.EnumerateArray().Single();
        Assert.Equal("Review design", task.GetProperty("title").GetString());
        Assert.Equal("Keep the original whitespace.", task.GetProperty("description").GetString());
        Assert.Equal("2026-12-05", task.GetProperty("dueDate").GetString());
        Assert.Equal("High", task.GetProperty("priority").GetString());

        var persisted = await ReadTaskAsync(workspaceId);
        Assert.Equal("Tasks", persisted.ListName);
        Assert.Equal(new DateOnly(2026, 12, 5), persisted.DueDate);
    }

    [Fact]
    public async Task MissingEmptyAndWhitespaceTitlesDoNotPersistTasks()
    {
        await using var session = await CreateAuthorizedSessionAsync();
        var workspaceId = await ReadWorkspaceIdAsync(session);
        var before = await ReadTaskCountAsync(workspaceId);

        using var missing = await session.CreateTaskAsync(new { description = "No title" });
        using var empty = await session.CreateTaskAsync(new { title = "" });
        using var blank = await session.CreateTaskAsync(new { title = " \t " });

        Assert.True(missing.StatusCode == HttpStatusCode.BadRequest, "A missing title must be rejected.");
        Assert.True(empty.StatusCode == HttpStatusCode.BadRequest, "An empty title must be rejected.");
        Assert.True(blank.StatusCode == HttpStatusCode.BadRequest, "A whitespace-only title must be rejected.");
        Assert.Equal(before, await ReadTaskCountAsync(workspaceId));
    }

    [Fact]
    public async Task WorkspaceCredentialScopesTaskReadsAndRejectsMissingCredentials()
    {
        await using var first = await CreateAuthorizedSessionAsync();
        await using var second = await CreateAuthorizedSessionAsync();
        var firstWorkspace = await ReadWorkspaceIdAsync(first);
        var secondWorkspace = await ReadWorkspaceIdAsync(second);
        using var created = await first.CreateTaskAsync(new { title = "Private task" });
        Assert.True(created.StatusCode == HttpStatusCode.Created, "The first workspace task should be created.");

        using var firstRead = await first.GetTasksAsync();
        using var secondRead = await second.GetTasksAsync();
        var firstCount = JsonDocument.Parse(await firstRead.Content.ReadAsStringAsync()).RootElement.GetArrayLength();
        var secondCount = JsonDocument.Parse(await secondRead.Content.ReadAsStringAsync()).RootElement.GetArrayLength();
        Assert.Equal(1, firstCount);
        Assert.Equal(0, secondCount);
        Assert.NotEqual(firstWorkspace, secondWorkspace);

        using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            BaseAddress = new Uri("https://localhost")
        });
        using var anonymousRead = await anonymous.GetAsync("/api/tasks");
        Assert.True(anonymousRead.StatusCode == HttpStatusCode.Unauthorized,
            "Task reads without a valid device credential must be unauthorized.");
    }

    [Fact]
    public async Task WorkspaceCreateRetryIsIdempotentAndRejectsChangedPayload()
    {
        await using var session = await TestSession.CreateAsync(factory);
        var key = TestSession.NewToken();
        var device = TestSession.NewToken();
        var recovery = TestSession.NewToken();

        using var first = await session.CreateWorkspaceAsync("America/Los_Angeles", key, device, recovery);
        using var replay = await session.CreateWorkspaceAsync("America/Los_Angeles", key, device, recovery);
        using var changed = await session.CreateWorkspaceAsync("Europe/Paris", key, device, recovery);

        Assert.True(first.StatusCode == HttpStatusCode.Created,
            $"The first workspace attempt returned HTTP {(int)first.StatusCode}; fields: {await SafeErrorFieldsAsync(first)}.");
        Assert.True(replay.StatusCode == HttpStatusCode.Created, "The same workspace operation should return its original result.");
        Assert.True(changed.StatusCode == HttpStatusCode.Conflict,
            "A changed payload must not reuse a workspace operation key.");

        var ids = await ReadScalarAsync<long>(
            "SELECT count(*) FROM workspace_create_operations WHERE key_digest = @digest",
            new NpgsqlParameter("digest", HashToken(key)));
        Assert.Equal(1, ids);
    }

    [Fact]
    public async Task LostCreateResponseRetriesImmutableOperationExactlyOnce()
    {
        await using var session = await CreateAuthorizedSessionAsync();
        var workspaceId = await ReadWorkspaceIdAsync(session);
        var key = TestSession.NewToken();
        var payload = new { title = "Submitted task", description = "Original payload" };

        using (var lostResponse = await session.CreateTaskAsync(payload, key))
        {
            Assert.True(lostResponse.StatusCode == HttpStatusCode.Created,
                "The server should commit the first operation before its response is discarded.");
        }

        using var replay = await session.CreateTaskAsync(payload, key);
        using var changed = await session.CreateTaskAsync(new { title = "Edited task" }, key);

        Assert.True(replay.IsSuccessStatusCode, "The same immutable operation should return its original result.");
        Assert.True(changed.StatusCode == HttpStatusCode.Conflict,
            "An edited payload must not reuse the original operation key.");
        Assert.Equal(1, await ReadTaskCountAsync(workspaceId));

        using var distinct = await session.CreateTaskAsync(new { title = "Edited task" }, TestSession.NewToken());
        Assert.True(distinct.StatusCode == HttpStatusCode.Created,
            "A later intentional edit must use a distinct operation key.");
        Assert.Equal(2, await ReadTaskCountAsync(workspaceId));
    }

    [Fact]
    public async Task TaskMutationRequiresOriginAndCsrfToken()
    {
        await using var session = await CreateAuthorizedSessionAsync();
        using var missingCsrf = await session.CreateTaskAsync(new { title = "Blocked write" }, includeCsrf: false);
        Assert.True(missingCsrf.StatusCode == HttpStatusCode.Forbidden,
            "A task write without a CSRF cookie and header must be rejected.");
        Assert.Equal(0, await ReadTaskCountAsync(await ReadWorkspaceIdAsync(session)));
    }

    private async Task<TestSession> CreateAuthorizedSessionAsync()
    {
        var session = await TestSession.CreateAsync(factory);
        using var response = await session.CreateWorkspaceAsync("America/Los_Angeles");
        Assert.True(response.StatusCode == HttpStatusCode.Created,
            $"Test workspace setup returned HTTP {(int)response.StatusCode}; fields: {await SafeErrorFieldsAsync(response)}.");
        return session;
    }

    private static async Task<string> SafeErrorFieldsAsync(HttpResponseMessage response)
    {
        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (document.RootElement.TryGetProperty("errors", out var errors))
            {
                return string.Join(",", errors.EnumerateObject().Select(error => error.Name));
            }

            if (document.RootElement.TryGetProperty("code", out var code))
            {
                return code.GetString() ?? "unknown";
            }
        }
        catch (JsonException)
        {
        }

        return "unspecified";
    }

    private static async Task<Guid> ReadWorkspaceIdAsync(TestSession session)
    {
        using var response = await session.GetWorkspaceAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, "The authorized workspace should be readable.");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<(string TimeZone, string ListName, byte[] DeviceVerifier, byte[] RecoveryVerifier, string[] WorkspaceColumns)>
        ReadWorkspaceAsync(Guid workspaceId)
    {
        await using var connection = new NpgsqlConnection(TestDatabase.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT w.time_zone, l.name, d.verifier, r.verifier
            FROM workspaces w
            JOIN task_lists l ON l.workspace_id = w.id
            JOIN device_credentials d ON d.workspace_id = w.id
            JOIN recovery_credentials r ON r.workspace_id = w.id
            WHERE w.id = @workspace_id
            """,
            connection);
        command.Parameters.AddWithValue("workspace_id", workspaceId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "Workspace initialization should persist all required records.");
        var record = (
            reader.GetString(0),
            reader.GetString(1),
            reader.GetFieldValue<byte[]>(2),
            reader.GetFieldValue<byte[]>(3));
        await reader.DisposeAsync();

        await using var columnsCommand = new NpgsqlCommand(
            "SELECT column_name FROM information_schema.columns WHERE table_name = 'workspaces'",
            connection);
        await using var columnsReader = await columnsCommand.ExecuteReaderAsync();
        var columns = new List<string>();
        while (await columnsReader.ReadAsync())
        {
            columns.Add(columnsReader.GetString(0));
        }

        return (record.Item1, record.Item2, record.Item3, record.Item4, columns.ToArray());
    }

    private static async Task<(string ListName, DateOnly? DueDate)> ReadTaskAsync(Guid workspaceId)
    {
        await using var connection = new NpgsqlConnection(TestDatabase.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT l.name, t.due_date
            FROM tasks t
            JOIN task_lists l ON l.id = t.list_id AND l.workspace_id = t.workspace_id
            WHERE t.workspace_id = @workspace_id
            """,
            connection);
        command.Parameters.AddWithValue("workspace_id", workspaceId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "A confirmed task should be present in PostgreSQL.");
        return (reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetFieldValue<DateOnly>(1));
    }

    private static async Task<long> ReadTaskCountAsync(Guid workspaceId) =>
        await ReadScalarAsync<long>(
            "SELECT count(*) FROM tasks WHERE workspace_id = @workspace_id",
            new NpgsqlParameter("workspace_id", workspaceId));

    private static async Task<T> ReadScalarAsync<T>(string sql, params NpgsqlParameter[] parameters)
    {
        await using var connection = new NpgsqlConnection(TestDatabase.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    private static byte[] HashToken(string token)
    {
        var value = token.Replace('-', '+').Replace('_', '/');
        value = value.PadRight(value.Length + ((4 - value.Length % 4) % 4), '=');
        return SHA256.HashData(Convert.FromBase64String(value));
    }

    public void Dispose() => factory.Dispose();
}

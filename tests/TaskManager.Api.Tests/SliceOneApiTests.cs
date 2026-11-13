using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskManager.Api.Data;
using TaskManager.Api.Features.Tasks;
using TaskManager.Api.Features.Workspace;

namespace TaskManager.Api.Tests;

[CollectionDefinition("API integration", DisableParallelization = true)]
public sealed class ApiTestCollection
{
}

[Collection("API integration")]
public sealed class SliceOneApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task WorkspaceCreationIsAtomicIdempotentAndStoresOnlyVerifiers()
    {
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();
        var csrf = await AcquireCsrfAsync(client);
        var request = CreateWorkspaceRequest("America/Los_Angeles");

        var firstResponse = await PostWorkspaceAsync(client, csrf, request);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        var cookie = ReadCookie(firstResponse, "__Host-device");
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);

        var secondResponse = await PostWorkspaceAsync(client, csrf, request);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        Assert.Equal(cookie.Split(';')[0], ReadCookie(secondResponse, "__Host-device").Split(';')[0]);

        var result = await secondResponse.Content.ReadFromJsonAsync<WorkspaceCreatedResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal("America/Los_Angeles", result.TimeZoneId);
        Assert.Equal("Tasks", result.DefaultListName);

        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<TaskManagerDbContext>();
        var workspace = await database.Workspaces.SingleAsync(item => item.Id == result.WorkspaceId);
        Assert.Equal("America/Los_Angeles", workspace.TimeZoneId);
        Assert.Single(await database.TaskLists.Where(item => item.WorkspaceId == workspace.Id).ToListAsync());
        Assert.Single(await database.DeviceCredentials.Where(item => item.WorkspaceId == workspace.Id).ToListAsync());
        Assert.Single(await database.RecoveryCredentials.Where(item => item.WorkspaceId == workspace.Id).ToListAsync());
        var device = await database.DeviceCredentials.SingleAsync(item => item.WorkspaceId == workspace.Id);
        var recovery = await database.RecoveryCredentials.SingleAsync(item => item.WorkspaceId == workspace.Id);
        Assert.Equal(Hash(request.DeviceCredential!), device.Verifier);
        Assert.Equal(Hash(request.RecoverySecret!), recovery.Verifier);
        Assert.DoesNotContain(request.DeviceCredential!, device.Verifier);
        Assert.DoesNotContain(request.RecoverySecret!, recovery.Verifier);
        Assert.Single(await database.WorkspaceCreationOperations.Where(item => item.WorkspaceId == workspace.Id).ToListAsync());
    }

    [Fact]
    public async Task ReusingWorkspaceOperationKeyWithDifferentRequestConflicts()
    {
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();
        var csrf = await AcquireCsrfAsync(client);
        var request = CreateWorkspaceRequest("Etc/UTC");

        Assert.Equal(HttpStatusCode.Created, (await PostWorkspaceAsync(client, csrf, request)).StatusCode);
        var changed = request with { TimeZoneId = "Europe/London" };
        var response = await PostWorkspaceAsync(client, csrf, changed);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task TaskCreateValidatesTitlePersistsDateAndDeduplicatesByOperationKey()
    {
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();
        var csrf = await AcquireCsrfAsync(client);
        var workspace = await CreateWorkspaceAsync(client, csrf);
        var create = new TaskCreateRequest("First task", "Description stays exact", new DateOnly(2026, 11, 13), "High");
        var key = RandomToken();

        var response = await PostTaskAsync(client, csrf, workspace.DeviceCredential, key, create);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<TaskResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal(create.Title, created.Title);
        Assert.Equal(create.Description, created.Description);
        Assert.Equal(create.DueDate, created.DueDate);
        Assert.Equal(create.Priority, created.Priority);
        Assert.Equal("Tasks", created.ListName);

        var retry = await PostTaskAsync(client, csrf, workspace.DeviceCredential, key, create);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        var repeated = await retry.Content.ReadFromJsonAsync<TaskResponse>(JsonOptions);
        Assert.Equal(created.Id, repeated?.Id);

        var changed = create with { Title = "Different task" };
        Assert.Equal(HttpStatusCode.Conflict, (await PostTaskAsync(client, csrf, workspace.DeviceCredential, key, changed)).StatusCode);

        var read = await GetTasksAsync(client, workspace.DeviceCredential);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var tasks = await read.Content.ReadFromJsonAsync<TaskResponse[]>(JsonOptions);
        Assert.NotNull(tasks);
        Assert.Single(tasks);
        Assert.Equal(created.Id, tasks[0].Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankTaskTitlesAreRejected(string? title)
    {
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();
        var csrf = await AcquireCsrfAsync(client);
        var workspace = await CreateWorkspaceAsync(client, csrf);
        var request = new TaskCreateRequest(title, null, null, null);

        var response = await PostTaskAsync(client, csrf, workspace.DeviceCredential, RandomToken(), request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await ReadTasksAsync(client, workspace.DeviceCredential));
        Assert.Contains("title", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TaskReadsRequireTheOwningDevice()
    {
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();
        var csrf = await AcquireCsrfAsync(client);
        var first = await CreateWorkspaceAsync(client, csrf);
        var second = await CreateWorkspaceAsync(client, csrf);
        var request = new TaskCreateRequest("Private task", null, null, null);

        Assert.Equal(HttpStatusCode.Created,
            (await PostTaskAsync(client, csrf, first.DeviceCredential, RandomToken(), request)).StatusCode);
        Assert.Empty(await ReadTasksAsync(client, second.DeviceCredential));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/tasks")).StatusCode);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task UnexpectedFailureIsSanitizedInEveryEnvironment(string environment)
    {
        await using var factory = new ApiTestFactory(environment);
        using var client = factory.CreateClient();
        factory.FailureSwitch.Enabled = true;
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/tasks");
        request.Headers.Add("X-Sensitive-Test-Header", "request-header-sensitive-marker");
        request.Headers.Add("Cookie", $"__Host-device={RandomToken()}");
        request.Headers.Add("Origin", "http://localhost");

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("An unexpected error occurred.", body);
        Assert.DoesNotContain("request-header-sensitive-marker", body);
        Assert.DoesNotContain("db-failure-sensitive-marker", body);
        Assert.DoesNotContain("stack trace", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("X-Sensitive-Test-Header", body);
    }

    [Fact]
    public async Task InvalidOriginsAndMissingCsrfAreRejectedBeforeMutation()
    {
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();
        var request = CreateWorkspaceRequest("Etc/UTC");
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<TaskManagerDbContext>();
        var countBefore = await database.Workspaces.CountAsync();
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/workspaces")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        message.Headers.Add("Origin", "https://attacker.invalid");

        var response = await client.SendAsync(message);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(countBefore, await database.Workspaces.CountAsync());
    }

    [Fact]
    public async Task SupportedZonesIncludeTheSelectedIanaZone()
    {
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/time-zones");
        var zones = await response.Content.ReadFromJsonAsync<string[]>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(zones);
        Assert.Contains("America/Los_Angeles", zones);
        Assert.Contains("Etc/UTC", zones);
    }

    private static async Task<CsrfState> AcquireCsrfAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/security/csrf");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<CsrfResponse>(JsonOptions);
        Assert.NotNull(body);
        return new CsrfState(body.Token, ReadCookie(response, "__Host-csrf").Split(';')[0]);
    }

    private static async Task<HttpResponseMessage> PostWorkspaceAsync(
        HttpClient client,
        CsrfState csrf,
        WorkspaceCreateRequest request)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/workspaces")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        AddMutationHeaders(message, csrf);
        return await client.SendAsync(message);
    }

    private static async Task<HttpResponseMessage> PostTaskAsync(
        HttpClient client,
        CsrfState csrf,
        string deviceCredential,
        string idempotencyKey,
        TaskCreateRequest request)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/tasks")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        AddMutationHeaders(message, csrf);
        message.Headers.Add("Cookie", $"__Host-device={deviceCredential}; {csrf.Cookie}");
        message.Headers.Add("Idempotency-Key", idempotencyKey);
        return await client.SendAsync(message);
    }

    private static async Task<HttpResponseMessage> GetTasksAsync(HttpClient client, string deviceCredential)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, "/api/tasks");
        message.Headers.Add("Cookie", $"__Host-device={deviceCredential}");
        return await client.SendAsync(message);
    }

    private static async Task<TaskResponse[]> ReadTasksAsync(HttpClient client, string deviceCredential)
    {
        var response = await GetTasksAsync(client, deviceCredential);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TaskResponse[]>(JsonOptions))!;
    }

    private static void AddMutationHeaders(HttpRequestMessage message, CsrfState csrf)
    {
        message.Headers.Add("Origin", "http://localhost");
        message.Headers.Add("X-CSRF-Token", csrf.Token);
        message.Headers.Add("Cookie", csrf.Cookie);
    }

    private static async Task<WorkspaceCredentials> CreateWorkspaceAsync(HttpClient client, CsrfState csrf)
    {
        var request = CreateWorkspaceRequest("Etc/UTC");
        var response = await PostWorkspaceAsync(client, csrf, request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return new WorkspaceCredentials(request.DeviceCredential!, request.RecoverySecret!);
    }

    private static WorkspaceCreateRequest CreateWorkspaceRequest(string timeZoneId) =>
        new(timeZoneId, RandomToken(), RandomToken(), RandomToken());

    private static string RandomToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string ReadCookie(HttpResponseMessage response, string name) =>
        response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith(name + "=", StringComparison.Ordinal));

    private sealed record CsrfState(string Token, string Cookie);

    private sealed record WorkspaceCredentials(string DeviceCredential, string RecoverySecret);
}

using System.Diagnostics;
using System.Net;
using System.Reflection;
using TaskManager.Api.Tests.TestInfrastructure;

namespace TaskManager.Api.Tests;

public sealed class FailureAndSecurityRegressionTests
{
    private const string DiagnosticSentinel = "SYNTHETIC-CREDENTIAL-DIAGNOSTIC-SENTINEL";

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task UnexpectedFailureIsSanitizedInEveryEnvironment(string environment)
    {
        using var factory = new ApiApplicationFactory(environment, failTaskReads: true);
        await using var session = await TestSession.CreateAsync(factory);
        using var workspace = await session.CreateWorkspaceAsync("America/Los_Angeles");
        Assert.True(workspace.StatusCode == HttpStatusCode.Created,
            "The database-failure test workspace should be initialized.");

        using var response = await session.GetTasksWithDiagnosticHeaderAsync(DiagnosticSentinel);
        var body = await response.Content.ReadAsStringAsync();
        var logs = string.Join(Environment.NewLine, factory.LogMessages);
        var genericProblem = response.StatusCode == HttpStatusCode.InternalServerError &&
            body.Contains("Unexpected service failure.", StringComparison.Ordinal) &&
            body.Contains("\"status\":500", StringComparison.Ordinal);
        var noSensitiveResponse = !body.Contains(DiagnosticSentinel, StringComparison.Ordinal) &&
            !body.Contains("NpgsqlException", StringComparison.Ordinal) &&
            !body.Contains(" at ", StringComparison.Ordinal);
        var noSensitiveLogs = !logs.Contains(DiagnosticSentinel, StringComparison.Ordinal) &&
            (session.DeviceTokenForTesting is null ||
                !logs.Contains(session.DeviceTokenForTesting, StringComparison.Ordinal));

        Assert.True(genericProblem && noSensitiveResponse && noSensitiveLogs,
            "Unexpected database errors must be generic and must not log credentials or request headers.");
    }

    [Fact]
    public async Task AssertionFailuresDoNotExposeCredentialArguments()
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("vstest");
        startInfo.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        startInfo.ArgumentList.Add("--TestCaseFilter:FullyQualifiedName~CookieAssertionDiagnosticFixture");
        startInfo.Environment["TASK_MANAGER_COOKIE_DIAGNOSTIC_FIXTURE"] = "1";

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var diagnostics = string.Concat(await stdout, await stderr);
        var expectedFailure = process.ExitCode != 0;
        var sentinelWasRedacted = !diagnostics.Contains(DiagnosticSentinel, StringComparison.Ordinal);

        Assert.True(expectedFailure && sentinelWasRedacted,
            "The synthetic xUnit cookie assertion must fail without exposing its credential sentinel.");
    }

    [Fact]
    public void CookieAssertionDiagnosticFixture()
    {
        if (Environment.GetEnvironmentVariable("TASK_MANAGER_COOKIE_DIAGNOSTIC_FIXTURE") != "1")
        {
            return;
        }

        var cookie = $"__Host-device={DiagnosticSentinel}; HttpOnly; Path=/";
        var isSecure = cookie.Split(';').Select(value => value.Trim()).Contains("Secure", StringComparer.OrdinalIgnoreCase);
        Assert.True(isSecure, "The device cookie must include Secure.");
    }
}

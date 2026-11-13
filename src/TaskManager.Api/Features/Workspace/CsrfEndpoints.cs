using System.Security.Cryptography;
using TaskManager.Api.Infrastructure;

namespace TaskManager.Api.Features.Workspace;

public static class CsrfEndpoints
{
    public static IResult Issue(HttpContext context)
    {
        var token = CredentialDigests.NewToken();
        context.Response.Cookies.Append(
            RequestSecurity.CsrfCookieName,
            token,
            new CookieOptions
            {
                Path = "/",
                Secure = true,
                HttpOnly = false,
                SameSite = SameSiteMode.Strict,
                MaxAge = TimeSpan.FromHours(8),
                IsEssential = true
            });
        return Results.Ok(new CsrfResponse(token));
    }
}

namespace TaskManager.Api.Features.Workspace;

public static class TimeZoneEndpoints
{
    public static IResult GetSupportedTimeZones() =>
        Results.Ok(TimeZoneInfo.GetSystemTimeZones()
            .Select(timeZone => timeZone.Id)
            .Order(StringComparer.Ordinal)
            .ToArray());

    public static bool IsSupported(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return false;
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}

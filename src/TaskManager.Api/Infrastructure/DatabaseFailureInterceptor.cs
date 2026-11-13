using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace TaskManager.Api.Infrastructure;

public sealed class DatabaseFailureInterceptor(DatabaseFailureSwitch failureSwitch)
    : DbCommandInterceptor
{
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        ThrowIfEnabled();
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        ThrowIfEnabled();
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        ThrowIfEnabled();
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    private void ThrowIfEnabled()
    {
        if (failureSwitch.Enabled)
        {
            throw new InvalidOperationException("request-time database failure with a sensitive diagnostic");
        }
    }
}

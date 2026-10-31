using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BloodBankLIS.Api.ErrorHandling;

/// <summary>
/// Turns a save that lost a race with another user (stale RowVersion or duplicate unique
/// value) into a 409 ProblemDetails the UI shows as a refresh-and-retry message, instead
/// of an unhandled 500. Other exceptions fall through to the next handler.
/// </summary>
public sealed class ConcurrencyConflictExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ConcurrencyConflictExceptionHandler> _logger;

    public ConcurrencyConflictExceptionHandler(ILogger<ConcurrencyConflictExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (!DatabaseConflict.TryDescribe(exception, out var message))
        {
            return false;
        }

        _logger.LogWarning(exception, "Concurrent update conflict on {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Changed by another user",
                Detail = message,
                Extensions = { ["traceId"] = httpContext.TraceIdentifier }
            },
            options: null,
            contentType: "application/problem+json",
            cancellationToken);
        return true;
    }
}

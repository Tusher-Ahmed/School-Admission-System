using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace SchoolAdmission.Api.Infrastructure;

/// <summary>
/// Every error response has the same JSON shape: { "error": "CODE", "message": "human readable text" }.
/// The UI shows "message" directly to the user.
/// </summary>
public static class ApiErrors
{
    public static IResult Error(int statusCode, string code, string message) =>
        Results.Json(new { error = code, message }, statusCode: statusCode);

    public static IResult BadRequest(string code, string message) => Error(StatusCodes.Status400BadRequest, code, message);
    public static IResult Forbidden(string code, string message) => Error(StatusCodes.Status403Forbidden, code, message);
    public static IResult NotFound(string code, string message) => Error(StatusCodes.Status404NotFound, code, message);
    public static IResult Conflict(string code, string message) => Error(StatusCodes.Status409Conflict, code, message);

    /// <summary>True for errors that come from talking to PostgreSQL (server down, timeout, constraint, ...).</summary>
    public static bool IsDatabaseError(Exception ex) => ex is NpgsqlException or DbUpdateException;

    /// <summary>
    /// HTTP status for a database error. Only problems with *reaching* the database are 503; data the database
    /// refused is the client's fault (4xx), and anything else is a bug (500) rather than "database unavailable".
    /// </summary>
    public static int StatusForDatabaseError(Exception ex)
    {
        var pg = ex as PostgresException ?? ex.InnerException as PostgresException;
        if (pg is null)
            return StatusCodes.Status503ServiceUnavailable; // connection refused, timeout, pool exhausted

        var state = pg.SqlState;
        if (state.StartsWith("23")) return StatusCodes.Status409Conflict;   // integrity constraint violation
        if (state.StartsWith("22")) return StatusCodes.Status400BadRequest; // data exception (bad value / encoding)
        if (state is "55P03" or "57014"                                     // lock or statement timeout: too busy
            || state.StartsWith("08") || state.StartsWith("53") || state.StartsWith("57P"))
            return StatusCodes.Status503ServiceUnavailable;
        return StatusCodes.Status500InternalServerError;
    }

    public static (int Status, string Code, string Message) DescribeDatabaseError(Exception ex)
    {
        var status = StatusForDatabaseError(ex);
        var sqlState = (ex as PostgresException ?? ex.InnerException as PostgresException)?.SqlState;
        return status switch
        {
            409 => (status, "CONFLICT", "The request conflicts with the current data. Refresh and try again."),
            400 => (status, "INVALID_INPUT", "Some input contains characters or values that cannot be stored."),
            503 when sqlState is "55P03" or "57014" =>
                (status, "SERVER_BUSY", "Too many requests right now. Please try again in a few seconds."),
            503 => (status, "DATABASE_UNAVAILABLE", "The database is not reachable right now. Please try again in a moment."),
            _ => (status, "SERVER_ERROR", "Something went wrong on the server. Please try again."),
        };
    }

    /// <summary>Turns a caught database exception into the matching JSON error response.</summary>
    public static IResult FromDatabaseError(Exception ex)
    {
        var (status, code, message) = DescribeDatabaseError(ex);
        return Error(status, code, message);
    }

    /// <summary>Logs a database exception at a level that matches how serious it is.</summary>
    public static void LogDatabaseError(ILogger logger, Exception ex, string action)
    {
        var level = StatusForDatabaseError(ex) == 500 ? LogLevel.Error : LogLevel.Warning;
        logger.Log(level, ex, "Database error while trying to {Action}", action);
    }

    /// <summary>Gives empty error responses (unknown route, wrong method, ...) the standard JSON body.</summary>
    public static Task WriteStatusCodeBody(StatusCodeContext context)
    {
        var response = context.HttpContext.Response;
        var (code, message) = Describe(response.StatusCode);
        return response.WriteAsJsonAsync(new { error = code, message });
    }

    public static (string Code, string Message) Describe(int statusCode) => statusCode switch
    {
        404 => ("NOT_FOUND", "The requested resource was not found."),
        405 => ("METHOD_NOT_ALLOWED", "This HTTP method is not allowed here."),
        413 => ("PAYLOAD_TOO_LARGE", $"The request body is too large (limit {SecurityMiddleware.MaxRequestBodyBytes / 1024} KB)."),
        415 => ("UNSUPPORTED_MEDIA_TYPE", "Send the request body as JSON (Content-Type: application/json)."),
        >= 400 and < 500 => ("BAD_REQUEST", "The request is not valid. Check that the body is correct JSON with the right field types."),
        _ => ("SERVER_ERROR", "Something went wrong on the server. Please try again."),
    };
}

/// <summary>
/// Turns exceptions that escaped the endpoints into JSON errors. Malformed JSON, wrong types and bad parameters
/// become 4xx (rule 7); only genuine bugs become 500.
/// </summary>
public class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        int status;
        string code, message;

        if (exception is BadHttpRequestException bad)
        {
            status = bad.StatusCode;
            (code, message) = ApiErrors.Describe(status);
        }
        else if (ApiErrors.IsDatabaseError(exception))
        {
            ApiErrors.LogDatabaseError(logger, exception, $"{context.Request.Method} {context.Request.Path}");
            (status, code, message) = ApiErrors.DescribeDatabaseError(exception);
        }
        else
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            status = StatusCodes.Status500InternalServerError;
            (code, message) = ApiErrors.Describe(status);
        }

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new { error = code, message }, ct);
        return true;
    }
}

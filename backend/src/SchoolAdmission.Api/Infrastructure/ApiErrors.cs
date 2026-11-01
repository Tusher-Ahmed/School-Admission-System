using Microsoft.AspNetCore.Diagnostics;

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

    public static IResult DatabaseUnavailable() =>
        Error(StatusCodes.Status503ServiceUnavailable, "DATABASE_UNAVAILABLE",
            "The database is not reachable right now. Please try again in a moment.");

    /// <summary>True for errors that come from talking to PostgreSQL (server down, timeout, constraint, ...).</summary>
    public static bool IsDatabaseError(Exception ex) =>
        ex is Npgsql.NpgsqlException or Microsoft.EntityFrameworkCore.DbUpdateException;

    /// <summary>Gives empty error responses (unknown route, wrong method, ...) the standard JSON body.</summary>
    public static Task WriteStatusCodeBody(StatusCodeContext context)
    {
        var response = context.HttpContext.Response;
        var (code, message) = response.StatusCode switch
        {
            404 => ("NOT_FOUND", "The requested resource was not found."),
            405 => ("METHOD_NOT_ALLOWED", "This HTTP method is not allowed here."),
            415 => ("UNSUPPORTED_MEDIA_TYPE", "Send the request body as JSON (Content-Type: application/json)."),
            >= 400 and < 500 => ("BAD_REQUEST", "The request is not valid."),
            _ => ("SERVER_ERROR", "Something went wrong on the server. Please try again."),
        };
        return response.WriteAsJsonAsync(new { error = code, message });
    }
}

/// <summary>
/// Turns exceptions into JSON errors. Malformed JSON, wrong types and bad parameters become 4xx (rule 7);
/// only genuine bugs become 500.
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
            code = "BAD_REQUEST";
            message = "The request is not valid. Check that the body is correct JSON with the right field types.";
        }
        else if (ApiErrors.IsDatabaseError(exception))
        {
            logger.LogWarning(exception, "Database error for {Method} {Path}", context.Request.Method, context.Request.Path);
            status = StatusCodes.Status503ServiceUnavailable;
            code = "DATABASE_UNAVAILABLE";
            message = "The database is not reachable right now. Please try again in a moment.";
        }
        else
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            status = StatusCodes.Status500InternalServerError;
            code = "SERVER_ERROR";
            message = "Something went wrong on the server. Please try again.";
        }

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new { error = code, message }, ct);
        return true;
    }
}

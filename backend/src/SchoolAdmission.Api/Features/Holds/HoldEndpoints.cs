using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Infrastructure;

namespace SchoolAdmission.Api.Features.Holds;

public static class HoldEndpoints
{
    public static void MapHoldEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/classes/{classId:guid}/holds", Apply).RequireStudentId();
        app.MapGet("/holds/{holdId:guid}", GetHold);
        app.MapGet("/me/holds", MyHolds).RequireStudentId();
        app.MapPost("/holds/{holdId:guid}/confirm", Confirm).RequireStudentId();
        app.MapDelete("/holds/{holdId:guid}", Cancel).RequireStudentId();
    }

    private static async Task<IResult> Confirm(Guid holdId, HttpContext http, HoldService holds, ILoggerFactory loggers, CancellationToken ct)
    {
        try
        {
            var result = await holds.ConfirmAsync(holdId, http.GetStudentId(), ct);
            if (result.Outcome != HoldOutcome.Ok)
                return ToError(result.Outcome);

            var h = result.Hold!;
            return Results.Ok(new
            {
                holdId = h.HoldId,
                status = StatusText(h.Status),
                classId = h.ClassId,
                confirmedAt = h.ConfirmedAt,
            });
        }
        catch (Exception ex) when (ApiErrors.IsDatabaseError(ex))
        {
            ApiErrors.LogDatabaseError(loggers.CreateLogger("Holds"), ex, $"confirm hold {holdId}");
            return ApiErrors.FromDatabaseError(ex);
        }
    }

    private static async Task<IResult> Cancel(Guid holdId, HttpContext http, HoldService holds, ILoggerFactory loggers, CancellationToken ct)
    {
        try
        {
            var result = await holds.CancelAsync(holdId, http.GetStudentId(), ct);
            return result.Outcome == HoldOutcome.Ok ? Results.NoContent() : ToError(result.Outcome);
        }
        catch (Exception ex) when (ApiErrors.IsDatabaseError(ex))
        {
            ApiErrors.LogDatabaseError(loggers.CreateLogger("Holds"), ex, $"cancel hold {holdId}");
            return ApiErrors.FromDatabaseError(ex);
        }
    }

    private static async Task<IResult> Apply(Guid classId, HttpContext http, HoldService holds, ILoggerFactory loggers, CancellationToken ct)
    {
        try
        {
            var result = await holds.ApplyAsync(classId, http.GetStudentId(), ct);
            if (result.Outcome != HoldOutcome.Ok)
                return ToError(result.Outcome);

            var h = result.Hold!;
            return Results.Created($"/holds/{h.HoldId}", new
            {
                holdId = h.HoldId,
                classId = h.ClassId,
                studentId = h.StudentId,
                status = StatusText(h.Status),
                expiresAt = h.ExpiresAt,
                className = h.ClassName,
            });
        }
        catch (Exception ex) when (ApiErrors.IsDatabaseError(ex))
        {
            ApiErrors.LogDatabaseError(loggers.CreateLogger("Holds"), ex, $"apply for class {classId}");
            return ApiErrors.FromDatabaseError(ex);
        }
    }

    private static async Task<IResult> GetHold(Guid holdId, HoldService holds, ILoggerFactory loggers, CancellationToken ct)
    {
        try
        {
            var h = await holds.FindAsync(holdId, ct);
            if (h is null)
                return ToError(HoldOutcome.HoldNotFound);

            // studentId is deliberately not returned: this endpoint needs no X-Student-Id.
            return Results.Ok(new
            {
                holdId = h.HoldId,
                classId = h.ClassId,
                status = StatusText(h.Status),
                expiresAt = h.ExpiresAt,
                className = h.ClassName,
            });
        }
        catch (Exception ex) when (ApiErrors.IsDatabaseError(ex))
        {
            ApiErrors.LogDatabaseError(loggers.CreateLogger("Holds"), ex, $"load hold {holdId}");
            return ApiErrors.FromDatabaseError(ex);
        }
    }

    private static async Task<IResult> MyHolds(HttpContext http, HoldService holds, ILoggerFactory loggers, CancellationToken ct)
    {
        try
        {
            var list = await holds.ListForStudentAsync(http.GetStudentId(), ct);
            return Results.Ok(list.Select(h => new
            {
                holdId = h.HoldId,
                classId = h.ClassId,
                className = h.ClassName,
                status = StatusText(h.Status),
                expiresAt = h.ExpiresAt,
                createdAt = h.CreatedAt,
            }));
        }
        catch (Exception ex) when (ApiErrors.IsDatabaseError(ex))
        {
            ApiErrors.LogDatabaseError(loggers.CreateLogger("Holds"), ex, "list holds for student");
            return ApiErrors.FromDatabaseError(ex);
        }
    }

    public static string StatusText(HoldStatus status) => status.ToString().ToUpperInvariant();

    public static IResult ToError(HoldOutcome outcome) => outcome switch
    {
        HoldOutcome.ClassNotFound => ApiErrors.NotFound("CLASS_NOT_FOUND", "This class does not exist."),
        HoldOutcome.ClassFull => ApiErrors.Conflict("CLASS_FULL", "No seats are available in this class."),
        HoldOutcome.AlreadyApplied => ApiErrors.Conflict("ALREADY_APPLIED",
            "This student already has an active application or a confirmed admission in this class."),
        HoldOutcome.HoldNotFound => ApiErrors.NotFound("HOLD_NOT_FOUND", "This application does not exist."),
        HoldOutcome.NotOwner => ApiErrors.Forbidden("NOT_YOUR_APPLICATION", "This application belongs to another student."),
        HoldOutcome.Expired => ApiErrors.Conflict("HOLD_EXPIRED", "This application has expired. Please apply again."),
        HoldOutcome.Cancelled => ApiErrors.Conflict("HOLD_CANCELLED", "This application was cancelled."),
        HoldOutcome.AlreadyConfirmed => ApiErrors.Conflict("ALREADY_CONFIRMED",
            "Admission is already confirmed, so this application cannot be cancelled."),
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
    };
}

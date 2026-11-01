using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Infrastructure;

namespace SchoolAdmission.Api.Features.Classes;

public record CreateClassRequest(string? Name, int? TotalSeats);

public record ClassCreatedResponse(Guid Id, string Name, int TotalSeats);

public record ClassSeatsResponse(Guid Id, string Name, int TotalSeats, int HeldSeats, int ConfirmedSeats, int AvailableSeats);

public static class ClassEndpoints
{
    public const int MaxNameLength = 200;
    public const int MaxSeats = 100_000;

    public static void MapClassEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/classes", CreateClass);
        app.MapGet("/classes", ListClasses);
        app.MapGet("/classes/{classId:guid}", GetClass);
    }

    private static async Task<IResult> CreateClass(
        CreateClassRequest? request, AdmissionDbContext db, ILoggerFactory loggers, CancellationToken ct)
    {
        if (request is null)
            return ApiErrors.BadRequest("INVALID_BODY", "Send JSON like { \"name\": \"Class 6 - Morning Shift\", \"totalSeats\": 120 }.");

        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name))
            return ApiErrors.BadRequest("INVALID_NAME", "Class name is required.");
        if (name.Length > MaxNameLength)
            return ApiErrors.BadRequest("INVALID_NAME", $"Class name must be at most {MaxNameLength} characters.");
        // NUL cannot be stored in PostgreSQL text, and line breaks / escape codes have no place in a class name.
        if (name.Any(char.IsControl))
            return ApiErrors.BadRequest("INVALID_NAME", "Class name must not contain control characters or line breaks.");

        if (request.TotalSeats is not int seats || seats < 1 || seats > MaxSeats)
            return ApiErrors.BadRequest("INVALID_TOTAL_SEATS", $"totalSeats must be a whole number from 1 to {MaxSeats}.");

        try
        {
            var entity = new SchoolClass { Id = Guid.NewGuid(), Name = name, TotalSeats = seats };
            db.Classes.Add(entity);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/classes/{entity.Id}", new ClassCreatedResponse(entity.Id, entity.Name, entity.TotalSeats));
        }
        catch (Exception ex) when (ApiErrors.IsDatabaseError(ex))
        {
            ApiErrors.LogDatabaseError(loggers.CreateLogger("Classes"), ex, "create class");
            return ApiErrors.FromDatabaseError(ex);
        }
    }

    private static async Task<IResult> ListClasses(AdmissionDbContext db, ILoggerFactory loggers, CancellationToken ct)
    {
        try
        {
            var rows = await WithSeatCounts(db, db.Classes.OrderBy(c => c.CreatedAt).ThenBy(c => c.Name)).ToListAsync(ct);
            return Results.Ok(rows.Select(ToResponse));
        }
        catch (Exception ex) when (ApiErrors.IsDatabaseError(ex))
        {
            ApiErrors.LogDatabaseError(loggers.CreateLogger("Classes"), ex, "list classes");
            return ApiErrors.FromDatabaseError(ex);
        }
    }

    private static async Task<IResult> GetClass(Guid classId, AdmissionDbContext db, ILoggerFactory loggers, CancellationToken ct)
    {
        try
        {
            var row = await WithSeatCounts(db, db.Classes.Where(c => c.Id == classId)).SingleOrDefaultAsync(ct);
            return row is null
                ? ApiErrors.NotFound("CLASS_NOT_FOUND", "This class does not exist.")
                : Results.Ok(ToResponse(row));
        }
        catch (Exception ex) when (ApiErrors.IsDatabaseError(ex))
        {
            ApiErrors.LogDatabaseError(loggers.CreateLogger("Classes"), ex, $"load class {classId}");
            return ApiErrors.FromDatabaseError(ex);
        }
    }

    private record SeatCountRow(Guid Id, string Name, int TotalSeats, int HeldSeats, int ConfirmedSeats);

    // Counts are computed from the holds table, never stored, so they cannot drift.
    // DateTime.UtcNow is translated to the database's now(), so every backend process uses the same clock.
    // A HELD hold whose expires_at has passed is not counted: the seat is free the moment time runs out (rule 2).
    // Filter and sort `classes` before calling this: EF cannot translate filters on the projected record.
    private static IQueryable<SeatCountRow> WithSeatCounts(AdmissionDbContext db, IQueryable<SchoolClass> classes) =>
        classes.Select(c => new SeatCountRow(
            c.Id,
            c.Name,
            c.TotalSeats,
            db.Holds.Count(h => h.ClassId == c.Id && h.Status == HoldStatus.Held && h.ExpiresAt > DateTime.UtcNow),
            db.Holds.Count(h => h.ClassId == c.Id && h.Status == HoldStatus.Confirmed)));

    private static ClassSeatsResponse ToResponse(SeatCountRow r) =>
        new(r.Id, r.Name, r.TotalSeats, r.HeldSeats, r.ConfirmedSeats,
            Math.Max(0, r.TotalSeats - r.HeldSeats - r.ConfirmedSeats));
}

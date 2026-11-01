using Microsoft.EntityFrameworkCore;
using Npgsql;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Infrastructure;

namespace SchoolAdmission.Api.Features.Holds;

public enum HoldOutcome
{
    Ok,
    ClassNotFound,
    ClassFull,
    AlreadyApplied,
    HoldNotFound,
    NotOwner,
    Expired,
    Cancelled,
    AlreadyConfirmed,
}

/// <summary>A hold as the API shows it. Status is the effective status: a HELD hold past expires_at is EXPIRED.</summary>
public record HoldView(
    Guid HoldId, Guid ClassId, string ClassName, string StudentId, HoldStatus Status,
    DateTime ExpiresAt, DateTime CreatedAt, DateTime? ConfirmedAt, DateTime? CancelledAt);

public record HoldResult(HoldOutcome Outcome, HoldView? Hold = null);

public class HoldService(AdmissionDbContext db, AdmissionOptions options)
{
    /// <summary>
    /// Applies for a seat (rules 1 and 3). The class row is locked with SELECT ... FOR UPDATE, so for one class
    /// only one transaction at a time can count the active holds and insert a new one. The lock lives in
    /// PostgreSQL, so this also holds across several backend processes.
    /// </summary>
    public async Task<HoldResult> ApplyAsync(Guid classId, string studentId, CancellationToken ct)
    {
        var holdId = Guid.NewGuid();
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            // 1. Lock the class row. Other applications for this class wait here until we commit.
            var locked = await db.Classes
                .FromSql($"SELECT * FROM classes WHERE id = {classId} FOR UPDATE")
                .AsNoTracking()
                .ToListAsync(ct);
            if (locked.Count == 0)
                return new HoldResult(HoldOutcome.ClassNotFound);
            var totalSeats = locked[0].TotalSeats;

            // 2. Release seats of holds that already ran out of time, so they don't block this student or the count.
            await db.Database.ExecuteSqlAsync(
                $"UPDATE holds SET status = 'EXPIRED' WHERE class_id = {classId} AND status = 'HELD' AND expires_at <= now()", ct);

            var active = db.Holds.Where(h => h.ClassId == classId
                && (h.Status == HoldStatus.Held || h.Status == HoldStatus.Confirmed));

            // 3. Rule 3: one active application or admission per student per class.
            if (await active.AnyAsync(h => h.StudentId == studentId, ct))
                return new HoldResult(HoldOutcome.AlreadyApplied);

            // 4. Rule 1: held + confirmed may never exceed totalSeats.
            if (await active.CountAsync(ct) >= totalSeats)
                return new HoldResult(HoldOutcome.ClassFull);

            // 5. Insert. expires_at uses the database clock, like every other time comparison.
            var seconds = options.HoldDuration.TotalSeconds;
            await db.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO holds (id, class_id, student_id, status, expires_at)
                VALUES ({holdId}, {classId}, {studentId}, 'HELD', now() + make_interval(secs => {seconds}))
                """, ct);

            await tx.CommitAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            // Safety net: ux_holds_active rejected a second active hold for this student and class.
            return new HoldResult(HoldOutcome.AlreadyApplied);
        }

        return new HoldResult(HoldOutcome.Ok, await FindAsync(holdId, ct));
    }

    public async Task<HoldView?> FindAsync(Guid holdId, CancellationToken ct)
    {
        var row = await Views(db.Holds.Where(h => h.Id == holdId)).SingleOrDefaultAsync(ct);
        return row is null ? null : Effective(row);
    }

    public async Task<List<HoldView>> ListForStudentAsync(string studentId, CancellationToken ct)
    {
        var rows = await Views(db.Holds.Where(h => h.StudentId == studentId).OrderByDescending(h => h.CreatedAt))
            .ToListAsync(ct);
        return rows.Select(Effective).ToList();
    }

    // Holds joined with their class name. IsLapsed is computed by the database (DateTime.UtcNow -> now()).
    // Filter and sort `holds` before calling this: EF cannot translate filters on the projected record.
    private IQueryable<HoldViewRow> Views(IQueryable<Hold> holds) =>
        from h in holds
        join c in db.Classes on h.ClassId equals c.Id
        select new HoldViewRow(h.Id, h.ClassId, c.Name, h.StudentId, h.Status, h.ExpiresAt,
            h.CreatedAt, h.ConfirmedAt, h.CancelledAt, h.ExpiresAt <= DateTime.UtcNow);

    private static HoldView Effective(HoldViewRow r) =>
        new(r.HoldId, r.ClassId, r.ClassName, r.StudentId,
            r.Status == HoldStatus.Held && r.IsLapsed ? HoldStatus.Expired : r.Status,
            r.ExpiresAt, r.CreatedAt, r.ConfirmedAt, r.CancelledAt);

    private record HoldViewRow(
        Guid HoldId, Guid ClassId, string ClassName, string StudentId, HoldStatus Status, DateTime ExpiresAt,
        DateTime CreatedAt, DateTime? ConfirmedAt, DateTime? CancelledAt, bool IsLapsed);
}

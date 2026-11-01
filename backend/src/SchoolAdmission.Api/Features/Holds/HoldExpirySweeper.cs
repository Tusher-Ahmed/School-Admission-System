using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data;

namespace SchoolAdmission.Api.Features.Holds;

/// <summary>
/// Every few seconds, stores status EXPIRED on HELD holds whose time has run out.
/// Seats are already free the moment expires_at passes (all reads compare with now()); this job only keeps the
/// stored status tidy. Safe with several backend processes: the UPDATE is idempotent and row-locked.
/// </summary>
public class HoldExpirySweeper(IServiceScopeFactory scopes, ILogger<HoldExpirySweeper> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AdmissionDbContext>();
                var expired = await db.Database.ExecuteSqlAsync(
                    $"UPDATE holds SET status = 'EXPIRED' WHERE status = 'HELD' AND expires_at <= now()", stoppingToken);
                if (expired > 0)
                    logger.LogInformation("Marked {Count} hold(s) as EXPIRED", expired);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Never let a database hiccup stop the sweeper; the next tick tries again.
                logger.LogWarning(ex, "Expiry sweep failed; will retry in {Interval}", Interval);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

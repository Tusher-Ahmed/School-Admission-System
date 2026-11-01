using System.Net;

namespace SchoolAdmission.Tests;

/// <summary>A second, completely separate API instance (own DI container and connection pool).</summary>
public class SecondApiFactory : ApiFactory;

/// <summary>
/// Bonus: two backend instances share one database. Rule 1 must hold across them, which works because the
/// lock is the PostgreSQL row lock, not anything in process memory.
/// (scripts/two-process-check.ps1 does the same with two real processes on ports 8080 and 8081.)
/// </summary>
public class MultiInstanceTests(ApiFactory first, SecondApiFactory second)
    : IClassFixture<ApiFactory>, IClassFixture<SecondApiFactory>
{
    [Fact]
    public async Task TwoInstances_SameClass_NeverMoreHoldsThanSeats()
    {
        const int seats = 8, requestsPerInstance = 25;
        var clientA = first.CreateClient();
        var clientB = second.CreateClient();
        var classId = await clientA.CreateClassAsync(seats, "two-instances");

        var responses = await ConcurrencyTests.RaceAsync(requestsPerInstance * 2, i =>
            (i % 2 == 0 ? clientA : clientB).ApplyAsync(classId, TestData.NewStudentId()));

        Assert.Equal(seats, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        var fromA = await clientA.GetClassAsync(classId);
        var fromB = await clientB.GetClassAsync(classId);
        Assert.Equal(fromA, fromB);
        Assert.Equal((seats, 0), (fromA.HeldSeats, fromA.AvailableSeats));
    }
}

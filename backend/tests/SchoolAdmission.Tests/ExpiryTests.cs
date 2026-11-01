using System.Net;
using System.Net.Http.Json;

namespace SchoolAdmission.Tests;

/// <summary>Runs the API with HOLD_DURATION_SECONDS=2 to watch real expiry, with no API call involved.</summary>
public class ShortHoldApiFactory() : ApiFactory(holdDurationSeconds: 2);

public class ExpiryTests(ShortHoldApiFactory factory) : IClassFixture<ShortHoldApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Hold_ExpiresOnItsOwn_AndSweeperStoresExpired()
    {
        var classId = await _client.CreateClassAsync(seats: 1);
        var student = TestData.NewStudentId();
        var hold = await _client.ApplyOkAsync(classId, student);
        Assert.InRange(hold.ExpiresAt, DateTime.UtcNow, DateTime.UtcNow.AddSeconds(5)); // duration comes from config

        // Rule 2: once the time is up, the seat is back without anyone calling the API.
        await Task.Delay(TimeSpan.FromSeconds(2.5));
        Assert.Equal(1, (await _client.GetClassAsync(classId)).AvailableSeats);
        Assert.Equal("EXPIRED", (await _client.GetFromJsonAsync<HoldDto>($"/holds/{hold.HoldId}"))!.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.ConfirmAsync(hold.HoldId, student)).StatusCode);

        // The background sweeper also writes EXPIRED into the row within one interval.
        var deadline = DateTime.UtcNow + HoldExpirySweeperInterval + TimeSpan.FromSeconds(3);
        string stored;
        do
        {
            stored = await factory.StoredStatusAsync(hold.HoldId);
            if (stored == "EXPIRED") break;
            await Task.Delay(500);
        } while (DateTime.UtcNow < deadline);
        Assert.Equal("EXPIRED", stored);
    }

    private static TimeSpan HoldExpirySweeperInterval => SchoolAdmission.Api.Features.Holds.HoldExpirySweeper.Interval;
}

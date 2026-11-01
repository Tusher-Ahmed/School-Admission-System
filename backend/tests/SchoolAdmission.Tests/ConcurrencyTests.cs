using System.Net;
using System.Net.Http.Json;

namespace SchoolAdmission.Tests;

/// <summary>
/// Fires many requests at the same moment and checks that the rules still hold.
/// All requests start together behind a gate so they really race each other.
/// </summary>
public class ConcurrencyTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ManyStudentsAtOnce_NeverMoreHoldsThanSeats()
    {
        const int seats = 10, requests = 50;
        var classId = await _client.CreateClassAsync(seats, "concurrency");

        var responses = await RaceAsync(requests, _ => _client.ApplyAsync(classId, TestData.NewStudentId()));

        Assert.Equal(seats, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(requests - seats, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        var cls = await _client.GetClassAsync(classId);
        Assert.Equal((seats, 0, 0), (cls.HeldSeats, cls.ConfirmedSeats, cls.AvailableSeats));
    }

    [Fact]
    public async Task SameStudentAppliesManyTimesAtOnce_OnlyOneHold()
    {
        var classId = await _client.CreateClassAsync(seats: 5, "same-student");
        var student = TestData.NewStudentId();

        var responses = await RaceAsync(20, _ => _client.ApplyAsync(classId, student));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Equal(19, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(1, (await _client.GetClassAsync(classId)).HeldSeats);
    }

    [Fact]
    public async Task ConfirmManyTimesAtOnce_AllOk_CountedOnce()
    {
        var classId = await _client.CreateClassAsync(seats: 3, "double-confirm");
        var student = TestData.NewStudentId();
        var hold = await _client.ApplyOkAsync(classId, student);

        var responses = await RaceAsync(10, _ => _client.ConfirmAsync(hold.HoldId, student));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var cls = await _client.GetClassAsync(classId);
        Assert.Equal((0, 1), (cls.HeldSeats, cls.ConfirmedSeats));
    }

    [Fact]
    public async Task ConfirmAndCancelAtOnce_ExactlyOneWins()
    {
        for (var round = 0; round < 10; round++)
        {
            var classId = await _client.CreateClassAsync(seats: 1, "confirm-vs-cancel");
            var student = TestData.NewStudentId();
            var hold = await _client.ApplyOkAsync(classId, student);

            var responses = await RaceAsync(2, i => i == 0
                ? _client.ConfirmAsync(hold.HoldId, student)
                : _client.CancelAsync(hold.HoldId, student));

            var confirmed = responses[0].StatusCode == HttpStatusCode.OK;
            var cancelled = responses[1].StatusCode == HttpStatusCode.NoContent;
            Assert.True(confirmed ^ cancelled, $"round {round}: confirm={responses[0].StatusCode}, cancel={responses[1].StatusCode}");
            Assert.Equal(HttpStatusCode.Conflict, confirmed ? responses[1].StatusCode : responses[0].StatusCode);

            var cls = await _client.GetClassAsync(classId);
            Assert.Equal(confirmed ? (0, 1, 0) : (0, 0, 1), (cls.HeldSeats, cls.ConfirmedSeats, cls.AvailableSeats));
        }
    }

    [Fact]
    public async Task ApplyCancelAndReapplyChurn_NeverOverbooks()
    {
        const int seats = 5;
        var classId = await _client.CreateClassAsync(seats, "churn");

        // Each worker applies, cancels half the time, and applies again, all in parallel.
        var outcomes = await RaceAsync(30, async i =>
        {
            var student = TestData.NewStudentId();
            var first = await _client.ApplyAsync(classId, student);
            if (first.StatusCode == HttpStatusCode.Created && i % 2 == 0)
            {
                var hold = await first.Content.ReadFromJsonAsync<HoldDto>();
                await _client.CancelAsync(hold!.HoldId, student);
                return await _client.ApplyAsync(classId, student);
            }
            return first;
        });

        Assert.All(outcomes, r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.Conflict }));
        var cls = await _client.GetClassAsync(classId);
        Assert.True(cls.HeldSeats + cls.ConfirmedSeats <= seats, $"held {cls.HeldSeats} + confirmed {cls.ConfirmedSeats} > {seats}");
        Assert.Equal(seats, cls.HeldSeats); // with 30 students every seat ends up taken
    }

    /// <summary>Starts all calls at the same moment and waits for every response.</summary>
    internal static async Task<HttpResponseMessage[]> RaceAsync(int count, Func<int, Task<HttpResponseMessage>> call)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, count).Select(async i =>
        {
            await gate.Task;
            return await call(i);
        }).ToArray();
        gate.SetResult();
        return await Task.WhenAll(tasks);
    }
}

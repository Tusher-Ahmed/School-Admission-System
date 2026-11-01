using System.Net;
using System.Text.Json;

namespace SchoolAdmission.Tests;

/// <summary>
/// A student may have only one active application (HELD and running, or CONFIRMED) per year, in any class.
/// Expired or cancelled applications, and applications from last year, do not block a new one.
/// </summary>
public class OnePerYearTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task HeldInOneClass_BlocksApplyingToAnother_AndPointsToIt()
    {
        var student = TestData.NewStudentId();
        var first = await _client.ApplyOkAsync(await _client.CreateClassAsync(seats: 3, "first"), student);
        var otherClass = await _client.CreateClassAsync(seats: 3, "second");

        var response = await _client.ApplyAsync(otherClass, student);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("ALREADY_APPLIED_THIS_YEAR", json.GetProperty("error").GetString());
        Assert.Equal(first.HoldId.ToString(), json.GetProperty("existingHoldId").GetString());
        Assert.Equal(0, (await _client.GetClassAsync(otherClass)).HeldSeats);
    }

    [Fact]
    public async Task ConfirmedInOneClass_BlocksApplyingToAnother()
    {
        var student = TestData.NewStudentId();
        var first = await _client.ApplyOkAsync(await _client.CreateClassAsync(seats: 3), student);
        await _client.ConfirmAsync(first.HoldId, student);

        var response = await _client.ApplyAsync(await _client.CreateClassAsync(seats: 3), student);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("ALREADY_APPLIED_THIS_YEAR", await response.ErrorCodeAsync());
    }

    [Fact]
    public async Task AfterCancelling_StudentCanApplyToAnotherClass()
    {
        var student = TestData.NewStudentId();
        var first = await _client.ApplyOkAsync(await _client.CreateClassAsync(seats: 3), student);
        await _client.CancelAsync(first.HoldId, student);

        var response = await _client.ApplyAsync(await _client.CreateClassAsync(seats: 3), student);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task AfterExpiry_StudentCanApplyToAnotherClass()
    {
        var student = TestData.NewStudentId();
        var first = await _client.ApplyOkAsync(await _client.CreateClassAsync(seats: 3), student);
        await factory.ExpireNowAsync(first.HoldId);

        var response = await _client.ApplyAsync(await _client.CreateClassAsync(seats: 3), student);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task LastYearsAdmission_DoesNotBlockThisYear()
    {
        var student = TestData.NewStudentId();
        var lastYear = await _client.ApplyOkAsync(await _client.CreateClassAsync(seats: 3, "last-year"), student);
        await _client.ConfirmAsync(lastYear.HoldId, student);
        await factory.MoveToLastYearAsync(lastYear.HoldId);

        var response = await _client.ApplyAsync(await _client.CreateClassAsync(seats: 3, "this-year"), student);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task SameStudentAppliesToManyClassesAtOnce_OnlyOneSucceeds()
    {
        var student = TestData.NewStudentId();
        var classes = new List<Guid>();
        for (var i = 0; i < 8; i++)
            classes.Add(await _client.CreateClassAsync(seats: 5, $"race-{i}"));

        var responses = await ConcurrencyTests.RaceAsync(classes.Count, i => _client.ApplyAsync(classes[i], student));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Equal(classes.Count - 1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        var held = 0;
        foreach (var c in classes) held += (await _client.GetClassAsync(c)).HeldSeats;
        Assert.Equal(1, held);
    }
}

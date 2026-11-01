using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SchoolAdmission.Tests;

public class ConfirmCancelApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(Guid classId, string student, HoldDto hold)> NewHoldAsync(int seats = 3)
    {
        var classId = await _client.CreateClassAsync(seats);
        var student = TestData.NewStudentId();
        return (classId, student, await _client.ApplyOkAsync(classId, student));
    }

    [Fact]
    public async Task Confirm_OwnHeldHold_Returns200Confirmed_AndMovesSeatToConfirmed()
    {
        var (classId, student, hold) = await NewHoldAsync();

        var response = await _client.ConfirmAsync(hold.HoldId, student);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(hold.HoldId.ToString(), json.GetProperty("holdId").GetString());
        Assert.Equal("CONFIRMED", json.GetProperty("status").GetString());
        var cls = await _client.GetClassAsync(classId);
        Assert.Equal((0, 1, 2), (cls.HeldSeats, cls.ConfirmedSeats, cls.AvailableSeats));
        Assert.Equal("CONFIRMED", (await _client.GetFromJsonAsync<HoldDto>($"/holds/{hold.HoldId}"))!.Status);
    }

    [Fact]
    public async Task Confirm_Twice_CountsOnce()
    {
        var (classId, student, hold) = await NewHoldAsync();

        var first = await _client.ConfirmAsync(hold.HoldId, student);
        var second = await _client.ConfirmAsync(hold.HoldId, student);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(1, (await _client.GetClassAsync(classId)).ConfirmedSeats);
    }

    [Fact]
    public async Task Confirm_OtherStudentsHold_Returns403_AndChangesNothing()
    {
        var (classId, _, hold) = await NewHoldAsync();

        var response = await _client.ConfirmAsync(hold.HoldId, TestData.NewStudentId());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("NOT_YOUR_APPLICATION", await response.ErrorCodeAsync());
        Assert.Equal(0, (await _client.GetClassAsync(classId)).ConfirmedSeats);
    }

    [Fact]
    public async Task Confirm_ExpiredHold_Returns409()
    {
        var (classId, student, hold) = await NewHoldAsync();
        await factory.ExpireNowAsync(hold.HoldId);

        var response = await _client.ConfirmAsync(hold.HoldId, student);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("HOLD_EXPIRED", await response.ErrorCodeAsync());
        Assert.Equal(0, (await _client.GetClassAsync(classId)).ConfirmedSeats);
    }

    [Fact]
    public async Task Confirm_CancelledHold_Returns409()
    {
        var (_, student, hold) = await NewHoldAsync();
        await _client.CancelAsync(hold.HoldId, student);

        var response = await _client.ConfirmAsync(hold.HoldId, student);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("HOLD_CANCELLED", await response.ErrorCodeAsync());
    }

    [Theory]
    [InlineData("POST", "/holds/00000000-0000-0000-0000-000000000000/confirm")]
    [InlineData("DELETE", "/holds/00000000-0000-0000-0000-000000000000")]
    [InlineData("POST", "/holds/h_789/confirm")]
    public async Task ConfirmOrCancel_UnknownHold_Returns404(string method, string url)
    {
        var response = await _client.SendAsStudent(new HttpMethod(method), url, TestData.NewStudentId());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ConfirmOrCancel_WithoutStudentHeader_Returns400()
    {
        var (_, _, hold) = await NewHoldAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await _client.ConfirmAsync(hold.HoldId, null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.CancelAsync(hold.HoldId, null)).StatusCode);
    }

    [Fact]
    public async Task Cancel_OwnHeldHold_Returns204_AndFreesSeat()
    {
        var (classId, student, hold) = await NewHoldAsync(seats: 1);

        var response = await _client.CancelAsync(hold.HoldId, student);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("CANCELLED", (await _client.GetFromJsonAsync<HoldDto>($"/holds/{hold.HoldId}"))!.Status);
        Assert.Equal(1, (await _client.GetClassAsync(classId)).AvailableSeats);
        // The freed seat can be taken, also by the same student.
        Assert.Equal(HttpStatusCode.Created, (await _client.ApplyAsync(classId, student)).StatusCode);
    }

    [Fact]
    public async Task Cancel_Twice_Returns204Both()
    {
        var (_, student, hold) = await NewHoldAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await _client.CancelAsync(hold.HoldId, student)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.CancelAsync(hold.HoldId, student)).StatusCode);
    }

    [Fact]
    public async Task Cancel_OtherStudentsHold_Returns403()
    {
        var (classId, _, hold) = await NewHoldAsync();

        var response = await _client.CancelAsync(hold.HoldId, TestData.NewStudentId());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(1, (await _client.GetClassAsync(classId)).HeldSeats);
    }

    [Fact]
    public async Task Cancel_ConfirmedHold_Returns409()
    {
        var (classId, student, hold) = await NewHoldAsync();
        await _client.ConfirmAsync(hold.HoldId, student);

        var response = await _client.CancelAsync(hold.HoldId, student);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("ALREADY_CONFIRMED", await response.ErrorCodeAsync());
        Assert.Equal(1, (await _client.GetClassAsync(classId)).ConfirmedSeats);
    }

    [Fact]
    public async Task ConfirmedStudent_CannotApplyAgainToSameClass()
    {
        var (classId, student, hold) = await NewHoldAsync();
        await _client.ConfirmAsync(hold.HoldId, student);

        var response = await _client.ApplyAsync(classId, student);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("ALREADY_APPLIED", await response.ErrorCodeAsync());
    }
}

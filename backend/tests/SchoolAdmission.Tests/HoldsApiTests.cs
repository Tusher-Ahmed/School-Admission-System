using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SchoolAdmission.Tests;

public class HoldsApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Apply_Returns201WithContractFields()
    {
        var classId = await _client.CreateClassAsync(seats: 3);
        var student = TestData.NewStudentId();

        var response = await _client.ApplyAsync(classId, student);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.False(string.IsNullOrEmpty(json.GetProperty("holdId").GetString()));
        Assert.Equal(classId.ToString(), json.GetProperty("classId").GetString());
        Assert.Equal(student, json.GetProperty("studentId").GetString());
        Assert.Equal("HELD", json.GetProperty("status").GetString());
        var expiresAt = json.GetProperty("expiresAt").GetDateTime();
        Assert.EndsWith("Z", json.GetProperty("expiresAt").GetString());
        Assert.InRange(expiresAt, DateTime.UtcNow.AddSeconds(590), DateTime.UtcNow.AddSeconds(610)); // default 600 s
    }

    [Fact]
    public async Task Apply_HoldsOneSeat()
    {
        var classId = await _client.CreateClassAsync(seats: 3);

        await _client.ApplyOkAsync(classId, TestData.NewStudentId());

        var cls = await _client.GetClassAsync(classId);
        Assert.Equal((3, 1, 0, 2), (cls.TotalSeats, cls.HeldSeats, cls.ConfirmedSeats, cls.AvailableSeats));
    }

    [Fact]
    public async Task Apply_WhenNoSeatLeft_Returns409ClassFull()
    {
        var classId = await _client.CreateClassAsync(seats: 1);
        await _client.ApplyOkAsync(classId, TestData.NewStudentId());

        var response = await _client.ApplyAsync(classId, TestData.NewStudentId());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("CLASS_FULL", await response.ErrorCodeAsync());
    }

    [Fact]
    public async Task Apply_SameStudentTwice_Returns409AlreadyApplied()
    {
        var classId = await _client.CreateClassAsync(seats: 5);
        var student = TestData.NewStudentId();
        await _client.ApplyOkAsync(classId, student);

        var response = await _client.ApplyAsync(classId, student);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("ALREADY_APPLIED", await response.ErrorCodeAsync());
        Assert.Equal(1, (await _client.GetClassAsync(classId)).HeldSeats);
    }

    [Fact]
    public async Task Apply_SameStudentDifferentClasses_IsAllowed()
    {
        var student = TestData.NewStudentId();

        await _client.ApplyOkAsync(await _client.CreateClassAsync(seats: 2), student);
        var second = await _client.ApplyAsync(await _client.CreateClassAsync(seats: 2), student);

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    [Theory]
    [InlineData("/classes/00000000-0000-0000-0000-000000000000/holds")]
    [InlineData("/classes/not-a-guid/holds")]
    public async Task Apply_UnknownClass_Returns404(string url)
    {
        var response = await _client.SendAsStudent(HttpMethod.Post, url, TestData.NewStudentId());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(null, "MISSING_STUDENT_ID")]
    [InlineData("", "MISSING_STUDENT_ID")]
    [InlineData("123", "INVALID_STUDENT_ID")]
    [InlineData("2015269123456789X", "INVALID_STUDENT_ID")]
    public async Task Apply_BadStudentHeader_Returns400(string? header, string expectedCode)
    {
        var classId = await _client.CreateClassAsync(seats: 2);

        var response = await _client.ApplyAsync(classId, header);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(expectedCode, await response.ErrorCodeAsync());
        Assert.Equal(0, (await _client.GetClassAsync(classId)).HeldSeats);
    }

    [Fact]
    public async Task GetHold_ReturnsCurrentState_WithoutStudentId()
    {
        var classId = await _client.CreateClassAsync(seats: 2);
        var hold = await _client.ApplyOkAsync(classId, TestData.NewStudentId());

        var json = JsonDocument.Parse(await _client.GetStringAsync($"/holds/{hold.HoldId}")).RootElement;

        Assert.Equal(hold.HoldId.ToString(), json.GetProperty("holdId").GetString());
        Assert.Equal(classId.ToString(), json.GetProperty("classId").GetString());
        Assert.Equal("HELD", json.GetProperty("status").GetString());
        Assert.Equal(hold.ExpiresAt, json.GetProperty("expiresAt").GetDateTime());
        Assert.False(json.TryGetProperty("studentId", out _));
    }

    [Theory]
    [InlineData("/holds/00000000-0000-0000-0000-000000000000")]
    [InlineData("/holds/h_789")]
    public async Task GetHold_UnknownId_Returns404(string url)
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task ExpiredHold_ShowsExpired_FreesSeat_AndStudentCanApplyAgain()
    {
        var classId = await _client.CreateClassAsync(seats: 1);
        var student = TestData.NewStudentId();
        var hold = await _client.ApplyOkAsync(classId, student);

        await factory.ExpireNowAsync(hold.HoldId);

        var after = await _client.GetFromJsonAsync<HoldDto>($"/holds/{hold.HoldId}");
        Assert.Equal("EXPIRED", after!.Status);
        var cls = await _client.GetClassAsync(classId);
        Assert.Equal((0, 1), (cls.HeldSeats, cls.AvailableSeats));
        Assert.Equal(HttpStatusCode.Created, (await _client.ApplyAsync(classId, student)).StatusCode);
    }

    [Fact]
    public async Task MyHolds_ReturnsOnlyOwnHolds_NewestFirst()
    {
        var me = TestData.NewStudentId();
        var other = TestData.NewStudentId();
        var first = await _client.ApplyOkAsync(await _client.CreateClassAsync(seats: 2, "first"), me);
        var second = await _client.ApplyOkAsync(await _client.CreateClassAsync(seats: 2, "second"), me);
        await _client.ApplyOkAsync(await _client.CreateClassAsync(seats: 2), other);

        var response = await _client.SendAsStudent(HttpMethod.Get, "/me/holds", me);

        var mine = await response.Content.ReadFromJsonAsync<List<HoldDto>>();
        Assert.NotNull(mine);
        Assert.Equal([second.HoldId, first.HoldId], mine.Select(h => h.HoldId));
        Assert.All(mine, h => Assert.StartsWith("TEST", h.ClassName));
        Assert.All(mine, h => Assert.Equal("HELD", h.Status));
    }

    [Fact]
    public async Task MyHolds_WithoutHeader_Returns400()
    {
        var response = await _client.GetAsync("/me/holds");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

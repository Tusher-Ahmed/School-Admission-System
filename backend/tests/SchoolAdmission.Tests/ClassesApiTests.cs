using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace SchoolAdmission.Tests;

public class ClassesApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CreateClass_Returns201WithContractFields()
    {
        var response = await _client.PostAsJsonAsync("/classes", new { name = "  TEST Class 6 - Morning Shift  ", totalSeats = 120 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.False(string.IsNullOrEmpty(json.GetProperty("id").GetString()));
        Assert.Equal("TEST Class 6 - Morning Shift", json.GetProperty("name").GetString());
        Assert.Equal(120, json.GetProperty("totalSeats").GetInt32());
    }

    [Fact]
    public async Task NewClass_HasAllSeatsAvailable_InListAndDetail()
    {
        var classId = await _client.CreateClassAsync(seats: 7);

        var detail = await _client.GetClassAsync(classId);
        var list = await _client.GetFromJsonAsync<List<ClassDto>>("/classes");

        Assert.Equal(new ClassDto(classId, detail.Name, 7, 0, 0, 7), detail);
        Assert.Contains(list!, c => c == detail);
    }

    [Theory]
    [InlineData("""{"name":"","totalSeats":10}""", "INVALID_NAME")]
    [InlineData("""{"name":"   ","totalSeats":10}""", "INVALID_NAME")]
    [InlineData("""{"totalSeats":10}""", "INVALID_NAME")]
    [InlineData("""{"name":"TEST x"}""", "INVALID_TOTAL_SEATS")]
    [InlineData("""{"name":"TEST x","totalSeats":0}""", "INVALID_TOTAL_SEATS")]
    [InlineData("""{"name":"TEST x","totalSeats":-3}""", "INVALID_TOTAL_SEATS")]
    [InlineData("""{"name":"TEST x","totalSeats":100001}""", "INVALID_TOTAL_SEATS")]
    [InlineData("""{"name":"TEST x","totalSeats":"abc"}""", "BAD_REQUEST")]
    [InlineData("""{"name":"TEST x","totalSeats":1.5}""", "BAD_REQUEST")]
    [InlineData("""{bad json""", "BAD_REQUEST")]
    [InlineData("""null""", "INVALID_BODY")]
    public async Task CreateClass_InvalidInput_Returns400(string body, string expectedCode)
    {
        var response = await _client.PostAsync("/classes", new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(expectedCode, await response.ErrorCodeAsync());
    }

    [Fact]
    public async Task CreateClass_WithoutJsonContentType_Returns4xx()
    {
        var response = await _client.PostAsync("/classes", new StringContent("name=x", Encoding.UTF8, "text/plain"));

        Assert.InRange((int)response.StatusCode, 400, 499);
    }

    [Theory]
    [InlineData("/classes/not-a-guid")]
    [InlineData("/classes/00000000-0000-0000-0000-000000000000")]
    public async Task GetClass_UnknownId_Returns404(string url)
    {
        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

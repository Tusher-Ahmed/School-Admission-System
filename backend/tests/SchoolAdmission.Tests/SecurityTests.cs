using System.Net;
using System.Net.Http.Json;
using System.Text;
using Npgsql;
using SchoolAdmission.Api.Infrastructure;

namespace SchoolAdmission.Tests;

public class SecurityTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("TEST bad\u0000name")]  // PostgreSQL text cannot store NUL
    [InlineData("TEST line\nbreak")]
    [InlineData("TEST \u001b[31mred")]
    public async Task ClassName_WithControlCharacters_Returns400_Not5xx(string name)
    {
        var response = await _client.PostAsJsonAsync("/classes", new { name, totalSeats = 5 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("INVALID_NAME", await response.ErrorCodeAsync());
    }

    [Fact]
    public async Task OversizedBody_Returns413()
    {
        var huge = $$"""{"name":"TEST {{new string('x', 100_000)}}","totalSeats":5}""";

        var response = await _client.PostAsync("/classes", new StringContent(huge, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("PAYLOAD_TOO_LARGE", await response.ErrorCodeAsync());
    }

    [Theory]
    [InlineData("http://localhost:5173")]
    [InlineData("http://127.0.0.1:5174")]
    public async Task Cors_AllowsLocalFrontend(string origin)
    {
        var response = await Preflight(origin);

        Assert.Equal(origin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("http://localhost.evil.example")]
    public async Task Cors_RejectsOtherWebsites(string origin)
    {
        var response = await Preflight(origin);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Responses_HaveSecurityHeaders_AndAreNotCached()
    {
        var response = await _client.GetAsync("/classes");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Theory]
    [InlineData("23505", 409)] // unique violation
    [InlineData("23514", 409)] // check violation
    [InlineData("22021", 400)] // invalid byte sequence
    [InlineData("22P02", 400)] // invalid text representation
    [InlineData("55P03", 503)] // lock timeout: too busy right now
    [InlineData("57P01", 503)] // admin shutdown
    [InlineData("08006", 503)] // connection failure
    [InlineData("42P01", 500)] // undefined table: a real bug, not "database unavailable"
    public void DatabaseErrors_MapToMeaningfulStatus(string sqlState, int expectedStatus)
    {
        var ex = new PostgresException("test", "ERROR", "ERROR", sqlState);

        Assert.Equal(expectedStatus, ApiErrors.StatusForDatabaseError(ex));
    }

    [Fact]
    public void ConnectionFailure_MapsTo503()
    {
        Assert.Equal(503, ApiErrors.StatusForDatabaseError(new NpgsqlException("connection refused")));
    }

    private Task<HttpResponseMessage> Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/classes");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");
        return _client.SendAsync(request);
    }
}

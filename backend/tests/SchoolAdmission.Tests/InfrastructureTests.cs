using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using SchoolAdmission.Api.Infrastructure;

namespace SchoolAdmission.Tests;

public class InfrastructureTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private record ErrorBody(string Error, string Message);

    [Fact]
    public async Task UnknownRoute_Returns404WithJsonError()
    {
        var response = await factory.CreateClient().GetAsync("/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("NOT_FOUND", body!.Error);
    }

    [Fact]
    public async Task CorsPreflight_AllowsStudentIdHeader()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/health");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "x-student-id,content-type");

        var response = await factory.CreateClient().SendAsync(request);

        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.Contains("x-student-id", string.Join(",", response.Headers.GetValues("Access-Control-Allow-Headers")),
            StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null, 600)]
    [InlineData("", 600)]
    [InlineData("30", 30)]
    [InlineData(" 45 ", 45)]
    public void HoldDuration_ParsesValidValues(string? raw, int expectedSeconds)
    {
        var options = AdmissionOptions.FromConfiguration(Config(raw));

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), options.HoldDuration);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("abc")]
    [InlineData("1.5")]
    public void HoldDuration_RejectsInvalidValues(string raw)
    {
        Assert.Throws<InvalidOperationException>(() => AdmissionOptions.FromConfiguration(Config(raw)));
    }

    private static IConfiguration Config(string? holdSeconds) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["HOLD_DURATION_SECONDS"] = holdSeconds })
            .Build();
}

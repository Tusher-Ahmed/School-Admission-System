using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SchoolAdmission.Tests;

/// <summary>
/// Hosts the real API in memory against the real PostgreSQL database (ConnectionStrings__Default,
/// or TEST_DB_CONNECTION if set). Tests never wipe data: each creates its own "TEST ..." class
/// and random student IDs, and only asserts on that data.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public ApiFactory() : this(holdDurationSeconds: null) { }

    protected ApiFactory(int? holdDurationSeconds) => HoldDurationSeconds = holdDurationSeconds;

    private int? HoldDurationSeconds { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var testDb = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION");
        if (!string.IsNullOrWhiteSpace(testDb))
            builder.UseSetting("ConnectionStrings:Default", testDb);

        if (HoldDurationSeconds is int seconds)
            builder.UseSetting("HOLD_DURATION_SECONDS", seconds.ToString());
    }
}

public record ErrorBody(string Error, string Message);

public record ClassDto(Guid Id, string Name, int TotalSeats, int HeldSeats, int ConfirmedSeats, int AvailableSeats);

public record HoldDto(Guid HoldId, Guid ClassId, string? StudentId, string? ClassName, string Status, DateTime ExpiresAt);

public static class TestData
{
    /// <summary>A random, valid 17-digit birth registration number.</summary>
    public static string NewStudentId() =>
        "9" + string.Concat(Enumerable.Range(0, 16).Select(_ => Random.Shared.Next(10)));

    public static async Task<Guid> CreateClassAsync(this HttpClient client, int seats, string label = "")
    {
        var response = await client.PostAsJsonAsync("/classes",
            new { name = $"TEST {label} {Guid.NewGuid():N}".Trim(), totalSeats = seats });
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<ClassDto>();
        return created!.Id;
    }

    public static async Task<ClassDto> GetClassAsync(this HttpClient client, Guid classId) =>
        (await client.GetFromJsonAsync<ClassDto>($"/classes/{classId}"))!;

    public static Task<HttpResponseMessage> SendAsStudent(this HttpClient client, HttpMethod method, string url, string? studentId)
    {
        var request = new HttpRequestMessage(method, url);
        if (studentId is not null)
            request.Headers.Add("X-Student-Id", studentId);
        return client.SendAsync(request);
    }

    public static Task<HttpResponseMessage> ApplyAsync(this HttpClient client, Guid classId, string? studentId) =>
        client.SendAsStudent(HttpMethod.Post, $"/classes/{classId}/holds", studentId);

    public static Task<HttpResponseMessage> ConfirmAsync(this HttpClient client, Guid holdId, string? studentId) =>
        client.SendAsStudent(HttpMethod.Post, $"/holds/{holdId}/confirm", studentId);

    public static Task<HttpResponseMessage> CancelAsync(this HttpClient client, Guid holdId, string? studentId) =>
        client.SendAsStudent(HttpMethod.Delete, $"/holds/{holdId}", studentId);

    public static async Task<HoldDto> ApplyOkAsync(this HttpClient client, Guid classId, string studentId)
    {
        var response = await client.ApplyAsync(classId, studentId);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<HoldDto>())!;
    }

    public static async Task<string> ErrorCodeAsync(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ErrorBody>())!.Error;
}

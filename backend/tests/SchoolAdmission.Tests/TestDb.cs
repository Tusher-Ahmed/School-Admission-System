using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SchoolAdmission.Api.Infrastructure;

namespace SchoolAdmission.Tests;

/// <summary>Direct database access for test setup, e.g. making a hold expire without waiting.</summary>
public static class TestDb
{
    public static async Task ExpireNowAsync(this ApiFactory factory, Guid holdId)
    {
        await using var conn = await OpenAsync(factory);
        await using var cmd = new NpgsqlCommand(
            "UPDATE holds SET expires_at = now() - interval '1 second' WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", holdId);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Pretends the application was made a year ago.</summary>
    public static async Task MoveToLastYearAsync(this ApiFactory factory, Guid holdId)
    {
        await using var conn = await OpenAsync(factory);
        await using var cmd = new NpgsqlCommand(
            "UPDATE holds SET created_at = created_at - interval '1 year' WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", holdId);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>The status column exactly as stored (not the effective status the API computes).</summary>
    public static async Task<string> StoredStatusAsync(this ApiFactory factory, Guid holdId)
    {
        await using var conn = await OpenAsync(factory);
        await using var cmd = new NpgsqlCommand("SELECT status FROM holds WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", holdId);
        return (string)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<NpgsqlConnection> OpenAsync(ApiFactory factory)
    {
        var connectionString = DatabaseConfig.GetConnectionString(factory.Services.GetRequiredService<IConfiguration>());
        var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        return conn;
    }
}

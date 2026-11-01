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
        var connectionString = DatabaseConfig.GetConnectionString(factory.Services.GetRequiredService<IConfiguration>());
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "UPDATE holds SET expires_at = now() - interval '1 second' WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", holdId);
        await cmd.ExecuteNonQueryAsync();
    }
}

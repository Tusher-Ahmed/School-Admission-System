using Npgsql;

namespace SchoolAdmission.Api.Data;

public static class SchemaInitializer
{
    // Any fixed number works; it just has to be the same in every backend process.
    private const long SchemaLockKey = 727_001;

    /// <summary>
    /// Runs the embedded schema.sql. The advisory lock stops two backend processes
    /// starting at the same moment from racing on CREATE ... IF NOT EXISTS.
    /// </summary>
    public static async Task EnsureSchemaAsync(string connectionString, CancellationToken ct = default)
    {
        var sql = await ReadSchemaAsync();

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        await using (var lockCmd = new NpgsqlCommand($"SELECT pg_advisory_lock({SchemaLockKey})", conn))
            await lockCmd.ExecuteNonQueryAsync(ct);
        try
        {
            await using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            await using var unlockCmd = new NpgsqlCommand($"SELECT pg_advisory_unlock({SchemaLockKey})", conn);
            await unlockCmd.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }

    private static async Task<string> ReadSchemaAsync()
    {
        var assembly = typeof(SchemaInitializer).Assembly;
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith("schema.sql"));
        await using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}

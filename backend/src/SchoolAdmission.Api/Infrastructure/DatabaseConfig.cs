namespace SchoolAdmission.Api.Infrastructure;

public static class DatabaseConfig
{
    public static string GetConnectionString(IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Database connection is not configured. Set the environment variable ConnectionStrings__Default " +
                "(or put it in backend/.env) - see README.");
        return connectionString;
    }
}

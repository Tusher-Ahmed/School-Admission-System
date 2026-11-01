using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Infrastructure;

DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException(
        "Database connection is not configured. Set the environment variable ConnectionStrings__Default " +
        "(or put it in backend/.env) - see README.");

builder.Services.AddDbContext<AdmissionDbContext>(o => o.UseNpgsql(connectionString));

var app = builder.Build();

await SchemaInitializer.EnsureSchemaAsync(connectionString);

app.MapGet("/health", async (AdmissionDbContext db) =>
    await db.Database.CanConnectAsync()
        ? Results.Ok(new { status = "ok", database = "ok" })
        : Results.Json(new { status = "degraded", database = "unreachable" }, statusCode: 503));

app.Run();

// Exposed so the test project can host the API with WebApplicationFactory.
public partial class Program;

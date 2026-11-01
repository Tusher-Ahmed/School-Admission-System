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

builder.Services.AddSingleton(AdmissionOptions.FromConfiguration(builder.Configuration));
builder.Services.AddDbContext<AdmissionDbContext>(o => o.UseNpgsql(connectionString));

// The React app runs on another port, so the browser needs CORS. X-Student-Id is a custom header.
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

await SchemaInitializer.EnsureSchemaAsync(connectionString);

app.UseExceptionHandler();
app.UseStatusCodePages(ApiErrors.WriteStatusCodeBody);
app.UseCors();

app.MapGet("/health", async (AdmissionDbContext db) =>
    await db.Database.CanConnectAsync()
        ? Results.Ok(new { status = "ok", database = "ok" })
        : Results.Json(new { status = "degraded", database = "unreachable" }, statusCode: 503));

app.Run();

// Exposed so the test project can host the API with WebApplicationFactory.
public partial class Program;

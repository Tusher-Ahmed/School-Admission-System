using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Features.Classes;
using SchoolAdmission.Api.Features.Holds;
using SchoolAdmission.Api.Infrastructure;

DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

// Configuration is read through DI so tests can override it; it is validated right after Build().
builder.Services.AddSingleton(sp => AdmissionOptions.FromConfiguration(sp.GetRequiredService<IConfiguration>()));
builder.Services.AddDbContext<AdmissionDbContext>((sp, o) =>
    o.UseNpgsql(DatabaseConfig.GetConnectionString(sp.GetRequiredService<IConfiguration>())));
builder.Services.AddScoped<HoldService>();
builder.Services.AddHostedService<HoldExpirySweeper>();

// The React app runs on another port, so the browser needs CORS. X-Student-Id is a custom header.
// Date is exposed so the UI can correct its countdown for a browser clock that is off.
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("Date")));

builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Fail fast on bad configuration before accepting any request.
var connectionString = DatabaseConfig.GetConnectionString(app.Configuration);
app.Services.GetRequiredService<AdmissionOptions>();
await SchemaInitializer.EnsureSchemaAsync(connectionString);

app.UseExceptionHandler();
app.UseStatusCodePages(ApiErrors.WriteStatusCodeBody);
app.UseCors();

app.MapGet("/health", async (AdmissionDbContext db) =>
    await db.Database.CanConnectAsync()
        ? Results.Ok(new { status = "ok", database = "ok" })
        : Results.Json(new { status = "degraded", database = "unreachable" }, statusCode: 503));

app.MapClassEndpoints();
app.MapHoldEndpoints();

app.Run();

// Exposed so the test project can host the API with WebApplicationFactory.
public partial class Program;

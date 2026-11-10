using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Endpoints;
using SchoolAdmission.Api.Infrastructure;
using SchoolAdmission.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings__Default is not configured.");

builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connectionString));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(HoldOptions.FromConfiguration(builder.Configuration));
builder.Services.AddScoped<AdmissionService>();

// The React app runs on another origin (localhost:3000), so allow cross-origin calls.
// "Date" is exposed so the UI can line its expiry countdown up with the server's clock.
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("Date")));

var app = builder.Build();

// CORS first, so error responses from the middleware below also carry CORS headers.
app.UseCors();
app.UseMiddleware<ApiExceptionMiddleware>();

await DbInitializer.InitializeAsync(app.Services, app.Logger);

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapAdmissionEndpoints();

app.Run();

// Exposed so integration tests can use WebApplicationFactory<Program>.
public partial class Program { }

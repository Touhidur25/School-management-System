using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings__Default is not configured.");

builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connectionString));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(HoldOptions.FromConfiguration(builder.Configuration));

// The React app runs on another origin (localhost:3000), so allow cross-origin calls.
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseCors();

await DbInitializer.InitializeAsync(app.Services, app.Logger);

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Class and hold endpoints are added in the next steps.

app.Run();

// Exposed so integration tests can use WebApplicationFactory<Program>.
public partial class Program { }

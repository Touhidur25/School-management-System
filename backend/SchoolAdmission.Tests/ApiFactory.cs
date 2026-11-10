using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace SchoolAdmission.Tests;

/// <summary>
/// Starts the real API in-process against a real PostgreSQL database (the seat locking
/// relies on PostgreSQL row locks, so an in-memory fake would prove nothing).
/// The database comes from TEST_DB_CONNECTION; see docker-compose.test.yml.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Hold lifetime used by all tests.</summary>
    public const int HoldSeconds = 60;

    public TestClock Clock { get; } = new();

    public ApiFactory()
    {
        var connection = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=school_admission_test;Username=school;Password=school_pw";

        // Program.cs reads configuration while building the host, and environment
        // variables are the one source that is always visible at that point.
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", connection);
        Environment.SetEnvironmentVariable("HOLD_DURATION_SECONDS", HoldSeconds.ToString());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Registered last, so it wins over TimeProvider.System from Program.cs.
            services.AddSingleton<TimeProvider>(Clock);
        });
    }
}

/// <summary>
/// All tests share one API instance and one database, and run one at a time. Every test
/// creates its own classes, so tests do not depend on each other's data.
/// </summary>
[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}

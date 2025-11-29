using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Respawn;
using Testcontainers.PostgreSql;
using VoiceByAuribus_API.Shared.Infrastructure.Data;
using VoiceByAuribus_API.Shared.Interfaces;
using VoiceByAuribus_API.Tests.Helpers.MockServices;

namespace VoiceByAuribus_API.Tests.Helpers;

/// <summary>
/// Database fixture for integration tests using Testcontainers.
/// Provides a real PostgreSQL database in a Docker container and handles cleanup between tests.
/// </summary>
public class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;
    private Respawner? _respawner;
    private WebApplicationFactory<Program>? _factory;

    public DatabaseFixture()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("voicebyauribus_test")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .WithCleanUp(true)
            .Build();
    }

    /// <summary>
    /// Gets the PostgreSQL connection string for the test container.
    /// </summary>
    public string ConnectionString => _postgresContainer.GetConnectionString();

    /// <summary>
    /// Initializes the test container and database.
    /// Called automatically by xUnit before tests run.
    /// </summary>
    public async Task InitializeAsync()
    {
        // Start PostgreSQL container
        await _postgresContainer.StartAsync();

        // Initialize Respawner for database cleanup
        using var connection = new Npgsql.NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = ["__EFMigrationsHistory"]
        });

        // Run migrations to create database schema
        await RunMigrationsAsync();
    }

    /// <summary>
    /// Cleans up the test container.
    /// Called automatically by xUnit after all tests complete.
    /// </summary>
    public async Task DisposeAsync()
    {
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }
        
        await _postgresContainer.DisposeAsync();
    }

    /// <summary>
    /// Creates a WebApplicationFactory for integration testing with test database.
    /// </summary>
    public WebApplicationFactory<Program> CreateFactory()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Test");
                
                builder.ConfigureTestServices(services =>
                {
                    // Replace real database with test container
                    services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                    services.AddDbContext<ApplicationDbContext>(options =>
                    {
                        options.UseNpgsql(ConnectionString);
                    });

                    // Mock AWS services (S3, SQS) to avoid real AWS calls
                    services.RemoveAll<ISqsService>();
                    services.AddScoped<ISqsService, MockSqsService>();

                    services.RemoveAll<IS3PresignedUrlService>();
                    services.AddScoped<IS3PresignedUrlService, MockS3PresignedUrlService>();

                    // Replace IDateTimeProvider with fake for predictable tests
                    services.RemoveAll<IDateTimeProvider>();
                    services.AddScoped<IDateTimeProvider, FakeDateTimeProvider>();
                });
            });

        return _factory;
    }

    /// <summary>
    /// Creates an HTTP client for making requests to the test server.
    /// </summary>
    public HttpClient CreateClient()
    {
        var factory = CreateFactory();
        return factory.CreateClient();
    }

    /// <summary>
    /// Creates a DbContext instance for direct database access in tests.
    /// </summary>
    public ApplicationDbContext CreateDbContext()
    {
        var factory = CreateFactory();
        var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }

    /// <summary>
    /// Resets the database to a clean state by deleting all data.
    /// Call this between tests to ensure test isolation.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        if (_respawner == null)
        {
            throw new InvalidOperationException("Respawner not initialized. Call InitializeAsync first.");
        }

        using var connection = new Npgsql.NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);
    }

    /// <summary>
    /// Runs EF Core migrations to create the database schema.
    /// </summary>
    private async Task RunMigrationsAsync()
    {
        var factory = CreateFactory();
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}

/// <summary>
/// Fake date/time provider for predictable testing.
/// </summary>
public class FakeDateTimeProvider : IDateTimeProvider
{
    private DateTime _utcNow = new DateTime(2025, 11, 25, 12, 0, 0, DateTimeKind.Utc);

    public DateTime UtcNow => _utcNow;

    public void SetUtcNow(DateTime dateTime)
    {
        _utcNow = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
    }
}

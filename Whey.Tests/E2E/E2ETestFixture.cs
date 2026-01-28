using Grpc.Net.Client;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Octokit;
using Whey.Infra.Data;
using Whey.Infra.Workers;
using Whey.Server;

namespace Whey.Tests.E2E;

/// E2E test fixture that uses real PostgreSQL and real services.
/// Only background jobs (JobInitializer) are disabled to avoid long-running operations.
public sealed class E2ETestFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
	private readonly string _connectionString;

	public E2ETestFixture()
	{
		// Use environment variable or default to local docker-compose setup
		_connectionString = Environment.GetEnvironmentVariable("E2E_CONNECTION_STRING")
			?? "Host=localhost;Port=5433;Database=whey_e2e;Username=postgres;Password=postgres";
	}

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.ConfigureServices(services =>
		{
			// Remove existing DbContext registration (InMemory from other tests)
			services.RemoveAll<DbContextOptions<WheyContext>>();
			services.RemoveAll<WheyContext>();

			// Use real PostgreSQL
			services.AddDbContext<WheyContext>(options =>
			{
				options.UseNpgsql(_connectionString);
			});

			// Use memory distributed cache (Redis replacement for now)
			services.RemoveAll<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
			services.AddDistributedMemoryCache();

			// Disable JobInitializer background service to prevent Quartz job scheduling
			// This avoids long-running background operations during E2E tests
			var jobInitializerDescriptor = services.SingleOrDefault(
				d => d.ImplementationType == typeof(JobInitializer));
			if (jobInitializerDescriptor != null)
			{
				services.Remove(jobInitializerDescriptor);
			}

			// Use real GitHubClient (unauthenticated, 60 req/hr rate limit)
			services.RemoveAll<GitHubClient>();
			services.AddSingleton(new GitHubClient(new ProductHeaderValue("WheyE2ETests")));
		});
	}

	public GrpcChannel CreateGrpcChannel()
	{
		var client = CreateClient();
		return GrpcChannel.ForAddress(client.BaseAddress!, new GrpcChannelOptions
		{
			HttpClient = client
		});
	}

	public WheyContext CreateDbContext()
	{
		var scope = Services.CreateScope();
		return scope.ServiceProvider.GetRequiredService<WheyContext>();
	}

	public async ValueTask InitializeAsync()
	{
		// Ensure database is created and migrations are applied
		using var scope = Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<WheyContext>();
		await db.Database.EnsureCreatedAsync();
	}

	public new async ValueTask DisposeAsync()
	{
		// Clean up database after all tests
		using var scope = Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<WheyContext>();
		await db.Database.EnsureDeletedAsync();
		await base.DisposeAsync();
	}
}

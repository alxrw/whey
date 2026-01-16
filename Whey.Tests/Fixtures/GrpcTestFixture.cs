using Grpc.Net.Client;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Octokit;
using Whey.Infra.Data;
using Whey.Server;

namespace Whey.Tests.Fixtures;

public sealed class GrpcTestFixture : WebApplicationFactory<Program>
{
	private readonly string _dbName = Guid.NewGuid().ToString();

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.ConfigureServices(services =>
		{
			// Remove existing DbContext registration
			services.RemoveAll<DbContextOptions<WheyContext>>();
			services.RemoveAll<WheyContext>();

			// Add InMemory database
			services.AddDbContext<WheyContext>(options =>
			{
				options.UseInMemoryDatabase(_dbName);
			});

			// Add a default GitHubClient for tests that don't hit GitHub
			// Tests that need to mock GitHub should do so at a different level
			services.RemoveAll<GitHubClient>();
			services.AddSingleton(new GitHubClient(new ProductHeaderValue("WheyTests")));

			services.AddDistributedMemoryCache();
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

	public WheyContext GetDbContext()
	{
		var scope = Services.CreateScope();
		return scope.ServiceProvider.GetRequiredService<WheyContext>();
	}
}

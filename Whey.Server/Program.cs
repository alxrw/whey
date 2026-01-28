using Quartz;
using Serilog;
using Whey.Infra.Extensions;
using Whey.Infra.Services;
using Whey.Infra.Workers;
using Whey.Server.Interceptors;
using Whey.Server.Grpc;
using System.Threading.RateLimiting;
using Octokit;
using Whey.Infra.Configuration;
using Microsoft.Extensions.Options;

namespace Whey.Server;

public sealed partial class Program
{
	public static void Main(string[] args)
	{
		var builder = WebApplication.CreateBuilder(args);

		// add logging
		Log.Logger = new LoggerConfiguration()
			.MinimumLevel.Debug()
			// TODO: change this to write to file
			// or maybe do smth with azure vms?
			.WriteTo.Console()
			.CreateLogger();
		builder.Services.AddLogging(builder =>
		{
			builder.ClearProviders();
			builder.AddSerilog(dispose: true);
		});

		// validate env vars and/or options
		var binOpts = builder.Services.AddOptions<BinStorageServiceOptions>()
			.Bind(builder.Configuration.GetSection("BinStorageService"))
			.ValidateDataAnnotations()
			.ValidateOnStart();

		var ghOpts = builder.Services.AddOptions<GitHubClientOptions>()
			.Bind(builder.Configuration.GetSection("Octokit"))
			.ValidateDataAnnotations()
			.ValidateOnStart();

		builder.Services.AddSingleton(sp =>
		{
			var globalLimiter = PartitionedRateLimiter.Create<string, string>(resource =>
			{
				return RateLimitPartition.GetConcurrencyLimiter("global", _ => new ConcurrencyLimiterOptions
				{
					PermitLimit = 500,
					QueueLimit = 0,
					QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
				});
			});

			var userLimiter = PartitionedRateLimiter.Create<string, string>(resource =>
			{
				return RateLimitPartition.GetTokenBucketLimiter(resource, _ => new TokenBucketRateLimiterOptions
				{
					TokenLimit = 40,
					QueueLimit = 10,
					TokensPerPeriod = 5,
					ReplenishmentPeriod = TimeSpan.FromSeconds(1),
					AutoReplenishment = true,
					QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
				});
			});

			return PartitionedRateLimiter.CreateChained(globalLimiter, userLimiter);
		});

		// add gRPC to DI
		builder.Services.AddTransient<AuthenticationInterceptor>();
		builder.Services.AddTransient<RateLimiterInterceptor>();

		builder.Services.AddSingleton<PackageTrackerServiceImpl>();
		builder.Services.AddSingleton<RegistrationServiceImpl>();
		builder.Services.AddSingleton<PackageRetrieverServiceImpl>();

		builder.Services.AddGrpc(opts =>
		{
			opts.Interceptors.Add<AuthenticationInterceptor>();
			opts.Interceptors.Add<RateLimiterInterceptor>();
		});

		// add infra services
		builder.Services.AddQuartz();
		builder.Services.AddQuartzHostedService(opts =>
		{
			opts.WaitForJobsToComplete = true;
		});
		builder.Services.AddWheyInfra(builder.Configuration); // adds postgres
		builder.Services.AddHttpClient();

		builder.Services.AddTransient<IApiSchedulingService, ApiSchedulingService>();
		builder.Services.AddSingleton<IBinStorageService, BinStorageService>();
		builder.Services.AddTransient<IAssetMappingService, AssetMappingService>();
		builder.Services.AddTransient<IPackageSyncService, PackageSyncService>();

		// initialize quartz jobs
		builder.Services.AddHostedService<JobInitializer>();

		builder.Services.AddSingleton<IGitHubClient>(sp =>
		{
			var opts = sp.GetRequiredService<IOptions<GitHubClientOptions>>().Value;
			var header = new ProductHeaderValue(opts.UserAgent);

			return new GitHubClient(header)
			{
				Credentials = new Credentials(opts.Token),
			};
		});
		builder.Services.AddDistributedMemoryCache();

		var app = builder.Build();

		// Configure the HTTP request pipeline.
		app.MapGrpcService<RegistrationServiceImpl>();
		app.MapGrpcService<PackageTrackerServiceImpl>().RequireAuthorization();
		app.MapGrpcService<PackageRetrieverServiceImpl>();
		app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

		app.Run();
	}
}

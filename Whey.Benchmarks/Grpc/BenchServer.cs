using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using NSec.Cryptography;
using Octokit;
using Whey.Benchmarks.Scheduling;
using Whey.Infra.Data;
using Whey.Infra.Workers;
using Whey.Server;
using Whey.Server.Interceptors;
using Whey.Server.Proto;

namespace Whey.Benchmarks.Grpc;

// The real Whey.Server pipeline hosted in-process on TestServer with a SQLite in-memory database.
// Excludes network, TLS and Postgres latency.
public sealed class BenchServer : WebApplicationFactory<Program>
{
	private readonly string _connStr = $"Data Source=file:{Guid.NewGuid():N}?mode=memory&cache=shared";
	private readonly Microsoft.Data.Sqlite.SqliteConnection _keepAlive; // keeps the shared in-memory DB alive

	public BenchServer()
	{
		// Quartz and Serilog hold static logger state from the previous in-process host, which is disposed by now.
		Quartz.Logging.LogProvider.SetCurrentLogProvider(null!);
		Serilog.Log.Logger = Serilog.Core.Logger.None;
		_keepAlive = new Microsoft.Data.Sqlite.SqliteConnection(_connStr);
		_keepAlive.Open();
	}

	public void EnsureDatabase()
	{
		using var scope = Services.CreateScope();
		scope.ServiceProvider.GetRequiredService<WheyContext>().Database.EnsureCreated();
	}

	// Registers clients straight in the database (the registration RPCs are exercised by the auth-probes suite).
	public void SeedClients(IEnumerable<PopClient> clients, string package = "o/r")
	{
		using var scope = Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<WheyContext>();
		foreach (var c in clients)
		{
			db.Clients.Add(new Whey.Core.Models.WheyClient
			{
				Id = Guid.CreateVersion7(),
				PublicKey = AuthenticationInterceptor.EncodePublicKey(c.PublicKey),
				TokenExpiry = DateTimeOffset.UtcNow.AddDays(30),
				RegisteredAt = DateTimeOffset.UtcNow,
			});
		}
		var id = Guid.CreateVersion7();
		var parts = package.Split('/');
		if (!db.Packages.Any(p => p.Owner == parts[0] && p.Repo == parts[1]))
		{
			db.Packages.Add(new Whey.Core.Models.Package { Id = id, Owner = parts[0], Repo = parts[1], Version = "v1", LastReleased = DateTimeOffset.UtcNow });
			var stats = new Whey.Core.Models.Stats.PackageStatistics { Id = id, PackageId = id };
			stats.Installs.Track();
			db.PackageStats.Add(stats);
		}
		db.SaveChanges();
	}

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.UseSetting("BinStorageService:AccountName", "bench");
		builder.UseSetting("BinStorageService:ContainerName", "bench");
		builder.UseSetting("Octokit:Token", "bench");
		builder.UseSetting("Octokit:UserAgent", "bench");
		if (Environment.GetEnvironmentVariable("BENCH_LOG") is null) builder.ConfigureLogging(l => l.ClearProviders());
		builder.ConfigureServices(services =>
		{
			services.Configure<global::Grpc.AspNetCore.Server.GrpcServiceOptions>(o => o.EnableDetailedErrors = true);

			// EF 9 keeps the provider configuration (Npgsql) in a separate registration; drop it too.
			foreach (var d in services.Where(d => d.ServiceType.IsGenericType && d.ServiceType.Name.StartsWith("IDbContextOptionsConfiguration")).ToList())
				services.Remove(d);
			services.RemoveAll<DbContextOptions<WheyContext>>();
			services.RemoveAll<WheyContext>();
			services.AddSingleton(new DbContextOptionsBuilder<WheyContext>().UseSqlite(_connStr).Options);
			services.AddScoped<WheyContext>(sp => new SqliteWheyContext(sp.GetRequiredService<DbContextOptions<WheyContext>>()));

			services.RemoveAll<IGitHubClient>();
			services.AddSingleton<IGitHubClient>(new GitHubClient(new ProductHeaderValue("WheyBench")));
			var init = services.SingleOrDefault(d => d.ImplementationType == typeof(JobInitializer));
			if (init is not null) services.Remove(init);
		});
	}

	public GrpcChannel CreateChannel()
	{
		var http = CreateClient();
		return GrpcChannel.ForAddress(http.BaseAddress!, new GrpcChannelOptions { HttpClient = http });
	}
}

// A client with a fresh Ed25519 key: registers through the real Challenge/Register flow and signs requests
// the way AuthenticationInterceptor verifies them.
public sealed class PopClient
{
	public Key Key { get; }
	public byte[] PublicKey { get; }

	public PopClient()
	{
		Key = NSec.Cryptography.Key.Create(SignatureAlgorithm.Ed25519, new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });
		PublicKey = Key.Export(KeyBlobFormat.RawPublicKey);
	}

	public async Task RegisterAsync(RegistrationService.RegistrationServiceClient reg)
	{
		var ch = await reg.ChallengeAsync(new ChallengeRequest());
		var intent = new RegisterIntent
		{
			PublicKey = ByteString.CopyFrom(PublicKey),
			Challenge = ByteString.CopyFromUtf8(ch.Nonce),
			Version = "1.0.0",
			Platform = Whey.Server.Proto.Platform.Linux,
			Purpose = "register",
			RpcMethod = "/whey.WheyRegistration/Register",
		};
		await reg.RegisterAsync(new RegisterRequest
		{
			PublicKey = ByteString.CopyFrom(PublicKey),
			PayloadSignature = ByteString.CopyFrom(SignatureAlgorithm.Ed25519.Sign(Key, intent.ToByteArray())),
			Version = "1.0.0",
			Platform = Whey.Server.Proto.Platform.Linux,
			Nonce = ByteString.CopyFromUtf8(ch.Nonce),
		});
	}

	// Metadata for an authenticated call. Overrides let probes forge, replay or age a request.
	public Metadata Sign(string method, IMessage request, string? nonce = null, long? unixSeconds = null, IMessage? signedBody = null)
	{
		nonce ??= Guid.NewGuid().ToString("N");
		string ts = (unixSeconds ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ToString();
		string canon = AuthenticationInterceptor.BuildCanonicalString(method, nonce, ts, (signedBody ?? request).ToByteArray());
		var sig = SignatureAlgorithm.Ed25519.Sign(Key, System.Text.Encoding.UTF8.GetBytes(canon));
		return new Metadata
		{
			{ AuthenticationInterceptor.PublicKeyHeader, PublicKey },
			{ AuthenticationInterceptor.SignatureHeader, sig },
			{ AuthenticationInterceptor.NonceHeader, nonce },
			{ AuthenticationInterceptor.TimestampHeader, ts },
		};
	}

	public const string ReportInstallMethod = "/package_tracker.v1.PackageTrackerService/ReportInstall";
}

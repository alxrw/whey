using AwesomeAssertions;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSec.Cryptography;
using Whey.Infra.Data;
using Whey.Server.Proto;

namespace Whey.Tests.E2E;

// End-to-end tests that exercise the full gRPC flow with real PostgreSQL.
// These tests are designed to run on an Azure VM or local Docker environment.
public class FullFlowE2ETests : IClassFixture<E2ETestFixture>, IAsyncLifetime
{
	private readonly E2ETestFixture _fixture;
	private readonly GrpcChannel _channel;
	private readonly RegistrationService.RegistrationServiceClient _registrationClient;
	private readonly PackageTrackerService.PackageTrackerServiceClient _trackerClient;

	// Test repository configuration - use environment variables or defaults
	private readonly string _testRepoOwner;
	private readonly string _testRepoName;

	public FullFlowE2ETests(E2ETestFixture fixture)
	{
		_fixture = fixture;
		_channel = fixture.CreateGrpcChannel();
		_registrationClient = new RegistrationService.RegistrationServiceClient(_channel);
		_trackerClient = new PackageTrackerService.PackageTrackerServiceClient(_channel);

		// Configure test repository - should be a real GitHub repo with at least one release
		_testRepoOwner = Environment.GetEnvironmentVariable("E2E_GITHUB_TEST_REPO_OWNER") ?? "octocat";
		_testRepoName = Environment.GetEnvironmentVariable("E2E_GITHUB_TEST_REPO_NAME") ?? "Hello-World";
	}

	public async ValueTask InitializeAsync()
	{
		// Clean up test data before each test
		using var scope = _fixture.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<WheyContext>();

		// Clear all data from previous test runs
		db.Clients.RemoveRange(db.Clients);
		db.PackageStats.RemoveRange(db.PackageStats);
		db.Packages.RemoveRange(db.Packages);
		await db.SaveChangesAsync();
	}

	public ValueTask DisposeAsync()
	{
		GC.SuppressFinalize(this);
		_channel.Dispose();
		return ValueTask.CompletedTask;
	}

	[Fact]
	public async Task Challenge_ReturnsNonceWithFutureExpiry()
	{
		var response = await _registrationClient.ChallengeAsync(new ChallengeRequest(), cancellationToken: TestContext.Current.CancellationToken);

		response.Nonce.Should().NotBeNullOrEmpty();
		response.ExpiresAt.Should().NotBeNull();
		response.ExpiresAt.ToDateTimeOffset().Should().BeAfter(DateTimeOffset.UtcNow);
	}

	[Fact]
	public async Task Challenge_ReturnsUniqueNonces()
	{
		var response1 = await _registrationClient.ChallengeAsync(new ChallengeRequest(), cancellationToken: TestContext.Current.CancellationToken);
		var response2 = await _registrationClient.ChallengeAsync(new ChallengeRequest(), cancellationToken: TestContext.Current.CancellationToken);
		var response3 = await _registrationClient.ChallengeAsync(new ChallengeRequest(), cancellationToken: TestContext.Current.CancellationToken);

		// All nonces should be unique
		var nonces = new[] { response1.Nonce, response2.Nonce, response3.Nonce };
		nonces.Distinct().Should().HaveCount(3);
	}

	[Fact]
	public async Task FullRegistrationFlow_CreatesClientInDatabase()
	{
		// Step 1: Get challenge nonce
		var challengeResponse = await _registrationClient.ChallengeAsync(new ChallengeRequest(), cancellationToken: TestContext.Current.CancellationToken);

		// Step 2: Generate Ed25519 key pair
		var key = Key.Create(SignatureAlgorithm.Ed25519, new KeyCreationParameters
		{
			ExportPolicy = KeyExportPolicies.AllowPlaintextExport
		});
		var publicKeyBytes = key.Export(KeyBlobFormat.RawPublicKey);

		// Step 3: Create and sign RegisterIntent
		var intent = new RegisterIntent
		{
			PublicKey = ByteString.CopyFrom(publicKeyBytes),
			Challenge = ByteString.CopyFromUtf8(challengeResponse.Nonce),
			Version = "1.0.0",
			Platform = Platform.Linux,
			Purpose = "register",
			RpcMethod = "/whey.WheyRegistration/Register",
		};

		var signature = SignatureAlgorithm.Ed25519.Sign(key, intent.ToByteArray());

		// Step 4: Call Register
		var registerRequest = new RegisterRequest
		{
			PublicKey = ByteString.CopyFrom(publicKeyBytes),
			PayloadSignature = ByteString.CopyFrom(signature),
			Version = "1.0.0",
			Platform = Platform.Linux,
			Nonce = ByteString.CopyFromUtf8(challengeResponse.Nonce),
		};

		var registerResponse = await _registrationClient.RegisterAsync(registerRequest, cancellationToken: TestContext.Current.CancellationToken);

		// Step 5: Verify client was created in database
		registerResponse.Should().NotBeNull();

		using var scope = _fixture.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<WheyContext>();
		var clients = await db.Clients.ToListAsync(TestContext.Current.CancellationToken);

		clients.Should().HaveCount(1);
		clients[0].Version.Should().Be("1.0.0");
		clients[0].Platform.Should().Be("linux");
	}

	[Fact]
	public async Task Register_WithInvalidNonce_ReturnsUnauthenticated()
	{
		var key = Key.Create(SignatureAlgorithm.Ed25519, new KeyCreationParameters
		{
			ExportPolicy = KeyExportPolicies.AllowPlaintextExport
		});
		var publicKeyBytes = key.Export(KeyBlobFormat.RawPublicKey);

		var intent = new RegisterIntent
		{
			PublicKey = ByteString.CopyFrom(publicKeyBytes),
			Challenge = ByteString.CopyFromUtf8("invalid-nonce-that-does-not-exist"),
			Version = "1.0.0",
			Platform = Platform.Linux,
			Purpose = "register",
			RpcMethod = "/whey.WheyRegistration/Register",
		};

		var signature = SignatureAlgorithm.Ed25519.Sign(key, intent.ToByteArray());

		var request = new RegisterRequest
		{
			PublicKey = ByteString.CopyFrom(publicKeyBytes),
			PayloadSignature = ByteString.CopyFrom(signature),
			Version = "1.0.0",
			Platform = Platform.Linux,
			Nonce = ByteString.CopyFromUtf8("invalid-nonce-that-does-not-exist"),
		};

		var act = async () => await _registrationClient.RegisterAsync(request);

		var ex = await act.Should().ThrowAsync<RpcException>();
		ex.Which.StatusCode.Should().Be(StatusCode.Unauthenticated);
	}

	[Fact]
	public async Task Register_WithInvalidSignature_ReturnsUnauthenticated()
	{
		// Get valid nonce
		var challengeResponse = await _registrationClient.ChallengeAsync(new ChallengeRequest(), cancellationToken: TestContext.Current.CancellationToken);

		var key = Key.Create(SignatureAlgorithm.Ed25519, new KeyCreationParameters
		{
			ExportPolicy = KeyExportPolicies.AllowPlaintextExport
		});
		var publicKeyBytes = key.Export(KeyBlobFormat.RawPublicKey);

		// Use invalid signature (all zeros)
		var request = new RegisterRequest
		{
			PublicKey = ByteString.CopyFrom(publicKeyBytes),
			PayloadSignature = ByteString.CopyFrom(new byte[64]),
			Version = "1.0.0",
			Platform = Platform.Linux,
			Nonce = ByteString.CopyFromUtf8(challengeResponse.Nonce),
		};

		var act = async () => await _registrationClient.RegisterAsync(request);

		var ex = await act.Should().ThrowAsync<RpcException>();
		ex.Which.StatusCode.Should().Be(StatusCode.Unauthenticated);
	}

	[Fact]
	public async Task Register_NonceCanOnlyBeUsedOnce()
	{
		// Get a single nonce
		var challengeResponse = await _registrationClient.ChallengeAsync(new ChallengeRequest(), cancellationToken: TestContext.Current.CancellationToken);

		// Create two different key pairs
		var key1 = Key.Create(SignatureAlgorithm.Ed25519, new KeyCreationParameters
		{
			ExportPolicy = KeyExportPolicies.AllowPlaintextExport
		});
		var publicKey1 = key1.Export(KeyBlobFormat.RawPublicKey);

		var key2 = Key.Create(SignatureAlgorithm.Ed25519, new KeyCreationParameters
		{
			ExportPolicy = KeyExportPolicies.AllowPlaintextExport
		});
		var publicKey2 = key2.Export(KeyBlobFormat.RawPublicKey);

		// First registration should succeed
		var intent1 = new RegisterIntent
		{
			PublicKey = ByteString.CopyFrom(publicKey1),
			Challenge = ByteString.CopyFromUtf8(challengeResponse.Nonce),
			Version = "1.0.0",
			Platform = Platform.Linux,
			Purpose = "register",
			RpcMethod = "/whey.WheyRegistration/Register",
		};
		var signature1 = SignatureAlgorithm.Ed25519.Sign(key1, intent1.ToByteArray());

		await _registrationClient.RegisterAsync(new RegisterRequest
		{
			PublicKey = ByteString.CopyFrom(publicKey1),
			PayloadSignature = ByteString.CopyFrom(signature1),
			Version = "1.0.0",
			Platform = Platform.Linux,
			Nonce = ByteString.CopyFromUtf8(challengeResponse.Nonce),
		}, cancellationToken: TestContext.Current.CancellationToken);

		// Second registration with same nonce should fail
		var intent2 = new RegisterIntent
		{
			PublicKey = ByteString.CopyFrom(publicKey2),
			Challenge = ByteString.CopyFromUtf8(challengeResponse.Nonce),
			Version = "1.0.0",
			Platform = Platform.Linux,
			Purpose = "register",
			RpcMethod = "/whey.WheyRegistration/Register",
		};
		var signature2 = SignatureAlgorithm.Ed25519.Sign(key2, intent2.ToByteArray());

		var act = async () => await _registrationClient.RegisterAsync(new RegisterRequest
		{
			PublicKey = ByteString.CopyFrom(publicKey2),
			PayloadSignature = ByteString.CopyFrom(signature2),
			Version = "1.0.0",
			Platform = Platform.Linux,
			Nonce = ByteString.CopyFromUtf8(challengeResponse.Nonce),
		});

		var ex = await act.Should().ThrowAsync<RpcException>();
		ex.Which.StatusCode.Should().Be(StatusCode.Unauthenticated);
	}

	// Note: The following tests require authentication which isn't fully implemented
	// in the test setup. They're marked as Skip for now but show the intended flow.

	[Fact(Skip = "Requires authentication interceptor setup for authorized endpoints")]
	public async Task EnsureTracked_WithRealGitHubRepo_CreatesPackage()
	{
		// This test would call the real GitHub API
		// PackageTrackerService.EnsureTracked requires authorization

		var request = new EnsureTrackedRequest
		{
			Owner = _testRepoOwner,
			Repo = _testRepoName,
			Platform = "linux",
		};

		var response = await _trackerClient.EnsureTrackedAsync(request, cancellationToken: TestContext.Current.CancellationToken);

		response.Should().NotBeNull();

		// Verify package was created in database
		using var scope = _fixture.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<WheyContext>();
		var package = await db.Packages.FirstOrDefaultAsync(
			p => p.Owner == _testRepoOwner && p.Repo == _testRepoName,
			cancellationToken: TestContext.Current.CancellationToken);

		package.Should().NotBeNull();
		package.Version.Should().NotBeNullOrEmpty();
	}

	[Fact(Skip = "Requires authentication interceptor setup for authorized endpoints")]
	public async Task ReportInstall_TracksStatistics()
	{
		// First ensure the package is tracked
		await _trackerClient.EnsureTrackedAsync(new EnsureTrackedRequest
		{
			Owner = _testRepoOwner,
			Repo = _testRepoName,
			Platform = "linux",
		}, cancellationToken: TestContext.Current.CancellationToken);

		// Report an install
		var response = await _trackerClient.ReportInstallAsync(new ReportInstallRequest
		{
			Owner = _testRepoOwner,
			Repo = _testRepoName,
		}, cancellationToken: TestContext.Current.CancellationToken);

		response.Should().NotBeNull();

		// Verify statistics were updated
		using var scope = _fixture.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<WheyContext>();
		var package = await db.Packages.FirstOrDefaultAsync(
			p => p.Owner == _testRepoOwner && p.Repo == _testRepoName,
			cancellationToken: TestContext.Current.CancellationToken);

		var stats = await db.PackageStats.FirstOrDefaultAsync(s => s.PackageId == package!.Id,
			cancellationToken: TestContext.Current.CancellationToken);
		stats.Should().NotBeNull();
	}
}

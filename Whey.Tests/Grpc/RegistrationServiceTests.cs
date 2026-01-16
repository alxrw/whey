using AwesomeAssertions;
using Google.Protobuf;
using NSec.Cryptography;
using Whey.Server.Proto;

namespace Whey.Tests.Grpc;

public class RegistrationServiceTests
{
	[Fact]
	public void Ed25519_CanCreateKeyPair()
	{
		var key = Key.Create(SignatureAlgorithm.Ed25519, new KeyCreationParameters
		{
			ExportPolicy = KeyExportPolicies.AllowPlaintextExport
		});

		var publicKey = key.Export(KeyBlobFormat.RawPublicKey);

		key.Should().NotBeNull();
		publicKey.Should().HaveCount(32); // Ed25519 public keys are 32 bytes
	}

	[Fact]
	public void Ed25519_CanSignAndVerify()
	{
		var key = Key.Create(SignatureAlgorithm.Ed25519, new KeyCreationParameters
		{
			ExportPolicy = KeyExportPolicies.AllowPlaintextExport
		});

		var publicKeyBytes = key.Export(KeyBlobFormat.RawPublicKey);
		var data = System.Text.Encoding.UTF8.GetBytes("test message");

		var signature = SignatureAlgorithm.Ed25519.Sign(key, data);

		signature.Should().HaveCount(64); // Ed25519 signatures are 64 bytes

		// Verify with imported public key
		var publicKey = PublicKey.Import(SignatureAlgorithm.Ed25519, publicKeyBytes, KeyBlobFormat.RawPublicKey);
		var isValid = SignatureAlgorithm.Ed25519.Verify(publicKey, data, signature);

		isValid.Should().BeTrue();
	}

	[Fact]
	public void Ed25519_InvalidSignature_FailsVerification()
	{
		var key = Key.Create(SignatureAlgorithm.Ed25519, new KeyCreationParameters
		{
			ExportPolicy = KeyExportPolicies.AllowPlaintextExport
		});

		var publicKeyBytes = key.Export(KeyBlobFormat.RawPublicKey);
		var data = System.Text.Encoding.UTF8.GetBytes("test message");
		var invalidSignature = new byte[64]; // All zeros is invalid

		var publicKey = PublicKey.Import(SignatureAlgorithm.Ed25519, publicKeyBytes, KeyBlobFormat.RawPublicKey);
		var isValid = SignatureAlgorithm.Ed25519.Verify(publicKey, data, invalidSignature);

		isValid.Should().BeFalse();
	}

	[Fact]
	public void RegisterIntent_CanSerializeToByteArray()
	{
		var key = Key.Create(SignatureAlgorithm.Ed25519, new KeyCreationParameters
		{
			ExportPolicy = KeyExportPolicies.AllowPlaintextExport
		});

		var publicKey = key.Export(KeyBlobFormat.RawPublicKey);

		var intent = new RegisterIntent
		{
			PublicKey = ByteString.CopyFrom(publicKey),
			Challenge = ByteString.CopyFromUtf8("test-nonce"),
			Version = "1.0.0",
			Platform = Platform.Linux,
			Purpose = "register",
			RpcMethod = "/whey.WheyRegistration/Register",
		};

		var bytes = intent.ToByteArray();

		bytes.Should().NotBeEmpty();
		bytes.Length.Should().BeGreaterThan(50);
	}

	[Fact]
	public void RegisterRequest_CanBeConstructed()
	{
		var key = Key.Create(SignatureAlgorithm.Ed25519, new KeyCreationParameters
		{
			ExportPolicy = KeyExportPolicies.AllowPlaintextExport
		});

		var publicKey = key.Export(KeyBlobFormat.RawPublicKey);
		var nonce = "test-nonce-12345";

		var intent = new RegisterIntent
		{
			PublicKey = ByteString.CopyFrom(publicKey),
			Challenge = ByteString.CopyFromUtf8(nonce),
			Version = "1.0.0",
			Platform = Platform.Linux,
			Purpose = "register",
			RpcMethod = "/whey.WheyRegistration/Register",
		};

		var signature = SignatureAlgorithm.Ed25519.Sign(key, intent.ToByteArray());

		var request = new RegisterRequest
		{
			PublicKey = ByteString.CopyFrom(publicKey),
			PayloadSignature = ByteString.CopyFrom(signature),
			Version = "1.0.0",
			Platform = Platform.Linux,
			Nonce = ByteString.CopyFromUtf8(nonce),
		};

		request.Should().NotBeNull();
		request.PublicKey.Length.Should().Be(32);
		request.PayloadSignature.Length.Should().Be(64);
		request.Version.Should().Be("1.0.0");
		request.Platform.Should().Be(Platform.Linux);
	}

	[Fact]
	public void ChallengeResponse_CanRepresentFutureExpiry()
	{
		var response = new ChallengeResponse
		{
			Nonce = Guid.NewGuid().ToString("N"),
			ExpiresAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(
				DateTimeOffset.UtcNow.AddMinutes(5)),
		};

		response.Nonce.Should().NotBeNullOrEmpty();
		response.ExpiresAt.ToDateTimeOffset().Should().BeAfter(DateTimeOffset.UtcNow);
	}
}

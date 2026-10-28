using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using NSec.Cryptography;
using Whey.Infra.Data;

namespace Whey.Server.Interceptors;

// Bearer-less proof-of-possession: every request carries the client's Ed25519 public key and a signature over
//   "{method}|{nonce}|{timestamp}|{hex(sha256(request body))}"
// The key must be registered, the timestamp fresh, and the nonce unused. Raw key/signature bytes travel in
// binary (-bin) metadata, which is the only way gRPC carries non-ASCII header values.
public class AuthenticationInterceptor : Interceptor
{
	public const string PublicKeyHeader = "whey-public-key-bin";
	public const string SignatureHeader = "whey-signature-bin";
	public const string NonceHeader = "whey-nonce";
	public const string TimestampHeader = "whey-timestamp"; // unix seconds

	public static readonly TimeSpan MaxClockSkew = TimeSpan.FromSeconds(60);
	private const int MaxNonceLength = 64;

	private readonly WheyContext _db;
	private readonly IDistributedCache _cache;

	public AuthenticationInterceptor(WheyContext db, IDistributedCache cache)
	{
		_db = db;
		_cache = cache;
	}

	public static string BuildCanonicalString(string method, string nonce, string timestamp, ReadOnlySpan<byte> body) =>
		$"{method}|{nonce}|{timestamp}|{Convert.ToHexString(SHA256.HashData(body))}";

	public static string EncodePublicKey(ReadOnlySpan<byte> key) => Convert.ToHexString(key);

	public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
			TRequest request,
			ServerCallContext context,
			UnaryServerMethod<TRequest, TResponse> continuation)
	{
		if (IsPublicMethod(context.Method))
		{
			return await continuation(request, context);
		}

		Metadata headers = context.RequestHeaders;
		byte[]? pubKey = headers.GetValueBytes(PublicKeyHeader);
		byte[]? signature = headers.GetValueBytes(SignatureHeader);
		string? nonce = headers.GetValue(NonceHeader);
		string? timestamp = headers.GetValue(TimestampHeader);

		if (pubKey is null || signature is null || string.IsNullOrEmpty(nonce) || timestamp is null)
		{
			throw Unauthenticated("Auth headers missing");
		}
		if (nonce.Length > MaxNonceLength)
		{
			throw Unauthenticated("Invalid nonce");
		}
		if (!long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out long unixSeconds) ||
			(DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(unixSeconds)).Duration() > MaxClockSkew)
		{
			throw Unauthenticated("Stale or invalid timestamp");
		}

		string canon = BuildCanonicalString(context.Method, nonce, timestamp, ((IMessage)request).ToByteArray());
		if (!VerifySignature(pubKey, Encoding.UTF8.GetBytes(canon), signature))
		{
			throw Unauthenticated("Invalid signature");
		}

		string pubKeyString = EncodePublicKey(pubKey);
		var client = await _db.Clients
			.AsNoTracking()
			.FirstOrDefaultAsync(c => c.PublicKey == pubKeyString, context.CancellationToken);

		if (client is null)
		{
			throw Unauthenticated("Client unregistered");
		}
		if (client.TokenExpiry <= DateTimeOffset.UtcNow)
		{
			throw Unauthenticated("Client registration expired");
		}

		// Only consume the nonce once the signature is known good, so unauthenticated callers can't fill the cache.
		// The entry outlives the timestamp window, so a replay is either stale or a duplicate.
		string nonceKey = $"auth:nonce:{pubKeyString}:{nonce}";
		if (await _cache.GetAsync(nonceKey, context.CancellationToken) is not null)
		{
			throw Unauthenticated("Nonce already used");
		}
		await _cache.SetAsync(nonceKey, [1], new DistributedCacheEntryOptions
		{
			AbsoluteExpirationRelativeToNow = MaxClockSkew * 2,
		}, context.CancellationToken);

		var identity = new ClaimsIdentity(
		[
			new Claim(ClaimTypes.NameIdentifier, client.Id.ToString()),
			new Claim("PublicKey", pubKeyString),
		], "bless-pop"); // "bearerless proof-of-possession"
		context.GetHttpContext().User = new ClaimsPrincipal(identity);

		return await continuation(request, context);
	}

	private static bool VerifySignature(byte[] pubKey, byte[] message, byte[] signature)
	{
		if (!PublicKey.TryImport(SignatureAlgorithm.Ed25519, pubKey, KeyBlobFormat.RawPublicKey, out var key) || key is null)
		{
			return false;
		}
		return SignatureAlgorithm.Ed25519.Verify(key, message, signature);
	}

	private static RpcException Unauthenticated(string detail) => new(new Status(StatusCode.Unauthenticated, detail));

	public bool IsPublicMethod(string method)
	{
		string[] publicMethods =
		[
			"/register.v1.RegistrationService/",
		];
		return publicMethods.Any(method.StartsWith);
	}
}

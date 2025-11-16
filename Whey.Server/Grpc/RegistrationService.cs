using System.Security.Cryptography;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Caching.Distributed;
using NSec.Cryptography;
using Whey.Infra.Data;
using Whey.Core.Models;
using Whey.Server.Converters;
using Whey.Server.Proto;

namespace Whey.Server.Grpc;

// TODO: find a better name for this
public class RegistrationServiceImpl : RegistrationService.RegistrationServiceBase
{
	private readonly WheyContext _context;
	private readonly IDistributedCache _cache;

	public RegistrationServiceImpl(WheyContext context, IDistributedCache cache)
	{
		_context = context;
		_cache = cache;
	}

	public override Task<ChallengeResponse> Challenge(ChallengeRequest request, ServerCallContext context)
	{
		const int NONCE_SIZE = 1 << 5;
		const int NONCE_EXPIRY = 5;

		Span<byte> bytes = stackalloc byte[NONCE_SIZE];
		RandomNumberGenerator.Fill(bytes);
		string nonce = bytes.ToString();

		DateTime absExpiry = DateTime.UtcNow.Add(TimeSpan.FromMinutes(NONCE_EXPIRY));

		var opts = new DistributedCacheEntryOptions
		{
			AbsoluteExpiration = absExpiry,
		};

		_cache.Set($"register:nonce:{nonce}", [1], opts);

		return Task.FromResult(new ChallengeResponse
		{
			Nonce = nonce,
			ExpiresAt = Timestamp.FromDateTime(absExpiry),
		});
	}

	/**
	 * Flow:
	 * - Client retrieves nonce from Server via Challenge();
	 * - Client must then generate an Ed25519 key pair client-side.
	 * - Client will then register the public key in Register().
	 * - Registration must contain a release signature so the server can verify the client's legitimacy.
	 * - Registration must contain an intent message, which will be signed with client's public key
	 * - Client ships off RegisterRequest to server.
	 * - Server will first verify the nonce.
	 * - Server will then verify the release signature.
	 * - Server will then verify the payload signature.
	 * - Server will perform the action and then register, returning a RegisterResponse if successful, or throw an HTTP error if not (?)
	 */
	public override async Task<RegisterResponse> Register(RegisterRequest request, ServerCallContext context)
	{
		// check nonce
		string key = $"register:nonce:{request.Nonce}";
		byte[]? _ = await _cache.GetAsync(key, context.CancellationToken) ??
			throw new RpcException(new Status(StatusCode.Unauthenticated, "provided challenge is invalid"));

		await _cache.RemoveAsync(key, context.CancellationToken);

		// check release signature
		// TODO: implement release signatures. store them in Db?

		// check payload signature

		var intent = new RegisterIntent
		{
			PublicKey = request.PublicKey,
			Challenge = request.Nonce,
			Version = request.Version,
			Platform = request.Platform,
			Purpose = "register", // method name but in camel case?
			RpcMethod = "/whey.WheyRegistration/Register", // ngl should probably change this
		};
		var nsecPublicKey = PublicKey.Import(SignatureAlgorithm.Ed25519, request.PublicKey.Span, KeyBlobFormat.RawPublicKey);
		bool valid = SignatureAlgorithm.Ed25519.Verify(nsecPublicKey, intent.ToByteArray(), request.PayloadSignature.Span);
		if (!valid)
		{
			throw new RpcException(new Status(StatusCode.Unauthenticated, "could not determine intent"));
		}

		string platform = PlatformConverter.ConvertProtoToString(request.Platform);

		const int TOKEN_EXPIRY = 30;
		var registerTime = DateTime.UtcNow;

		WheyClient client = new()
		{
			Id = Guid.CreateVersion7(),
			PublicKey = request.PublicKey.ToString()!,
			Version = request.Version,
			Platform = platform,
			TokenExpiry = registerTime.AddDays(TOKEN_EXPIRY),
			RegisteredAt = registerTime,
		};

		await _context.Clients.AddAsync(client, context.CancellationToken);

		return new RegisterResponse();
	}
}

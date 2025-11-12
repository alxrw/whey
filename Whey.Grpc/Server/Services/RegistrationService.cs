using System.Security.Cryptography;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.WebUtilities;
using Whey.Grpc.Server;

namespace Whey.Grpc.Server.Services;

public class RegistrationServiceImpl : RegistrationService.RegistrationServiceBase
{
	public RegistrationServiceImpl() { }

	public override Task<ChallengeResponse> Challenge(ChallengeRequest request, ServerCallContext context)
	{
		const int NONCE_SIZE = 1 << 5;
		const int NONCE_EXPIRY = 5;

		Span<byte> bytes = stackalloc byte[NONCE_SIZE];
		RandomNumberGenerator.Fill(bytes);
		string nonce = bytes.ToString();
		return Task.FromResult(new ChallengeResponse
		{
			Nonce = nonce,
			ExpiresAt = Timestamp.FromDateTime(DateTime.UtcNow.Add(TimeSpan.FromMinutes(NONCE_EXPIRY))),
		});
	}

	public override Task<RegisterResponse> Register(RegisterRequest request, ServerCallContext context)
	{
		return null;
	}
}

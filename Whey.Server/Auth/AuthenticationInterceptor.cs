using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.EntityFrameworkCore;
using NSec.Cryptography;
using Whey.Infra.Data;

namespace Whey.Server.Auth;

public class AuthenticationInterceptor : Interceptor
{
	private readonly WheyContext _db;

	public AuthenticationInterceptor(WheyContext db)
	{
		_db = db;
	}

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
		byte[]? pubKey = headers.GetValueBytes("whey-public-key");
		byte[]? payloadSignature = headers.GetValueBytes("whey-release-signature");
		string? nonce = headers.GetValue("whey-nonce"); // client generated nonce
		string? timestamp = headers.GetValue("whey-timestamp");

		// null check
		Object?[] items = [pubKey, payloadSignature, nonce, timestamp]; // scuffed
		foreach (Object? item in items)
		{
			if (item is null)
			{
				Status status = new(StatusCode.Unauthenticated, "Auth headers missing");
				throw new RpcException(status);
			}
		}

		var msg = (IMessage)request;
		byte[] hashBytes = SHA256.HashData(msg.ToByteArray());

		string canonString = $"{context.Method}|{nonce}|{timestamp}|{hashBytes}";
		byte[] canonBytes = System.Text.Encoding.UTF8.GetBytes(canonString);

		// verify payload signature
		try
		{
			var key = NSec.Cryptography.PublicKey.Import(SignatureAlgorithm.Ed25519, pubKey, KeyBlobFormat.RawPublicKey);
			bool isValid = SignatureAlgorithm.Ed25519.Verify(key, canonBytes, payloadSignature);

			if (!isValid)
			{
				Status status = new(StatusCode.Unauthenticated, "Invalid signature");
				throw new RpcException(status);
			}

			// guaranteed to not be null ?
			string pubKeyString = BitConverter.ToString(pubKey!);

			var client = await _db.Clients
				.AsNoTracking()
				.FirstOrDefaultAsync(c => c.PublicKey == pubKeyString);

			if (client is null)
			{
				Status status = new(StatusCode.Unauthenticated, "Client unregistered");
				throw new RpcException(status);
			}

			var claims = new[] {
				new Claim(ClaimTypes.NameIdentifier, client.Id.ToString()),
				new Claim("PublicKey", pubKeyString),
			};
			var identity = new ClaimsIdentity(claims, "bless-pop"); // "bearerless proof-of-possession"
			context.GetHttpContext().User = new ClaimsPrincipal(identity);
		}
		catch (Exception ex)
		{
			Status status = new(StatusCode.Unauthenticated, $"Auth failed: {ex.Message}");
			throw new RpcException(status);
		}

		return await continuation(request, context);
	}

	public bool IsPublicMethod(string method)
	{
		string[] publicMethods =
		[
			"/register.v1.RegistrationService/",
		];
		return publicMethods.Contains(method);
	}
}

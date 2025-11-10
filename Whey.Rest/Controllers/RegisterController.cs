using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using NSec.Cryptography;
using PeterO.Cbor;
using Whey.Rest.Data;
using Whey.Rest.Models;

namespace Whey.Rest.Controllers;

[Route("[controller]")]
[ApiController]
public class RegisterController : ControllerBase
{
	private readonly WheyContext _context;
	// TODO: currently using in memory cache. change to redis in the future?
	private readonly IDistributedCache _cache;

	public RegisterController(WheyContext context, IDistributedCache cache)
	{
		_context = context;
		_cache = cache;
	}

	[HttpGet("challenge")]
	public ActionResult<string> GetChallengeNonce()
	{
		const int NONCE_SIZE = 32;
		const int NONCE_EXPIRY = 5;

		Span<byte> bytes = stackalloc byte[NONCE_SIZE];
		RandomNumberGenerator.Fill(bytes);
		string nonce = WebEncoders.Base64UrlEncode(bytes);

		var opts = new DistributedCacheEntryOptions
		{
			AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(NONCE_EXPIRY)
		};
		_cache.Set($"register:nonce:{nonce}", [1], opts);

		return Ok(nonce);
	}

	// POST: Register
	// To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
	[HttpPost]
	public async Task<ActionResult<RegisterRequest>> PostWheyClient(RegisterRequest request)
	{
		if (!ModelState.IsValid)
		{
			return BadRequest(ModelState);
		}

		// verify nonce issued by challenge
		try
		{
			WebEncoders.Base64UrlDecode(request.Nonce);
		}
		catch (System.FormatException)
		{
			return BadRequest("invalid source");
		}

		string key = $"register:nonce:{request.Nonce}";
		byte[]? marker = await _cache.GetAsync(key);
		if (marker is null)
		{
			return Unauthorized("nonce invalid or expired");
		}

		await _cache.RemoveAsync(key);

		// verify release signature
		// TODO: when registering a release, release signature should be retrieved from some cache/storage and compared to the release signature of the request.

		// verify payload

		byte[] pkBytes = WebEncoders.Base64UrlDecode(request.PublicKey);
		if (pkBytes.Length != 32)
		{
			return Unauthorized("invalid public key");
		}

		byte[] signature = WebEncoders.Base64UrlDecode(request.PayloadSignature);
		if (signature.Length != 64)
		{
			return Unauthorized("invalid payload signature");
		}

		// build CBOR
		var cbor = CBORObject.NewOrderedMap()
			.Add("m", "POST")
			.Add("p", "/register")
			.Add("nonce", request.Nonce)
			.Add("pk", request.PublicKey)
			.Add("ver", request.Version)
			.Add("plat", request.Platform)
			.Add("purpose", "register:v1");
		byte[] msg = cbor.EncodeToBytes(CBOREncodeOptions.DefaultCtap2Canonical);

		var publicKey = PublicKey.Import(SignatureAlgorithm.Ed25519, pkBytes, KeyBlobFormat.RawPublicKey);
		if (!SignatureAlgorithm.Ed25519.Verify(publicKey, msg, signature))
		{
			return Unauthorized("payload signature invalid");
		}

		return null!;
	}
}

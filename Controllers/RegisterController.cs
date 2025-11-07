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
using Whey.Data;
using Whey.Models;

namespace Whey.Controllers
{
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

			// verify payload

			return null!;
		}

		private bool WheyClientExists(Guid id)
		{
			return _context.Clients.Any(e => e.Id == id);
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Whey.Data;
using Whey.Models;

namespace Whey.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class RegisterKeyController : ControllerBase
	{
		private readonly WheyContext _context;

		public RegisterKeyController(WheyContext context)
		{
			_context = context;
		}

		// GET: api/RegisterKey
		[HttpGet]
		public async Task<ActionResult<IEnumerable<WheyClient>>> GetClients()
		{
			return await _context.Clients.ToListAsync();
		}

		// GET: api/RegisterKey/5
		[HttpGet("{id}")]
		public async Task<ActionResult<WheyClient>> GetWheyClient(Guid id)
		{
			var wheyClient = await _context.Clients.FindAsync(id);

			if (wheyClient == null)
			{
				return NotFound();
			}

			return wheyClient;
		}

		// PUT: api/RegisterKey/5
		// To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
		[HttpPut("{id}")]
		public async Task<IActionResult> PutWheyClient(Guid id, WheyClient wheyClient)
		{
			if (id != wheyClient.Id)
			{
				return BadRequest();
			}

			_context.Entry(wheyClient).State = EntityState.Modified;

			try
			{
				await _context.SaveChangesAsync();
			}
			catch (DbUpdateConcurrencyException)
			{
				if (!WheyClientExists(id))
				{
					return NotFound();
				}
				else
				{
					throw;
				}
			}

			return NoContent();
		}

		// POST: api/RegisterKey
		// To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
		[HttpPost]
		public async Task<ActionResult<WheyClient>> PostWheyClient(WheyClient wheyClient)
		{
			_context.Clients.Add(wheyClient);
			await _context.SaveChangesAsync();

			return CreatedAtAction("GetWheyClient", new { id = wheyClient.Id }, wheyClient);
		}

		// DELETE: api/RegisterKey/5
		[HttpDelete("{id}")]
		public async Task<IActionResult> DeleteWheyClient(Guid id)
		{
			var wheyClient = await _context.Clients.FindAsync(id);
			if (wheyClient == null)
			{
				return NotFound();
			}

			_context.Clients.Remove(wheyClient);
			await _context.SaveChangesAsync();

			return NoContent();
		}

		private bool WheyClientExists(Guid id)
		{
			return _context.Clients.Any(e => e.Id == id);
		}
	}
}

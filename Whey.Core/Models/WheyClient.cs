using System.ComponentModel.DataAnnotations;

namespace Whey.Core.Models;
// Whey client as determined by the public key
public class WheyClient
{
	[Key]
	public Guid Id { get; set; } // INFO: must be UUIDv7/ULID

	[Required]
	public required string PublicKey { get; set; }

	public string? Version { get; set; }
	public string? Platform { get; set; }

	public DateTimeOffset TokenExpiry { get; set; }
	public DateTimeOffset RegisteredAt { get; set; }
}

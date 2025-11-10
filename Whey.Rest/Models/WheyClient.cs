using System.ComponentModel.DataAnnotations;
using System.Numerics;

namespace Whey.Rest.Models;
// Whey client as determined by the public key
public class WheyClient
{
	[Key]
	public Guid Id { get; set; } // INFO: must be UUIDv7/ULID

	[Required]
	public required string PublicKey { get; set; }

	public string? Version { get; set; }
	public string? Platform { get; set; }

	public required string ApiToken { get; set; }
	public DateTime TokenExpiry { get; set; }

	public DateTime RegisteredAt { get; set; }
	public string? IpAddress { get; set; }
}

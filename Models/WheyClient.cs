using System.ComponentModel.DataAnnotations;

namespace Whey.Models;
// Whey client as determined by the public key
public class WheyClient
{
	[Key]
	public Guid Id { get; set; } = Guid.CreateVersion7();

	[Required]
	public required string PublicKey { get; set; }

	[Required]
	public required string ReleaseSignature { get; set; }

	public string? Version { get; set; }
	public string? Platform { get; set; }
	public required string Nonce { get; set; }
	public long TimeStamp { get; set; }

	public required string ApiToken { get; set; }
	public DateTime TokenExpiry { get; set; }

	public DateTime RegisteredAt { get; set; }
	public string? IpAddress { get; set; }
}

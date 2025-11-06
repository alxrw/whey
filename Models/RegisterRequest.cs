namespace Whey.Models;

public class RegisterRequest
{
	public required string PublicKey { get; set; } // ed25519 public key
	public required string PayloadSignature { get; set; } // prove key ownership
	public required string ReleaseSignature { get; set; } // signature from parm binary
	public string? Version { get; set; }
	public string? Platform { get; set; }

	public required string Nonce { get; set; }
	public long TimeStamp { get; set; }
}

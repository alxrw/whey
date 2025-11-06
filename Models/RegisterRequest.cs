namespace Whey.Models;

public class RegisterRequest
{
	public string PublicKey { get; set; } // ed25519 public key
	public string PayloadSignature { get; set; } // prove key ownership
	public string ReleaseSignature { get; set; } // signature from parm binary
	public string Version { get; set; }
	public string Platform { get; set; }

	public string Nonce { get; set; }
	public long TimeStamp { get; set; }
}

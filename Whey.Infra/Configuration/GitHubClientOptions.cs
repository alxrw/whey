using System.ComponentModel.DataAnnotations;

namespace Whey.Infra.Configuration;

public class GitHubClientOptions
{
	[Required]
	public string Token { get; set; } = string.Empty;
	[Required]
	public string UserAgent { get; set; } = string.Empty;
}

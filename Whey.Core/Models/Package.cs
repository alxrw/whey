using System.ComponentModel.DataAnnotations;

namespace Whey.Core.Models;

public class Package
{
	[Key]
	public Guid Id { get; set; } // must be UUIDv7

	public required string Owner { get; init; }
	public required string Repo { get; init; }
	public required string Version { get; set; } // release tag name
	public Dictionary<Platform, string>? DownloadPaths { get; set; } // binary paths relative to 

	public Platform SupportedPlatforms { get; set; } = Platform.UNSPECIFIED;
	public List<string> Dependencies { get; set; } = [];
	public DateTime LastPolled { get; set; }
}

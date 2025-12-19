using System.ComponentModel.DataAnnotations;

namespace Whey.Core.Models;

public class Package
{
	[Key]
	public Guid Id { get; set; } // must be UUIDv7

	public required string Owner { get; init; }
	public required string Repo { get; init; }
	public required string Version { get; set; } // release tag name

	// INFO: should not be null but object constructor gets mad if it is
	public required DateTimeOffset? LastReleased { get; set; } // if no release, then can't add to Whey.
	public string? ETag { get; set; }
	public Dictionary<Platform, string>? DownloadPaths { get; set; } // binary paths

	public Platform SupportedPlatforms { get; set; } = Platform.UNSPECIFIED;
	public List<string> Dependencies { get; set; } = [];
	public DateTimeOffset LastPolled { get; set; }
}

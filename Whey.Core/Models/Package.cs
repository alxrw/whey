using Whey.Core.Models;

namespace Whey.Core.Models;

public class Package
{
	public required string Owner { get; init; }
	public required string Repo { get; init; }
	public required Release Release { get; set; }

	public Platform SupportedPlatforms { get; set; } = Platform.UNSPECIFIED;
	public List<string> Dependencies { get; set; } = [];
	public DateTime LastPolled { get; set; }
}

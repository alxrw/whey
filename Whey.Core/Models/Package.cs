using Octokit;

namespace Whey.Core.Models;

public class Package
{
	public required Repository Repository { get; init; }
	public Platform SupportedPlatforms { get; set; } = Platform.UNSPECIFIED;
	public List<string> Dependencies { get; set; } = [];
}

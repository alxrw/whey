namespace Whey.Core.Models;

public class Release
{
	public required string Version { get; set; } // release tag name
	public Dictionary<Platform, string>? DownloadPaths { get; set; }
}

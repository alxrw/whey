using Octokit;

namespace Whey.Tests.Fakes;

// A minimal fake for testing that wraps around a real GitHubClient
// but intercepts specific calls we need to control in tests.
public class FakeGitHubClientWrapper
{
	private readonly Dictionary<(string Owner, string Repo), Release> _releases = [];

	public void SetLatestRelease(string owner, string repo, Release release)
	{
		_releases[(owner, repo)] = release;
	}

	public Release? GetLatestRelease(string owner, string repo)
	{
		return _releases.TryGetValue((owner, repo), out var release) ? release : null;
	}

	// Creates a GitHubClient that can be used for testing.
	// NOTE: For actual integration tests, we mock at a higher level.
	// maybe mock an HTTP client and hardcode responses?
	public static GitHubClient CreateRealClient()
	{
		return new GitHubClient(new ProductHeaderValue("WheyTests"));
	}
}

public static class ReleaseFactory
{
	private static long _idCounter = 1;

	public static Release Create(
		string tagName,
		DateTimeOffset? publishedAt = null,
		IReadOnlyList<ReleaseAsset>? assets = null)
	{
		var id = _idCounter++;
		publishedAt ??= DateTimeOffset.UtcNow;

		// Use reflection to create Release since it has internal constructor
		var release = new Release(
				"", // url
				"", // htmlUrl
				"", // assetsUrl
				"", // uploadUrl
				id, // id
				"", // nodeId
				tagName, // tagName
				"", // targetCommitish
				tagName, // name
				"", // body
				false, // draft
				false, // prerelease
				DateTimeOffset.UtcNow, // createdAt
				publishedAt, // publishedAt
				null, // author
				"", // tarballUrl
				"", // zipballUrl
				assets ?? [] // assets
			);

		return release;
	}
}

/// Creates mock ReleaseAsset objects for testing.
public static class ReleaseAssetFactory
{
	private static int _idCounter = 1;

	public static ReleaseAsset Create(
		string name,
		string? browserDownloadUrl = null,
		string? contentType = null,
		int size = 1024)
	{
		var id = _idCounter++;
		browserDownloadUrl ??= $"https://github.com/test/test/releases/download/v1.0.0/{name}";
		contentType ??= "application/octet-stream";

		// Use reflection to create ReleaseAsset since it has internal constructor
		var asset = new ReleaseAsset(
				"", // url
				id, // id
				"", // nodeId
				name, // name
				"", // label
				"uploaded", // state
				contentType, // contentType
				size, // size
				0, // downloadCount
				DateTimeOffset.UtcNow, // createdAt
				DateTimeOffset.UtcNow, // updatedAt
				browserDownloadUrl, // browserDownloadUrl
				null // uploader
				);

		return asset;
	}
}

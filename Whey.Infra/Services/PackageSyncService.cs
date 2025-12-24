using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Octokit;
using Whey.Infra.Data;

namespace Whey.Infra.Services;

using WheyPackage = Whey.Core.Models.Package;

public interface IPackageSyncService
{
	public Task Sync(Guid packageId);
}

public class PackageSyncService
{
	private readonly ILogger<PackageSyncService> _logger;
	private readonly IHttpClientFactory _httpClientFactory;
	private readonly WheyContext _db;
	private readonly IBinStorageService _binStorageService;


	public PackageSyncService(ILogger<PackageSyncService> logger, IHttpClientFactory httpClientFactory, WheyContext db, IBinStorageService bsService)
	{
		_logger = logger;
		_httpClientFactory = httpClientFactory;
		_db = db;
		_binStorageService = bsService;
	}

	public async Task Sync(Guid packageId)
	{
		WheyPackage? package = await _db.Packages.FindAsync(packageId);
		if (package is null)
		{
			_logger.LogError("Cannot find package with Id {id}", packageId);
			return;
		}
		HttpClient client = _httpClientFactory.CreateClient();
		HttpRequestMessage msg = new(HttpMethod.Get, $"https://api.github.com/repos/{package.Owner}/{package.Repo}/releases/latest");
		// msg.Headers.UserAgent.Add(); // what??

		if (!string.IsNullOrEmpty(package.ETag))
		{
			msg.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(package.ETag));
		}

		using var response = await client.SendAsync(msg);

		// save some tokens??
		if (response.StatusCode == HttpStatusCode.NotModified)
		{
			_logger.LogInformation("{owner}/{repo} is already up to date.", package.Owner, package.Repo);
			package.LastPolled = DateTimeOffset.UtcNow;

			// TODO: put ts somewhere else, i.e. don't save until all current jobs are finished.
			await _db.SaveChangesAsync();
			return;
		}

		// WARNING: throws an exception! probably change this.
		// or maybe it's fine because of finally{}
		response.EnsureSuccessStatusCode();

		if (response.Headers.ETag is not null)
		{
			package.ETag = response.Headers.ETag.Tag;
		}

		var release = await response.Content.ReadFromJsonAsync<Release>();
		if (release is null)
		{
			_logger.LogError("Could not retrieve latest release for {owner}/{repo}", package.Owner, package.Repo);
			return;
		}

		// TODO: abstract ts logic somewhere else
		if (release.TagName != package.Version)
		{
			// new release
			var assets = release.Assets;

			// should we be parallelizing downloads?
			foreach (ReleaseAsset asset in assets)
			{
				using var stream = client.GetStreamAsync(asset.BrowserDownloadUrl);

				string fileName = $"{package.Owner}/{package.Repo}/{asset.Name}";

				// FIX: fetch account name from env vars
				BlobServiceClient blobClient = _binStorageService.GetBinStorageServiceClient("example");

				// FIX: fetch container name from env vars
				BlobContainerClient containerClient = blobClient.GetBlobContainerClient("whey-pkg-container");

				// TODO: make asset uploading atomic?
				// either all assets are uploaded, or none at all.
				// maybe use some sort of cleanup OR staging uploads
				await _binStorageService.UploadBinaryAsync(containerClient, fileName, stream.Result);
			}

			package.Version = release.TagName;
			package.LastReleased = release.PublishedAt;
		}

		package.LastPolled = DateTimeOffset.UtcNow;
		// entity MUST be tracked.
		await _db.SaveChangesAsync();
	}
}

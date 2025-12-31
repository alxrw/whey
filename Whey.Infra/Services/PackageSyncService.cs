using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Octokit;
using Whey.Core.Models;
using Whey.Infra.Data;
using Whey.Infra.Utils;

namespace Whey.Infra.Services;

using WheyPackage = Whey.Core.Models.Package;

public interface IPackageSyncService
{
	public Task Sync(Guid packageId);
}

public class PackageSyncService : IPackageSyncService
{
	private static readonly Platform[] Platforms = [
		Platform.Windows,
		Platform.Linux,
		Platform.Darwin
	];
	private static readonly ProcessorArchitecture[] Archs = [
		ProcessorArchitecture.Amd64,
		ProcessorArchitecture.Arm,
		ProcessorArchitecture.X86
	];

	private readonly ILogger<PackageSyncService> _logger;
	private readonly IHttpClientFactory _httpClientFactory;
	private readonly WheyContext _db;
	private readonly IBinStorageService _binStorageService;
	private readonly IAssetMappingService _assetMappingService;

	public PackageSyncService(
			ILogger<PackageSyncService> logger,
			IHttpClientFactory httpClientFactory,
			WheyContext db,
			IBinStorageService bsService,
			IAssetMappingService amService)
	{
		_logger = logger;
		_httpClientFactory = httpClientFactory;
		_db = db;
		_binStorageService = bsService;
		_assetMappingService = amService;
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

			string? azureAcctName = Environment.GetEnvironmentVariable("AZURE_ACCOUNT_NAME") ??
				throw new ArgumentNullException("could not find env var AZURE_ACCOUNT_NAME");

			string? azureContainerName = Environment.GetEnvironmentVariable("WHEY_CONTAINER_NAME") ??
				throw new ArgumentNullException("could not find env var WHEY_CONTAINER_NAME");


			// should we be parallelizing downloads?
			foreach (ReleaseAsset asset in assets)
			{
				string fileName = $"{package.Owner}/{package.Repo}/{asset.Name}";
				BlobServiceClient blobClient = _binStorageService.GetBinStorageServiceClient(azureAcctName);
				BlobContainerClient containerClient = blobClient.GetBlobContainerClient(azureContainerName);

				string jobDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
				Directory.CreateDirectory(jobDir);

				string tempPath = Path.Combine(jobDir, asset.Name);
				string? extractPath = null;

				// TODO: make asset uploading atomic?
				// either all assets are uploaded, or none at all.
				// maybe use some sort of cleanup OR staging uploads
				//
				// also make this concurrent/async in a different thread? maybe retry on fail?
				try
				{
					// download file to blob storage and VM for deps analysis
					using (var networkStream = await client.GetStreamAsync(asset.BrowserDownloadUrl))
					using (var fileStream = File.Create(tempPath))
					{
						await networkStream.CopyToAsync(fileStream);
					}

					// extract file
					if (asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
					{
						extractPath = Path.Combine(jobDir, Path.GetRandomFileName());
						Directory.CreateDirectory(extractPath);
						ZipFile.ExtractToDirectory(tempPath, extractPath);
					}
					else if (asset.Name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
							 asset.Name.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
					{
						extractPath = Path.Combine(jobDir, Path.GetRandomFileName());
						Directory.CreateDirectory(extractPath);

						using var fs = File.OpenRead(tempPath);
						using var gz = new GZipStream(fs, CompressionMode.Decompress);
						TarFile.ExtractToDirectory(gz, extractPath, overwriteFiles: true);
					}

					// Find dependencies w/ objdump (Linux only)
					if (extractPath is not null)
					{
						var bins = BinaryInspector.FindBinaries(extractPath);
						foreach (string bin in bins)
						{
							// can only do this for Linux, as server runs only on Linux
							var deps = DependencyFinderService.GetLibsLinux(bin);
							if (deps.Length != 0 && package.Dependencies.ContainsKey(Platform.Linux))
							{
								package.Dependencies[Platform.Linux] = deps;
							}
						}
					}

					using (var uploadStream = File.OpenRead(tempPath))
					{
						await _binStorageService.UploadBinaryAsync(containerClient, fileName, uploadStream);
					}
				}
				finally
				{
					if (Directory.Exists(jobDir))
					{
						Directory.Delete(jobDir, recursive: true);
					}
				}
			}

			// map platforms/archs to respective assets
			foreach (Platform plat in Platforms)
			{
				foreach (ProcessorArchitecture arch in Archs)
				{
					var matches = _assetMappingService.SelectReleaseAsset(assets, plat, arch, strictMatchesOnly: true);
				}
			}

			package.Version = release.TagName;
			package.LastReleased = release.PublishedAt;
		}

		package.LastPolled = DateTimeOffset.UtcNow;
		// entity MUST be tracked.
		// TODO: maybe only save when all current jobs are finished?
		await _db.SaveChangesAsync();
	}
}

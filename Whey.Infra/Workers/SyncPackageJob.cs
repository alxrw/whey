using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Octokit;
using Quartz;
using Whey.Infra.Data;

namespace Whey.Infra.Workers;

using GhPackage = Octokit.Package;
using WheyPackage = Whey.Core.Models.Package;

public class SyncPackageJob : IJob
{
	public static readonly JobKey Key = new("sync-package"); // idk what ts is for icl

	private readonly ILogger<SyncPackageJob> _logger;
	private readonly IHttpClientFactory _httpClientFactory;
	private readonly WheyContext _db;

	public SyncPackageJob(ILogger<SyncPackageJob> logger, IHttpClientFactory httpClientFactory, WheyContext db)
	{
		_logger = logger;
		_httpClientFactory = httpClientFactory;
		_db = db;
	}

	public async Task Execute(IJobExecutionContext context)
	{
		// TODO: change
		const int MAX_RETRIES = 3;

		if (!context.MergedJobDataMap.TryGetGuid("package-id", out Guid packageId))
		{
			_logger.LogError("Job failed: missing package-id from context.");
			return;
		}

		try
		{
			// INFO: may not even run since it would catch anyway?
			if (context.RefireCount >= MAX_RETRIES)
			{
				_logger.LogWarning("Job scheduled at {Date} cannot be run since MAX_RETRIES exceeded.", DateTime.UtcNow);
				return;
			}

			WheyPackage? package = await _db.Packages.FindAsync(packageId, context.CancellationToken);
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

			using var response = await client.SendAsync(msg, context.CancellationToken);

			// save some tokens??
			if (response.StatusCode == HttpStatusCode.NotModified)
			{
				_logger.LogInformation("{owner}/{repo} is already up to date.", package.Owner, package.Repo);
				package.LastPolled = DateTimeOffset.UtcNow;

				// TODO: put ts somewhere else, i.e. don't save until all current jobs are finished.
				await _db.SaveChangesAsync(context.CancellationToken);
				return;
			}

			// WARNING: throws an exception! probably change this.
			// or maybe it's fine because of finally{}
			response.EnsureSuccessStatusCode();

			if (response.Headers.ETag is not null)
			{
				package.ETag = response.Headers.ETag.Tag;
			}

			var release = await response.Content.ReadFromJsonAsync<Release>(cancellationToken: context.CancellationToken);
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
					using var stream = client.GetStreamAsync(asset.BrowserDownloadUrl, context.CancellationToken);

					string tempReleaseName = $"{release.Name}-{Guid.NewGuid()}";

					// TODO: store on blob storage?
					string path = Path.Combine("~", "whey", "pkg", package.Owner, package.Repo, $"{tempReleaseName}");
					using var fs = new FileStream(path, System.IO.FileMode.OpenOrCreate);
					try
					{
						await stream.Result.CopyToAsync(fs, context.CancellationToken);
					}
					catch (OperationCanceledException)
					{
						Directory.Delete(path);
					}
				}

				// TODO: change ts when using DTOs
				// am i going to use DTOs?
				package.Version = release.TagName;
				package.LastReleased = release.PublishedAt;
			}

			package.LastPolled = DateTimeOffset.UtcNow;
			// entity MUST be tracked.
			await _db.SaveChangesAsync(context.CancellationToken);
		}
		catch (Exception e)
		{
			_logger.LogError("Could not run job. Trace: {Error}", e.ToString());
			throw;
		}
		finally // even if the try fails, reschedule job anyway.
		{
			// make compiler happy.
			// should just change this later anyway to only have to fetch the package once per job instead of twice.
			var pkg = await _db.Packages.FindAsync(packageId, context.CancellationToken);
			WheyPackage package = pkg!;

			ApiSchedulingService apiService = (ApiSchedulingService)context.MergedJobDataMap.Get("apiService");
			DateTimeOffset nextRun = apiService.GetNextRun(package);
			ITrigger nextRunTrigger = TriggerBuilder.Create()
				.ForJob(context.JobDetail)
				.StartAt(nextRun)
				.Build();

			await context.Scheduler.ScheduleJob(nextRunTrigger);
		}
	}
}

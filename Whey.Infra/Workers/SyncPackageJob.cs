using Microsoft.Extensions.Logging;
using Octokit;
using Quartz;

namespace Whey.Infra.Workers;

using GhPackage = Octokit.Package;
using WheyPackage = Whey.Core.Models.Package;

public class SyncPackageJob : IJob
{
	public static readonly JobKey Key = new("sync-package");
	public readonly ILogger _logger;

	public SyncPackageJob(ILogger logger)
	{
		_logger = logger;
	}

	public async Task Execute(IJobExecutionContext context)
	{
		// TODO: change
		const int MAX_RETRIES = 3;

		try
		{
			// INFO: may not even run since it would catch anyway?
			if (context.RefireCount >= MAX_RETRIES)
			{
				_logger.LogWarning("Job scheduled at {Date} cannot be run since MAX_RETRIES exceeded.", DateTime.UtcNow);
				return;
			}

			GitHubClient gitHubClient = (GitHubClient)context.MergedJobDataMap.Get("gitHubClient");
			WheyPackage package = (WheyPackage)context.MergedJobDataMap.Get("package"); // use DTO?
			HttpClient downloadClient = (HttpClient)context.MergedJobDataMap.Get("downloadClient");

			var release = await gitHubClient.Repository.Release.GetLatest(package.Owner, package.Repo);

			// TODO: abstract ts logic somewhere else
			if (release.TagName != package.Version)
			{
				// new release
				var assets = release.Assets;
				foreach (ReleaseAsset asset in assets)
				{
					using var stream = downloadClient.GetStreamAsync(asset.BrowserDownloadUrl, context.CancellationToken);

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
				package.LastPolled = DateTimeOffset.UtcNow;
				package.Version = release.TagName;
			}
		}
		catch (Exception e)
		{
			_logger.LogError("Could not run job. Trace: {Error}", e.ToString());
		}
		finally // even if the try fails, reschedule job anyway.
		{
			WheyPackage package = (WheyPackage)context.MergedJobDataMap.Get("package"); // use DTO?
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

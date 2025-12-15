using Octokit;
using Quartz;

namespace Whey.Infra.Workers;

using GhPackage = Octokit.Package;
using WheyPackage = Whey.Core.Models.Package;

public class SyncPackageJob : IJob
{
	public static readonly JobKey Key = new("sync-package");

	public async Task Execute(IJobExecutionContext context)
	{
		// TODO: change
		const int MAX_RETRIES = 3;

		try
		{
			if (context.RefireCount >= MAX_RETRIES)
			{
				// TODO: error log
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

					// INFO: store on blob storage?
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
				package.LastPolled = DateTime.UtcNow;
				package.Version = release.TagName;
			}
		}
		catch (Exception)
		{

		}
		finally
		{
			// TODO: reschedule the job here
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

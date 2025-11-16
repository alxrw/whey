using Octokit;
using Quartz;

namespace Whey.Infra.Workers;

using GhPackage = Octokit.Package;
using GhRelease = Octokit.Release;

using WheyPackage = Whey.Core.Models.Package;
using WheyRelease = Whey.Core.Models.Release;

public class SyncPackageJob : IJob
{
	public static readonly JobKey Key = new("sync-package");

	public async Task Execute(IJobExecutionContext context)
	{
		// TODO: change
		if (context.RefireCount >= 3)
		{
			return;
		}

		GitHubClient client = (GitHubClient)context.MergedJobDataMap.Get("client");
		WheyPackage package = (WheyPackage)context.MergedJobDataMap.Get("package");

		var releases = await client.Repository.Release.GetLatest(package.Owner, package.Repo);
	}
}

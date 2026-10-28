using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl;
using Whey.Core.Models;
using Whey.Infra.Data;
using Whey.Infra.Extensions;
using Whey.Infra.Workers;

namespace Whey.Infra.Services;

public interface IApiSchedulingService
{
	public (IJobDetail jobDetail, ITrigger trigger) GetNextScheduledJob(Package pkg, DateTimeOffset? startAt = null);
	public DateTimeOffset GetNextRun(Package pkg);
}

// Whenever a PackageSyncService worker runs, the ApiSchedulingService worker determines the next time
// the PackageSyncService worker should run.

// lowkey this should probably not be in here but w/e
public class ApiSchedulingService : IApiSchedulingService
{
	// tier 1: every hour, release 2 weeks ago OR top 5% in downloads
	// tier 2: every 6 hours, releases within 3 months OR top 25%
	// tier 3: every 24 hours, releases within the last year OR top 50%. default.
	// tier 4: every 72 hours, releases over a year ago, bottom 50%
	private static readonly TimeSpan TIER1 = TimeSpan.FromHours(1);
	private static readonly TimeSpan TIER2 = TimeSpan.FromHours(6);
	private static readonly TimeSpan TIER3 = TimeSpan.FromDays(1);
	private static readonly TimeSpan TIER4 = TimeSpan.FromDays(3);

	private readonly WheyContext _db;
	private readonly ILogger _logger;

	public ApiSchedulingService(WheyContext ctx, ILogger<ApiSchedulingService> logger)
	{
		_db = ctx;
		_logger = logger;
	}

	// Gets next IJobDetail and ITrigger to schedule next job
	// TODO: check if job exists?
	public (IJobDetail jobDetail, ITrigger trigger) GetNextScheduledJob(Package pkg, DateTimeOffset? startAt = null)
	{
		DateTimeOffset nextRun;
		if (startAt.HasValue)
		{
			nextRun = startAt.Value;
		}
		else
		{
			const int MinsJitter = 6;
			var jitter = (int)TimeSpan.FromMinutes(MinsJitter).TotalSeconds;
			nextRun = GetNextRun(pkg).AddJitter(jitter);
		}

		var jobKey = new JobKey($"sync-{pkg.Id}", "packages");

		IJobDetail job = JobBuilder.Create<SyncPackageJob>()
			.WithIdentity(jobKey)
			.UsingJobData("package-id", pkg.Id)
			.Build();

		ITrigger nextRunTrigger = TriggerBuilder.Create()
			.ForJob(jobKey)
			.WithIdentity($"trigger-{pkg.Id}", "packages")
			.StartAt(nextRun)
			.Build();

		return (job, nextRunTrigger);
	}

	public DateTimeOffset GetNextRun(Package pkg)
	{
		var stats = _db.PackageStats.Find(pkg.Id); // assume pkg id is the exact same as stats id
		if (stats is null)
		{
			_logger.LogError("Couldn't retrieve package stats for {pkg}.", $"{pkg.Owner}/{pkg.Repo}");
			return DateTimeOffset.UnixEpoch;
		}

		long total = stats.TotalInteractions;
		int higher = _db.PackageStats.Count(s => s.TotalInteractions > total);
		int numPkg = _db.PackageStats.Count();

		if (numPkg == 0)
		{
			numPkg = 1;
		}

		double percentile = InteractionPercentile(total, higher, numPkg);
		TimeSpan? age = DateTimeOffset.UtcNow - pkg.LastReleased;
		if (age is null)
		{
			_logger.LogError("Couldn't determine age of {pkg}'s last release.", $"{pkg.Owner}/{pkg.Repo}");
			return DateTimeOffset.UnixEpoch;
		}

		return DateTimeOffset.UtcNow.Add(SelectInterval(percentile, age.Value.TotalDays));
	}

	// 1.0 is the most-interacted-with package. Packages with no interactions have no popularity signal:
	// without this rule they all tie as "nobody has more", i.e. percentile 1.0, and would all be polled hourly.
	internal static double InteractionPercentile(long total, int higherCount, int packageCount) =>
		total <= 0 ? 0.0 : 1.0 - ((double)higherCount / Math.Max(1, packageCount));

	// Pure tier selection. percentile is in (0, 1], where 1.0 is the most-interacted-with package.
	internal static TimeSpan SelectInterval(double percentile, double ageDays)
	{
		const int T1_DAYS = 14, T2_DAYS = 90, T3_DAYS = 365;
		const double T1_P = 0.95, T2_P = 0.75, T3_P = 0.5;

		if (ageDays <= T1_DAYS || percentile >= T1_P)
		{
			return TIER1;
		}
		if (ageDays <= T2_DAYS || percentile >= T2_P)
		{
			return TIER2;
		}
		if (ageDays <= T3_DAYS || percentile >= T3_P)
		{
			return TIER3;
		}
		if (ageDays >= T3_DAYS || percentile <= T3_P)
		{
			return TIER4;
		}
		return TIER3;
	}
}

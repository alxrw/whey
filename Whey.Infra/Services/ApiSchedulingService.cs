using Microsoft.Extensions.Logging;
using Whey.Core.Models;
using Whey.Infra.Data;

namespace Whey.Infra.Services;

public interface IApiSchedulingService
{
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

	public ApiSchedulingService(WheyContext ctx, ILogger logger)
	{
		_db = ctx;
		_logger = logger;
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

		double percentile = 1.0 - ((double)higher / numPkg);
		TimeSpan? age = DateTimeOffset.UtcNow - pkg.LastReleased;
		if (age is null)
		{
			_logger.LogError("Couldn't determine age of {pkg}'s last release.", $"{pkg.Owner}/{pkg.Repo}");
			return DateTimeOffset.UnixEpoch;
		}

		const int T1_DAYS = 14, T2_DAYS = 90, T3_DAYS = 365;
		const double T1_P = 0.95, T2_P = 0.75, T3_P = 0.5;

		double ageDouble = age.Value.TotalDays;

		if (ageDouble <= T1_DAYS || percentile >= T1_P)
		{
			return DateTimeOffset.UtcNow.Add(TIER1);
		}
		if (ageDouble <= T2_DAYS || percentile >= T2_P)
		{
			return DateTimeOffset.UtcNow.Add(TIER2);
		}
		if (ageDouble <= T3_DAYS || percentile >= T3_P)
		{
			return DateTimeOffset.UtcNow.Add(TIER3);
		}
		if (ageDouble >= T3_DAYS || percentile <= T3_P)
		{
			return DateTimeOffset.UtcNow.Add(TIER4);
		}
		return DateTimeOffset.UtcNow.Add(TIER3);
	}
}

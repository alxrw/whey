using Whey.Core.Models;
using Whey.Infra.Data;

namespace Whey.Infra.Workers;

// Whenever a PackageSyncService worker runs, the ApiSchedulingService worker determines the next time
// the PackageSyncService worker should run.
public class ApiSchedulingService
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

	public ApiSchedulingService(WheyContext ctx)
	{
		_db = ctx;
	}

	public DateTimeOffset GetNextRun(Package pkg)
	{
		var stats = _db.PackageStats.Find(pkg.Id);

		// WARNING: temporary
		return DateTimeOffset.FromUnixTimeSeconds(67);
	}
}

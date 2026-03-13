using Microsoft.Extensions.Logging;
using Quartz;
using Whey.Infra.Data;
using Whey.Infra.Services;

namespace Whey.Infra.Workers;

using WheyPackage = Core.Models.Package;

[DisallowConcurrentExecution]
public class SyncPackageJob : IJob
{
	private readonly ILogger<SyncPackageJob> _logger;
	private readonly WheyContext _db;
	private readonly PackageSyncService _syncService;
	private readonly IApiSchedulingService _schedulingService;

	public SyncPackageJob(ILogger<SyncPackageJob> logger, WheyContext db, PackageSyncService syncService, IApiSchedulingService schedulingService)
	{
		_logger = logger;
		_db = db;
		_syncService = syncService;
		_schedulingService = schedulingService;
	}

	public async Task Execute(IJobExecutionContext context)
	{
		// TODO: change, idk if this will even work ngl
		const int MaxRetries = 3;

		if (!context.MergedJobDataMap.TryGetGuid("package-id", out Guid packageId))
		{
			_logger.LogError("Job failed: missing package-id from context.");
			return;
		}

		var pkg = await _db.Packages.FindAsync(packageId, context.CancellationToken);

		if (pkg is null)
		{
			_logger.LogError("Cannot find package with Id {id}", packageId);
			return;
		}

		try
		{
			// INFO: may not even run since it would catch anyway?
			if (context.RefireCount >= MaxRetries)
			{
				_logger.LogWarning("Job scheduled at {Date} cannot be run since MAX_RETRIES exceeded.", DateTime.UtcNow);
				return;
			}

			await _syncService.Sync(pkg);
		}
		catch (Exception e)
		{
			_logger.LogError("Could not run job. Trace: {Error}", e.ToString());
			throw;
		}
		finally // even if the try fails, reschedule job anyway.
		{
			var (jobDetail, trigger) = _schedulingService.GetNextScheduledJob(pkg);

			await context.Scheduler.ScheduleJob(jobDetail, trigger);
		}
	}
}

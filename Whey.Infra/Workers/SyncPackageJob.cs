using Microsoft.Extensions.Logging;
using Quartz;
using Whey.Infra.Data;
using Whey.Infra.Services;

namespace Whey.Infra.Workers;

using WheyPackage = Whey.Core.Models.Package;

// TODO: add jobs to queue if scheduled to run at the same exact time.
public class SyncPackageJob : IJob
{
	public static readonly JobKey Key = new("sync-package"); // idk what ts is for icl

	private readonly ILogger<SyncPackageJob> _logger;
	private readonly WheyContext _db;
	private readonly PackageSyncService _syncService;

	public SyncPackageJob(ILogger<SyncPackageJob> logger, WheyContext db, PackageSyncService syncService)
	{
		_logger = logger;
		_db = db;
		_syncService = syncService;
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

			await _syncService.Sync(packageId);
		}
		catch (Exception e)
		{
			_logger.LogError("Could not run job. Trace: {Error}", e.ToString());
			throw;
		}
		finally // even if the try fails, reschedule job anyway.
		{
			// make compiler happy.
			// TODO: should just change this later anyway to only have to fetch the package once per job instead of twice.
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

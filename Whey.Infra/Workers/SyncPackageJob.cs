using Microsoft.Extensions.Logging;
using Quartz;
using Whey.Infra.Data;
using Whey.Infra.Extensions;
using Whey.Infra.Services;

namespace Whey.Infra.Workers;

using WheyPackage = Whey.Core.Models.Package;

[DisallowConcurrentExecution]
public class SyncPackageJob : IJob
{
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
		const int MaxRetries = 3;

		if (!context.MergedJobDataMap.TryGetGuid("package-id", out Guid packageId))
		{
			_logger.LogError("Job failed: missing package-id from context.");
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

			const int MinsJitter = 6;
			var jitter = (int)TimeSpan.FromMinutes(MinsJitter).TotalSeconds;

			ApiSchedulingService apiService = (ApiSchedulingService)context.MergedJobDataMap.Get("apiService");
			DateTimeOffset nextRun = apiService.GetNextRun(package)
				.AddJitter(jitter);

			var jobKey = new JobKey($"sync-{packageId}", "packages");

			IJobDetail job = JobBuilder.Create<SyncPackageJob>()
				.WithIdentity(jobKey)
				.UsingJobData("package-id", packageId)
				.Build();

			ITrigger nextRunTrigger = TriggerBuilder.Create()
				.ForJob(jobKey)
				.WithIdentity($"trigger-{packageId}", "packages")
				.StartAt(nextRun)
				.Build();

			await context.Scheduler.ScheduleJob(job, nextRunTrigger);
		}
	}
}

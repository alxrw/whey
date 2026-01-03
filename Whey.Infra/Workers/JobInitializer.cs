using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using Whey.Infra.Data;
using Whey.Infra.Services;

namespace Whey.Infra.Workers;

public class JobInitializer : BackgroundService
{
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<JobInitializer> _logger;

	public JobInitializer(IServiceProvider serviceProvider, ILogger<JobInitializer> logger)
	{
		_serviceProvider = serviceProvider;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		using var scope = _serviceProvider.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<WheyContext>();
		var schedulerFactory = scope.ServiceProvider.GetRequiredService<ISchedulerFactory>();
		var apiSchedulingService = scope.ServiceProvider.GetRequiredService<IApiSchedulingService>();
		var scheduler = await schedulerFactory.GetScheduler(stoppingToken);

		const int BatchSize = 100;
		Guid? lastId = null;

		while (!stoppingToken.IsCancellationRequested)
		{
			var query = db.Packages.AsNoTracking().OrderBy(p => p.Id).AsQueryable();
			if (lastId.HasValue)
			{
				query = query.Where(p => p.Id > lastId.Value);
			}

			var batch = await query.Take(BatchSize).ToListAsync(stoppingToken);

			if (batch.Count == 0)
			{
				break;
			}

			foreach (var pkg in batch)
			{
				lastId = pkg.Id;
				var (jobDetail, trigger) = apiSchedulingService.GetNextScheduledJob(pkg);
				if (jobDetail != null && trigger != null)
				{
					await scheduler.ScheduleJob(jobDetail, trigger, stoppingToken);
				}
			}
		}
	}
}

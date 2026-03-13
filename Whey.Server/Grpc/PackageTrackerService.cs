using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Octokit;
using Quartz;
using Whey.Core.Models.Stats;
using Whey.Infra.Data;
using Whey.Infra.Services;
using Whey.Server.Converters;
using Whey.Server.Proto;

namespace Whey.Server.Grpc;

using WheyPackage = Core.Models.Package;

[Authorize]
public class PackageTrackerServiceImpl : PackageTrackerService.PackageTrackerServiceBase
{
	private readonly WheyContext _db;
	private readonly IGitHubClient _client;
	private readonly ISchedulerFactory _schedulerFactory;
	private readonly IApiSchedulingService _schedulingService;

	public PackageTrackerServiceImpl(
		WheyContext db,
		IGitHubClient client,
		ISchedulerFactory schedulerFactory,
		IApiSchedulingService schedulingService)
	{
		_db = db;
		_client = client;
		_schedulerFactory = schedulerFactory;
		_schedulingService = schedulingService;
	}

	public override async Task<EnsureTrackedResponse> EnsureTracked(EnsureTrackedRequest request, ServerCallContext context)
	{
		Release rel = await _client.Repository.Release.GetLatest(request.Owner, request.Repo);
		WheyPackage package = new()
		{
			Owner = request.Owner,
			Repo = request.Repo,
			Version = rel.TagName,
			LastReleased = rel.PublishedAt,
			SupportedPlatforms = PlatformConverter.ConvertStringToCore(request.Platform),
		};
		bool exists = await _db.Packages
			.AnyAsync(p => p.Owner == request.Owner && p.Repo == request.Repo);

		if (!exists)
		{
			_db.Packages.Add(package);
			package.Id = Guid.CreateVersion7();

			PackageStatistics stats = new()
			{
				Id = package.Id, // change? maybe just generate a long?
				PackageId = package.Id,
				Installs = new(),
			};
			stats.Installs.Track();
			_db.PackageStats.Add(stats);

			// TODO: put this elsewhere, maybe batch saves?
			try
			{
				await _db.SaveChangesAsync();
			}
			catch (DbUpdateException)
			{
				return new EnsureTrackedResponse();
			}

			var (job, trigger) = _schedulingService.GetNextScheduledJob(package, startAt: DateTimeOffset.UtcNow);
			var scheduler = await _schedulerFactory.GetScheduler();
			await scheduler.ScheduleJob(job, trigger);
		}

		return new EnsureTrackedResponse();
	}

	// TODO: report updates

	public override async Task<ReportInstallResponse> ReportInstall(ReportInstallRequest request, ServerCallContext context)
	{
		var pkg = await _db.Packages
			.FirstOrDefaultAsync(p => p.Owner == request.Owner && p.Repo == request.Repo);

		// TODO: do something, increment download metrics

		return new ReportInstallResponse();
	}
}
